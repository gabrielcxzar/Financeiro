using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAccounts(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);

            var creditCardAccounts = accounts.Where(a => a.IsCreditCard).ToList();
            var invoiceTotals = new Dictionary<int, decimal>();

            if (creditCardAccounts.Count > 0)
            {
                var today = DateTime.UtcNow;
                var ranges = new List<CardInvoiceRange>();

                foreach (var account in creditCardAccounts)
                {
                    var closingDay = account.ClosingDay ?? 1;
                    var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
                    var safeClosingDay = Math.Min(closingDay, daysInMonth);

                    var closeDate = new DateTime(today.Year, today.Month, safeClosingDay, 23, 59, 59, DateTimeKind.Utc);
                    if (today.Day >= safeClosingDay)
                    {
                        closeDate = closeDate.AddMonths(1);
                    }

                    var startDate = closeDate.AddMonths(-1).AddDays(1);
                    ranges.Add(new CardInvoiceRange(account.Id, startDate, closeDate));
                }

                var minStartDate = ranges.Min(r => r.StartDate);
                var maxCloseDate = ranges.Max(r => r.CloseDate);
                var cardIds = ranges.Select(r => r.AccountId).ToList();

                var transactions = await _context.Transactions
                    .AsNoTracking()
                    .Where(t =>
                        t.UserId == userId &&
                        cardIds.Contains(t.AccountId) &&
                        t.Date >= minStartDate &&
                        t.Date <= maxCloseDate)
                    .Select(t => new
                    {
                        t.AccountId,
                        t.Date,
                        t.Type,
                        t.Amount
                    })
                    .ToListAsync(cancellationToken);

                invoiceTotals = ranges.ToDictionary(
                    range => range.AccountId,
                    range => transactions
                        .Where(t => t.AccountId == range.AccountId && t.Date >= range.StartDate && t.Date <= range.CloseDate)
                        .Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount)
                );
            }

            var result = accounts.Select(acc => new
            {
                acc.Id,
                acc.Name,
                acc.InitialBalance,
                CurrentBalance = acc.CurrentBalance,
                InvoiceAmount = invoiceTotals.GetValueOrDefault(acc.Id, 0m),
                acc.Type,
                acc.IsCreditCard,
                acc.CreditLimit,
                acc.ClosingDay,
                acc.DueDay
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<Account>> PostAccount(Account account)
        {
            account.UserId = GetUserId();
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetAccounts", new { id = account.Id }, account);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccount(int id, Account account)
        {
            var userId = GetUserId();
            if (id != account.Id) return BadRequest();

            var existingAccount = await _context.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (existingAccount == null) return NotFound();

            account.UserId = userId;
            _context.Entry(account).State = EntityState.Modified;

            await _context.SaveChangesAsync();
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

            return NoContent();
        }

        [HttpPost("adjust-balance")]
        public async Task<IActionResult> AdjustBalance([FromBody] AdjustBalanceDto request)
        {
            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == userId);

            if (account == null) return NotFound("Conta nao encontrada");

            decimal diferenca = request.NewBalance - account.CurrentBalance;

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

            account.CurrentBalance = request.NewBalance;
            _context.Entry(account).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Saldo ajustado com sucesso!", newBalance = account.CurrentBalance });
        }

        private sealed record CardInvoiceRange(int AccountId, DateTime StartDate, DateTime CloseDate);

        public class AdjustBalanceDto
        {
            public int AccountId { get; set; }
            public decimal NewBalance { get; set; }
        }
    }
}
