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
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = GetUserId();
            var query = _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Category)
                .AsQueryable();

            if (month.HasValue && year.HasValue)
            {
                query = query.Where(t => t.Date.Month == month && t.Date.Year == year);
            }

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        [HttpGet("invoice")]
        public async Task<ActionResult<object>> GetInvoiceSummary([FromQuery] int accountId, [FromQuery] int month, [FromQuery] int year)
        {
            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);

            if (account == null || !account.IsCreditCard) return BadRequest("Conta inválida");

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
        public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
        {
            var userId = GetUserId();
            transaction.UserId = userId;

            if (transaction.Date == default) transaction.Date = DateTime.UtcNow;
            else transaction.Date = transaction.Date.ToUniversalTime();

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

            int parcelas = transaction.Installments > 1 ? transaction.Installments : 1;
            decimal valorParcela = transaction.Amount;

            string? installmentId = null;
            if (parcelas > 1)
            {
                installmentId = Guid.NewGuid().ToString();
            }

            DateTime dataBase = transaction.Date;

            var created = new List<Transaction>();

            for (int i = 0; i < parcelas; i++)
            {
                var novaTransacao = new Transaction
                {
                    UserId = userId,
                    CategoryId = transaction.CategoryId,
                    AccountId = transaction.AccountId,
                    Type = transaction.Type,
                    Paid = transaction.Paid,
                    Amount = valorParcela,
                    Description = parcelas > 1 ? $"{transaction.Description} ({i + 1}/{parcelas})" : transaction.Description,
                    Date = dataBase.AddMonths(i).ToUniversalTime(),
                    InstallmentId = installmentId
                };

                _context.Transactions.Add(novaTransacao);
                created.Add(novaTransacao);

                if (account != null)
                {
                    if (novaTransacao.Type == "Income") account.CurrentBalance += novaTransacao.Amount;
                    else account.CurrentBalance -= novaTransacao.Amount;

                    _context.Entry(account).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();

            var first = created.FirstOrDefault();
            if (first == null) return Ok();

            return CreatedAtAction("GetTransactions", new { id = first.Id }, first);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, Transaction transaction)
        {
            var userId = GetUserId();
            if (id != transaction.Id) return BadRequest();

            var oldTransaction = await _context.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (oldTransaction == null) return NotFound();

            transaction.UserId = userId;
            transaction.Date = transaction.Date.ToUniversalTime();
            transaction.InstallmentId = oldTransaction.InstallmentId;

            var oldAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == oldTransaction.AccountId && a.UserId == userId);
            var newAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

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
            var dateUtc = request.Date.ToUniversalTime();

            var expense = new Transaction
            {
                UserId = userId,
                AccountId = request.FromAccountId,
                Amount = request.Amount,
                Description = "Transferência para conta/cartão",
                Type = "Expense",
                Date = dateUtc,
                Paid = true
            };

            var income = new Transaction
            {
                UserId = userId,
                AccountId = request.ToAccountId,
                Amount = request.Amount,
                Description = "Recebido de transferência",
                Type = "Income",
                Date = dateUtc,
                Paid = true
            };

            var fromAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromAccountId && a.UserId == userId);
            var toAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToAccountId && a.UserId == userId);

            if (fromAcc == null || toAcc == null)
                return BadRequest("Contas inválidas");

            fromAcc.CurrentBalance -= request.Amount;
            toAcc.CurrentBalance += request.Amount;

            _context.Transactions.Add(expense);
            _context.Transactions.Add(income);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Transferência/Pagamento realizado!" });
        }
    }

    public class TransferDto
    {
        public int FromAccountId { get; set; }
        public int ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}
