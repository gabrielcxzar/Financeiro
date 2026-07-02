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
    public class BudgetsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BudgetsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Budget>>> GetBudgets([FromQuery] int? month = null, [FromQuery] int? year = null)
        {
            var userId = GetUserId();
            var resolvedMonth = month ?? DateTime.UtcNow.Month;
            var resolvedYear = year ?? DateTime.UtcNow.Year;

            return await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId && b.Month == resolvedMonth && b.Year == resolvedYear)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Budget>> PostBudget(UpsertBudgetDto request)
        {
            var userId = GetUserId();
            if (request.CategoryId <= 0)
                return BadRequest("Categoria invalida.");
            if (request.Amount <= 0)
                return BadRequest("Valor da meta deve ser maior que zero.");
            if (request.Month is < 1 or > 12)
                return BadRequest("Mes invalido.");
            if (request.Year is < 2000 or > 2100)
                return BadRequest("Ano invalido.");

            var categoryExists = await _context.Categories
                .AnyAsync(c => c.Id == request.CategoryId && c.UserId == userId);
            if (!categoryExists)
                return BadRequest("Categoria nao encontrada para este usuario.");

            var existing = await _context.Budgets
                .FirstOrDefaultAsync(b =>
                    b.CategoryId == request.CategoryId &&
                    b.UserId == userId &&
                    b.Month == request.Month &&
                    b.Year == request.Year);

            if (existing != null)
            {
                existing.Amount = request.Amount;
                existing.AllowRollover = request.AllowRollover;
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var budget = new Budget
            {
                Amount = request.Amount,
                CategoryId = request.CategoryId,
                Month = request.Month,
                Year = request.Year,
                AllowRollover = request.AllowRollover,
                UserId = userId
            };

            _context.Budgets.Add(budget);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetBudgets", new { id = budget.Id }, budget);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var userId = GetUserId();
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (budget == null) return NotFound();

            _context.Budgets.Remove(budget);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        public sealed class UpsertBudgetDto
        {
            public decimal Amount { get; set; }
            public int CategoryId { get; set; }
            public int Month { get; set; } = DateTime.UtcNow.Month;
            public int Year { get; set; } = DateTime.UtcNow.Year;
            public bool AllowRollover { get; set; }
        }
    }
}
