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
    public class RecurringController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RecurringController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecurringTransaction>>> GetRecurrings()
        {
            var userId = GetUserId();
            return await _context.RecurringTransactions
                .Include(r => r.Category)
                .Include(r => r.Account)
                .Where(r => r.Active && r.UserId == userId)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<RecurringTransaction>> PostRecurring(RecurringTransaction recurring)
        {
            if (!recurring.AccountId.HasValue)
                return BadRequest("Conta obrigatória.");

            recurring.UserId = GetUserId();
            _context.RecurringTransactions.Add(recurring);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetRecurrings", new { id = recurring.Id }, recurring);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecurring(int id)
        {
            var userId = GetUserId();
            var item = await _context.RecurringTransactions
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (item == null) return NotFound();

            _context.RecurringTransactions.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateTransactions([FromQuery] int month, [FromQuery] int year)
        {
            var userId = GetUserId();
            var rules = await _context.RecurringTransactions
                .Where(r => r.Active && r.UserId == userId)
                .ToListAsync();

            int count = 0;

            foreach (var rule in rules)
            {
                if (!rule.AccountId.HasValue)
                    continue;

                var accountId = rule.AccountId.Value;
                int daysInMonth = DateTime.DaysInMonth(year, month);
                int day = Math.Min(rule.DayOfMonth, daysInMonth);

                var targetDate = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

                bool exists = await _context.Transactions.AnyAsync(t =>
                    t.UserId == userId &&
                    t.Description == rule.Description &&
                    t.Amount == rule.Amount &&
                    t.Type == rule.Type &&
                    t.AccountId == accountId &&
                    t.CategoryId == rule.CategoryId &&
                    t.Date.Month == month &&
                    t.Date.Year == year
                );

                if (!exists)
                {
                    var newTrans = new Transaction
                    {
                        UserId = userId,
                        Description = rule.Description,
                        Amount = rule.Amount,
                        Type = rule.Type,
                        CategoryId = rule.CategoryId,
                        AccountId = accountId,
                        Date = targetDate,
                        Paid = false
                    };

                    _context.Transactions.Add(newTrans);
                    count++;

                    var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
                    if (account != null)
                    {
                        if (newTrans.Type == "Income") account.CurrentBalance += newTrans.Amount;
                        else account.CurrentBalance -= newTrans.Amount;

                        _context.Entry(account).State = EntityState.Modified;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"{count} transações geradas." });
        }

        [HttpGet("projection")]
        public async Task<IActionResult> GetProjection([FromQuery] int months = 6, [FromQuery] int? startMonth = null, [FromQuery] int? startYear = null)
        {
            if (months < 1) months = 1;
            if (months > 36) months = 36;

            var userId = GetUserId();
            var rules = await _context.RecurringTransactions
                .Where(r => r.Active && r.UserId == userId)
                .ToListAsync();

            var accounts = await _context.Accounts
                .Where(a => a.UserId == userId && !a.IsCreditCard)
                .ToListAsync();

            var startingBalance = accounts.Sum(a => a.CurrentBalance);

            var baseDate = DateTime.Today;
            var month = startMonth ?? baseDate.Month;
            var year = startYear ?? baseDate.Year;
            var start = new DateTime(year, month, 1);

            var monthlyIncome = rules.Where(r => r.Type == "Income").Sum(r => r.Amount);
            var monthlyExpense = rules.Where(r => r.Type == "Expense").Sum(r => r.Amount);

            var result = new List<ProjectionItemDto>();
            var runningBalance = startingBalance;

            for (int i = 0; i < months; i++)
            {
                var date = start.AddMonths(i);
                var net = monthlyIncome - monthlyExpense;
                runningBalance += net;

                result.Add(new ProjectionItemDto(
                    date.Year,
                    date.Month,
                    monthlyIncome,
                    monthlyExpense,
                    net,
                    runningBalance
                ));
            }

            return Ok(new { startBalance = startingBalance, items = result });
        }

        public record ProjectionItemDto(
            int Year,
            int Month,
            decimal Income,
            decimal Expense,
            decimal Net,
            decimal ProjectedBalance
        );
    }
}
