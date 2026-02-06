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
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAccounts()
        {
            var userId = GetUserId();
            var accounts = await _context.Accounts.Where(a => a.UserId == userId).ToListAsync();

            var result = new List<object>();

            foreach (var acc in accounts)
            {
                decimal invoiceAmount = 0;

                if (acc.IsCreditCard)
                {
                    var today = DateTime.UtcNow;
                    var closingDay = acc.ClosingDay ?? 1;

                    int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
                    int safeClosingDay = Math.Min(closingDay, daysInMonth);

                    var closeDate = new DateTime(today.Year, today.Month, safeClosingDay, 23, 59, 59, DateTimeKind.Utc);

                    if (today.Day >= safeClosingDay)
                    {
                        closeDate = closeDate.AddMonths(1);
                    }

                    var startDate = closeDate.AddMonths(-1).AddDays(1);

                    invoiceAmount = await _context.Transactions
                        .Where(t => t.AccountId == acc.Id && t.UserId == userId
                                    && t.Date >= startDate && t.Date <= closeDate)
                        .SumAsync(t => t.Type == "Expense" ? t.Amount : -t.Amount);
                }

                result.Add(new
                {
                    acc.Id,
                    acc.Name,
                    acc.InitialBalance,
                    CurrentBalance = acc.CurrentBalance,
                    InvoiceAmount = invoiceAmount,
                    acc.Type,
                    acc.IsCreditCard,
                    acc.CreditLimit,
                    acc.ClosingDay,
                    acc.DueDay
                });
            }

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

            if (account == null) return NotFound("Conta não encontrada");

            decimal diferenca = request.NewBalance - account.CurrentBalance;

            if (diferenca == 0) return Ok(new { message = "Saldo já está correto." });

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

        public class AdjustBalanceDto
        {
            public int AccountId { get; set; }
            public decimal NewBalance { get; set; }
        }
    }
}
