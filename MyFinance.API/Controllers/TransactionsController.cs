using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using MyFinance.API.Services;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFinancialSnapshotService _financialSnapshotService;

        public TransactionsController(AppDbContext context, IFinancialSnapshotService financialSnapshotService)
        {
            _context = context;
            _financialSnapshotService = financialSnapshotService;
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
            var invoiceWindow = _financialSnapshotService.GetInvoiceWindow(account, month, year);

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t =>
                    t.AccountId == accountId &&
                    t.UserId == userId &&
                    t.Date >= invoiceWindow.StartDate &&
                    t.Date <= invoiceWindow.CloseDate &&
                    !t.IsTransfer)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var total = _financialSnapshotService.CalculateInvoiceAmount(account, transactions, month, year);

            return new
            {
                period = $"{invoiceWindow.StartDate:dd/MM} a {invoiceWindow.CloseDate:dd/MM}",
                dueDate = invoiceWindow.DueDate,
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

            var installmentPlan = ResolveInstallmentPlan(request);
            if (!installmentPlan.IsValid)
            {
                return BadRequest(installmentPlan.ValidationError);
            }

            decimal valorParcela = request.Amount;
            string? installmentId = installmentPlan.RequiresGrouping ? Guid.NewGuid().ToString() : null;
            DateTime dataBase = transactionDate;

            var created = new List<Transaction>();

            for (int i = 0; i < installmentPlan.RemainingInstallments; i++)
            {
                var installmentNumber = installmentPlan.StartingInstallment + i;
                var novaTransacao = new Transaction
                {
                    UserId = userId,
                    CategoryId = request.CategoryId,
                    AccountId = request.AccountId,
                    Type = request.Type,
                    Paid = request.Paid,
                    Amount = valorParcela,
                    Description = BuildInstallmentDescription(request.Description, installmentNumber, installmentPlan.TotalInstallments),
                    Date = dataBase.AddMonths(i).ToUniversalTime(),
                    InstallmentId = installmentId
                };

                _context.Transactions.Add(novaTransacao);
                created.Add(novaTransacao);
            }

            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);

            var first = created.FirstOrDefault();
            if (first == null) return Ok();

            return CreatedAtAction("GetTransactions", new { id = first.Id }, first);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, [FromBody] UpsertTransactionDto request)
        {
            var userId = GetUserId();
            if (request.Id != 0 && request.Id != id)
                return BadRequest("Id do corpo difere da rota.");
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest("Descricao obrigatoria.");
            if (request.Amount <= 0)
                return BadRequest("Valor deve ser maior que zero.");
            if (request.AccountId <= 0)
                return BadRequest("Conta invalida.");

            var oldTransaction = await _context.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (oldTransaction == null) return NotFound();
            if (!oldTransaction.IsTransfer && request.CategoryId <= 0)
                return BadRequest("Categoria invalida.");

            if (oldTransaction.IsTransfer)
            {
                return await UpdateTransferAsync(oldTransaction, request, userId);
            }

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
                InstallmentId = oldTransaction.InstallmentId,
                IsTransfer = oldTransaction.IsTransfer,
                TransferGroupId = oldTransaction.TransferGroupId,
                RecurringRuleId = oldTransaction.RecurringRuleId
            };

            var newAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);
            if (newAccount == null)
                return BadRequest("Conta invalida.");

            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id, [FromQuery] bool deleteAll = false)
        {
            var userId = GetUserId();
            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction == null) return NotFound();
            if (transaction.IsTransfer)
            {
                return await DeleteTransferAsync(transaction, userId);
            }

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

            _context.Transactions.RemoveRange(transactionsToDelete);

            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);
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

            var fromAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.FromAccountId && a.UserId == userId);
            var toAcc = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.ToAccountId && a.UserId == userId);

            if (fromAcc == null || toAcc == null)
                return BadRequest("Contas invalidas");

            var expense = new Transaction
            {
                UserId = userId,
                AccountId = request.FromAccountId,
                Amount = request.Amount,
                Description = toAcc?.IsCreditCard == true ? "Pagamento de fatura" : "Transferencia para conta/cartao",
                Type = "Expense",
                Date = dateUtc,
                Paid = true,
                IsTransfer = true,
                TransferGroupId = Guid.NewGuid().ToString("N")
            };

            var income = new Transaction
            {
                UserId = userId,
                AccountId = request.ToAccountId,
                Amount = request.Amount,
                Description = toAcc?.IsCreditCard == true ? "Pagamento de fatura" : "Recebido de transferencia",
                Type = "Income",
                Date = dateUtc,
                Paid = true,
                IsTransfer = true,
                TransferGroupId = expense.TransferGroupId
            };

            _context.Transactions.Add(expense);
            _context.Transactions.Add(income);

            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);

            return Ok(new { message = "Transferencia/Pagamento realizado!" });
        }

        private async Task<IActionResult> UpdateTransferAsync(Transaction originalTransaction, UpsertTransactionDto request, int userId)
        {
            var transferPair = await LoadTransferPairAsync(originalTransaction, userId);
            if (transferPair == null)
            {
                return Conflict("Transferencia inconsistente. As duas pontas nao foram encontradas.");
            }

            var editedSide = transferPair.Single(t => t.Id == originalTransaction.Id);
            var otherSide = transferPair.Single(t => t.Id != originalTransaction.Id);

            if (request.Type != editedSide.Type)
            {
                return BadRequest("Nao e permitido alterar o tipo de uma transferencia.");
            }

            var updatedAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);
            var otherAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == otherSide.AccountId && a.UserId == userId);
            if (updatedAccount == null || otherAccount == null)
            {
                return BadRequest("Contas invalidas");
            }

            if (updatedAccount.Id == otherAccount.Id)
            {
                return BadRequest("Contas invalidas");
            }

            var transferDate = request.Date == default ? originalTransaction.Date : request.Date.ToUniversalTime();
            var updatedDescription = ResolveTransferDescription(updatedAccount, otherAccount, editedSide.Type);
            var otherDescription = ResolveTransferDescription(otherAccount, updatedAccount, otherSide.Type);

            editedSide.AccountId = updatedAccount.Id;
            editedSide.Amount = request.Amount;
            editedSide.Description = updatedDescription;
            editedSide.Date = transferDate;
            editedSide.Paid = true;

            otherSide.Amount = request.Amount;
            otherSide.Description = otherDescription;
            otherSide.Date = transferDate;
            otherSide.Paid = true;

            _context.Transactions.UpdateRange(editedSide, otherSide);
            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);
            return NoContent();
        }

        private async Task<IActionResult> DeleteTransferAsync(Transaction transaction, int userId)
        {
            var transferPair = await LoadTransferPairAsync(transaction, userId);
            if (transferPair == null)
            {
                return Conflict("Transferencia inconsistente. As duas pontas nao foram encontradas.");
            }

            _context.Transactions.RemoveRange(transferPair);
            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);
            return NoContent();
        }

        private async Task<List<Transaction>?> LoadTransferPairAsync(Transaction transaction, int userId)
        {
            if (!transaction.IsTransfer || string.IsNullOrWhiteSpace(transaction.TransferGroupId))
            {
                return null;
            }

            var pair = await _context.Transactions
                .Where(t => t.UserId == userId && t.TransferGroupId == transaction.TransferGroupId)
                .ToListAsync();

            if (pair.Count != 2)
            {
                return null;
            }

            var hasExpense = pair.Any(t => t.Type == "Expense");
            var hasIncome = pair.Any(t => t.Type == "Income");
            if (!hasExpense || !hasIncome)
            {
                return null;
            }

            return pair;
        }

        private static string ResolveTransferDescription(Account sourceAccount, Account destinationAccount, string type)
        {
            if (type == "Expense")
            {
                return destinationAccount.IsCreditCard ? "Pagamento de fatura" : "Transferencia para conta/cartao";
            }

            return sourceAccount.IsCreditCard ? "Pagamento de fatura" : "Recebido de transferencia";
        }

        private static InstallmentPlan ResolveInstallmentPlan(UpsertTransactionDto request)
        {
            var fallbackTotal = request.Installments > 1 ? request.Installments : 1;
            var totalInstallments = request.TotalInstallments > 1 ? request.TotalInstallments : fallbackTotal;
            var startingInstallment = request.InstallmentNumber > 1 ? request.InstallmentNumber : 1;

            if (totalInstallments < 1)
            {
                return InstallmentPlan.Invalid("Quantidade de parcelas invalida.");
            }

            if (startingInstallment < 1)
            {
                return InstallmentPlan.Invalid("Numero da parcela atual invalido.");
            }

            if (startingInstallment > totalInstallments)
            {
                return InstallmentPlan.Invalid("Parcela atual nao pode ser maior que o total.");
            }

            return InstallmentPlan.Valid(startingInstallment, totalInstallments);
        }

        private static string BuildInstallmentDescription(string description, int installmentNumber, int totalInstallments)
        {
            return totalInstallments > 1
                ? $"{description} ({installmentNumber}/{totalInstallments})"
                : description;
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
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Expense";
        public int CategoryId { get; set; }
        public int AccountId { get; set; }
        public DateTime Date { get; set; }
        public bool Paid { get; set; }
        public int Installments { get; set; } = 1;
        public int InstallmentNumber { get; set; } = 1;
        public int TotalInstallments { get; set; } = 1;
    }

    internal sealed record InstallmentPlan(bool IsValid, string? ValidationError, int StartingInstallment, int TotalInstallments)
    {
        public int RemainingInstallments => TotalInstallments - StartingInstallment + 1;
        public bool RequiresGrouping => TotalInstallments > 1;

        public static InstallmentPlan Valid(int startingInstallment, int totalInstallments) =>
            new(true, null, startingInstallment, totalInstallments);

        public static InstallmentPlan Invalid(string validationError) =>
            new(false, validationError, 1, 1);
    }
}
