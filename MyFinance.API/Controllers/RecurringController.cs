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
    public class RecurringController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFinancialSnapshotService _financialSnapshotService;

        public RecurringController(AppDbContext context, IFinancialSnapshotService financialSnapshotService)
        {
            _context = context;
            _financialSnapshotService = financialSnapshotService;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecurringTransaction>>> GetRecurrings(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            return await _context.RecurringTransactions
                .AsNoTracking()
                .Include(r => r.Category)
                .Include(r => r.Account)
                .Where(r => r.Active && r.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        [HttpPost]
        public async Task<ActionResult<RecurringTransaction>> PostRecurring(RecurringTransaction recurring)
        {
            if (!recurring.AccountId.HasValue)
                return BadRequest("Conta ou cartao obrigatorio.");

            var userId = GetUserId();
            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == recurring.AccountId.Value && a.UserId == userId);

            if (account == null)
                return BadRequest("Conta ou cartao invalido.");

            if (account.IsCreditCard && recurring.Type != "Expense")
                return BadRequest("Recorrencias de cartao devem ser despesas.");

            recurring.UserId = userId;
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
                var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
                if (account == null)
                    continue;

                if (account.IsCreditCard && rule.Type != "Expense")
                    continue;

                int daysInMonth = DateTime.DaysInMonth(year, month);
                int day = Math.Min(rule.DayOfMonth, daysInMonth);

                var targetDate = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

                bool exists = await _context.Transactions.AnyAsync(t =>
                    t.UserId == userId &&
                    t.Date.Month == month &&
                    t.Date.Year == year &&
                    (t.RecurringRuleId == rule.Id ||
                     (t.Description == rule.Description &&
                      t.Amount == rule.Amount &&
                      t.Type == rule.Type &&
                      t.AccountId == accountId &&
                      t.CategoryId == rule.CategoryId))
                );

                if (exists)
                    continue;

                var newTrans = new Transaction
                {
                    UserId = userId,
                    Description = rule.Description,
                    Amount = rule.Amount,
                    Type = rule.Type,
                    CategoryId = rule.CategoryId,
                    AccountId = accountId,
                    Date = targetDate,
                    Paid = false,
                    RecurringRuleId = rule.Id
                };

                _context.Transactions.Add(newTrans);
                count++;
            }

            await _context.SaveChangesAsync();
            await _financialSnapshotService.RecalculateAccountBalancesAsync(userId);
            return Ok(new { message = $"{count} transacoes geradas." });
        }

        [HttpGet("projection")]
        public async Task<IActionResult> GetProjection(
            [FromQuery] int months = 6,
            [FromQuery] int? startMonth = null,
            [FromQuery] int? startYear = null,
            CancellationToken cancellationToken = default)
        {
            if (months < 1) months = 1;
            if (months > 36) months = 36;

            var userId = GetUserId();
            var snapshot = await _financialSnapshotService.BuildUserSnapshotAsync(userId, DateTime.UtcNow, cancellationToken);
            var baseDate = DateTime.UtcNow;
            var month = startMonth ?? baseDate.Month;
            var year = startYear ?? baseDate.Year;
            var projection = _financialSnapshotService.BuildProjection(
                snapshot.Accounts,
                snapshot.Transactions,
                snapshot.RecurringRules,
                DateTime.UtcNow,
                month,
                year,
                months);

            return Ok(new
            {
                startBalance = projection.StartBalance,
                items = projection.Items.Select(item => new ProjectionItemDto(
                    item.Year,
                    item.Month,
                    item.Income,
                    item.Expense,
                    item.TransferImpact,
                    item.Net,
                    item.ProjectedBalance))
            });
        }

        public record ProjectionItemDto(
            int Year,
            int Month,
            decimal Income,
            decimal Expense,
            decimal TransferImpact,
            decimal Net,
            decimal ProjectedBalance
        );
    }
}
