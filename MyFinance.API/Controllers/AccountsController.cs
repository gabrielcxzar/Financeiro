using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using MyFinance.API.Services;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFinancialSnapshotService _financialSnapshotService;

        public AccountsController(AppDbContext context, IFinancialSnapshotService financialSnapshotService)
        {
            _context = context;
            _financialSnapshotService = financialSnapshotService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAccounts(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var snapshot = await _financialSnapshotService.BuildUserSnapshotAsync(userId, DateTime.UtcNow, cancellationToken);
            var today = DateTime.UtcNow;

            var result = snapshot.Accounts.Select(acc =>
            {
                var accountSnapshot = snapshot.AccountSnapshots.First(s => s.AccountId == acc.Id);

                return new
                {
                    acc.Id,
                    acc.Name,
                    acc.InitialBalance,
                    CurrentBalance = accountSnapshot.RealBalance,
                    accountSnapshot.PendingBalance,
                    accountSnapshot.ProjectedBalance,
                    accountSnapshot.OutstandingLiability,
                    accountSnapshot.PendingLiability,
                    accountSnapshot.ProjectedLiability,
                    InvoiceAmount = acc.IsCreditCard
                        ? _financialSnapshotService.CalculateInvoiceAmount(acc, snapshot.Transactions, today.Month, today.Year)
                        : 0m,
                    acc.Type,
                    acc.IsCreditCard,
                    acc.CreditLimit,
                    acc.ClosingDay,
                    acc.DueDay
                };
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<Account>> PostAccount([FromBody] UpsertAccountDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Nome da conta e obrigatorio.");

            var account = new Account
            {
                UserId = GetUserId(),
                Name = request.Name.Trim(),
                Type = request.Type,
                IsCreditCard = request.IsCreditCard,
                InitialBalance = request.InitialBalance,
                CurrentBalance = request.InitialBalance,
                CreditLimit = request.CreditLimit,
                ClosingDay = request.ClosingDay,
                DueDay = request.DueDay
            };

            if (account.IsCreditCard)
            {
                account.Type = "Checking";
                account.InitialBalance = 0;
                account.CurrentBalance = 0;
            }
            else
            {
                account.CreditLimit = null;
                account.ClosingDay = null;
                account.DueDay = null;
            }

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(account.UserId);
            return CreatedAtAction("GetAccounts", new { id = account.Id }, account);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccount(int id, [FromBody] UpsertAccountDto request)
        {
            var userId = GetUserId();

            var existingAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (existingAccount == null) return NotFound();

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Nome da conta e obrigatorio.");

            existingAccount.Name = request.Name.Trim();
            existingAccount.Type = request.IsCreditCard ? "Checking" : request.Type;
            existingAccount.IsCreditCard = request.IsCreditCard;

            if (request.IsCreditCard)
            {
                existingAccount.CreditLimit = request.CreditLimit;
                existingAccount.ClosingDay = request.ClosingDay;
                existingAccount.DueDay = request.DueDay;
            }
            else
            {
                existingAccount.CreditLimit = null;
                existingAccount.ClosingDay = null;
                existingAccount.DueDay = null;
            }

            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var userId = GetUserId();
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return NotFound();

            var transactions = _context.Transactions.Where(t => t.AccountId == id && t.UserId == userId);
            _context.Transactions.RemoveRange(transactions);

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);

            return NoContent();
        }

        [HttpPost("adjust-balance")]
        public async Task<IActionResult> AdjustBalance([FromBody] AdjustBalanceDto request)
        {
            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);

            if (account == null) return NotFound("Conta nao encontrada");

            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);
            var snapshot = await _financialSnapshotService.BuildUserSnapshotAsync(userId, DateTime.UtcNow);
            var accountSnapshot = snapshot.AccountSnapshots.First(s => s.AccountId == account.Id);
            decimal diferenca = request.NewBalance - accountSnapshot.RealBalance;

            if (diferenca == 0) return Ok(new { message = "Saldo ja esta correto." });

            var transaction = new Transaction
            {
                UserId = userId,
                AccountId = account.Id,
                Amount = Math.Abs(diferenca),
                Description = "Ajuste Manual de Saldo",
                Date = DateTime.UtcNow,
                Paid = true,
                Type = diferenca > 0 ? "Income" : "Expense",
                CategoryId = null
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);

            return Ok(new { message = "Saldo ajustado com sucesso!", newBalance = request.NewBalance });
        }

        public class AdjustBalanceDto
        {
            public int AccountId { get; set; }
            public decimal NewBalance { get; set; }
        }

        public class UpsertAccountDto
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = "Checking";
            public decimal InitialBalance { get; set; }
            public bool IsCreditCard { get; set; }
            public decimal? CreditLimit { get; set; }
            public int? ClosingDay { get; set; }
            public int? DueDay { get; set; }
        }
    }
}
