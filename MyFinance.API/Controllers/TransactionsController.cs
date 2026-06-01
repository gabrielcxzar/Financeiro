using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions(
            [FromQuery] int? month,
            [FromQuery] int? year,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .Include(t => t.Category)
                .AsQueryable();

            if (month.HasValue && year.HasValue)
            {
                var startDate = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
                var endDate = startDate.AddMonths(1);
                query = query.Where(t => t.Date >= startDate && t.Date < endDate);
            }

            return await query.OrderByDescending(t => t.Date).ToListAsync(cancellationToken);
        }

        [HttpGet("invoice")]
        public async Task<ActionResult<object>> GetInvoiceSummary([FromQuery] int accountId, [FromQuery] int month, [FromQuery] int year)
        {
            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null || !account.IsCreditCard) return BadRequest("Conta invalida");

            int closingDay = account.ClosingDay ?? 1;
            int dueDay = account.DueDay ?? 10;

            int daysInMonth = DateTime.DaysInMonth(year, month);
            int safeClosingDay = Math.Min(closingDay, daysInMonth);
            DateTime closeDate = new DateTime(year, month, safeClosingDay, 23, 59, 59, DateTimeKind.Utc);

            DateTime startDate = closeDate.AddMonths(-1).AddDays(1);

            DateTime dueDate;
            if (dueDay < closingDay)
            {
                var nextMonthDate = closeDate.AddMonths(1);
                int daysInNextMonth = DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month);
                int safeDueDay = Math.Min(dueDay, daysInNextMonth);
                dueDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, safeDueDay, 12, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                int safeDueDay = Math.Min(dueDay, daysInMonth);
                dueDate = new DateTime(year, month, safeDueDay, 12, 0, 0, DateTimeKind.Utc);
            }

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.AccountId == accountId && t.UserId == userId && t.Date >= startDate && t.Date <= closeDate)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var total = transactions.Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount);

            return new
            {
                period = $"{startDate:dd/MM} a {closeDate:dd/MM}",
                dueDate = dueDate,
                total,
                status = total > 0 ? "Aberta" : "Paga",
                transactions
            };
        }

        [HttpPost]
        public async Task<ActionResult<Transaction>> PostTransaction([FromBody] UpsertTransactionDto request)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest("Descricao obrigatoria.");
            if (request.Amount <= 0)
                return BadRequest("Valor deve ser maior que zero.");
            if (request.AccountId <= 0)
                return BadRequest("Conta invalida.");
            if (request.CategoryId <= 0)
                return BadRequest("Categoria invalida.");

            var transactionDate = request.Date == default ? DateTime.UtcNow : request.Date.ToUniversalTime();

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);
            if (account == null)
                return BadRequest("Conta invalida.");

            int parcelas = request.Installments > 1 ? request.Installments : 1;
            decimal valorParcela = request.Amount;

            string? installmentId = null;
            if (parcelas > 1)
            {
                installmentId = Guid.NewGuid().ToString();
            }

            DateTime dataBase = transactionDate;

            var created = new List<Transaction>();

            for (int i = 0; i < parcelas; i++)
            {
                var novaTransacao = new Transaction
                {
                    UserId = userId,
                    CategoryId = request.CategoryId,
                    AccountId = request.AccountId,
                    Type = request.Type,
                    Paid = request.Paid,
                    Amount = valorParcela,
                    Description = parcelas > 1 ? $"{request.Description} ({i + 1}/{parcelas})" : request.Description,
                    Date = dataBase.AddMonths(i).ToUniversalTime(),
                    InstallmentId = installmentId
                };

                _context.Transactions.Add(novaTransacao);
                created.Add(novaTransacao);

                if (novaTransacao.Type == "Income") account.CurrentBalance += novaTransacao.Amount;
                else account.CurrentBalance -= novaTransacao.Amount;

                _context.Entry(account).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();

            var first = created.FirstOrDefault();
            if (first == null) return Ok();

            return CreatedAtAction("GetTransactions", new { id = first.Id }, first);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, [FromBody] UpsertTransactionDto request)
        {
            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest("Descricao obrigatoria.");
            if (request.Amount <= 0)
                return BadRequest("Valor deve ser maior que zero.");
            if (request.AccountId <= 0)
                return BadRequest("Conta invalida.");
            if (request.CategoryId <= 0)
                return BadRequest("Categoria invalida.");

            var oldTransaction = await _context.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (oldTransaction == null) return NotFound();

            var transaction = new Transaction
            {
                Id = id,
                UserId = userId,
                CategoryId = request.CategoryId,
                AccountId = request.AccountId,
                Type = request.Type,
                Paid = request.Paid,
                Amount = request.Amount,
                Description = request.Description,
                Date = request.Date.ToUniversalTime(),
                InstallmentId = oldTransaction.InstallmentId
            };

            var oldAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == oldTransaction.AccountId && a.UserId == userId);
            var newAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);
            if (newAccount == null)
                return BadRequest("Conta invalida.");

            if (oldAccount != null)
            {
                if (oldTransaction.Type == "Income") oldAccount.CurrentBalance -= oldTransaction.Amount;
                else oldAccount.CurrentBalance += oldTransaction.Amount;

                _context.Entry(oldAccount).State = EntityState.Modified;
            }

            if (newAccount != null)
            {
                if (transaction.Type == "Income") newAccount.CurrentBalance += transaction.Amount;
                else newAccount.CurrentBalance -= transaction.Amount;

                _context.Entry(newAccount).State = EntityState.Modified;
            }

            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id, [FromQuery] bool deleteAll = false)
        {
            var userId = GetUserId();
            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction == null) return NotFound();

            List<Transaction> transactionsToDelete = new List<Transaction>();

            if (deleteAll && !string.IsNullOrEmpty(transaction.InstallmentId))
            {
                transactionsToDelete = await _context.Transactions
                    .Where(t => t.InstallmentId == transaction.InstallmentId && t.UserId == userId)
                    .ToListAsync();
            }
            else
            {
                transactionsToDelete.Add(transaction);
            }

            foreach (var t in transactionsToDelete)
            {
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == t.AccountId && a.UserId == userId);
                if (account != null)
                {
                    if (t.Type == "Income") account.CurrentBalance -= t.Amount;
                    else account.CurrentBalance += t.Amount;

                    _context.Entry(account).State = EntityState.Modified;
                }

                _context.Transactions.Remove(t);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer(TransferDto request)
        {
            var userId = GetUserId();
            if (request.Amount <= 0) return BadRequest("Valor invalido.");
            if (request.FromAccountId <= 0 || request.ToAccountId <= 0 || request.FromAccountId == request.ToAccountId)
                return BadRequest("Contas invalidas");
            var dateUtc = request.Date.ToUniversalTime();

            var expense = new Transaction
            {
                UserId = userId,
                AccountId = request.FromAccountId,
                Amount = request.Amount,
                Description = "Transferencia para conta/cartao",
                Type = "Expense",
                Date = dateUtc,
                Paid = true
            };

            var income = new Transaction
            {
                UserId = userId,
                AccountId = request.ToAccountId,
                Amount = request.Amount,
                Description = "Recebido de transferencia",
                Type = "Income",
                Date = dateUtc,
                Paid = true
            };

            var fromAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromAccountId && a.UserId == userId);
            var toAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToAccountId && a.UserId == userId);

            if (fromAcc == null || toAcc == null)
                return BadRequest("Contas invalidas");

            fromAcc.CurrentBalance -= request.Amount;
            toAcc.CurrentBalance += request.Amount;

            _context.Transactions.Add(expense);
            _context.Transactions.Add(income);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Transferencia/Pagamento realizado!" });
        }
    }

    public class TransferDto
    {
        public int FromAccountId { get; set; }
        public int ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    public class UpsertTransactionDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense";
        public int CategoryId { get; set; }
        public int AccountId { get; set; }
        public DateTime Date { get; set; }
        public bool Paid { get; set; }
        public int Installments { get; set; } = 1;
    }
}
