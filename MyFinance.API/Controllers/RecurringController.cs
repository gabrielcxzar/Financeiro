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
                int daysInMonth = DateTime.DaysInMonth(year, month);
                int day = Math.Min(rule.DayOfMonth, daysInMonth);
                
                var targetDate = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

                bool exists = await _context.Transactions.AnyAsync(t => 
                    t.UserId == userId &&
                    t.Description == rule.Description && 
                    t.Amount == rule.Amount &&
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
                        AccountId = rule.AccountId,
                        Date = targetDate,
                        Paid = false
                    };

                    _context.Transactions.Add(newTrans);
                    count++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"{count} transações geradas." });
        }
    }
}