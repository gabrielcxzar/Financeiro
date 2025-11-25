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
        public async Task<ActionResult<IEnumerable<Budget>>> GetBudgets()
        {
            var userId = GetUserId();
            return await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Budget>> PostBudget(Budget budget)
        {
            var userId = GetUserId();
            budget.UserId = userId;

            // Verifica se já existe meta para essa categoria (evita duplicata)
            var existing = await _context.Budgets
                .FirstOrDefaultAsync(b => b.CategoryId == budget.CategoryId && b.UserId == userId);

            if (existing != null)
            {
                // Se já existe, atualiza o valor
                existing.Amount = budget.Amount;
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

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
    }
}