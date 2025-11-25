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
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            var userId = GetUserId();
            return await _context.Categories
                .Where(c => c.UserId == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Category>> PostCategory(Category category)
        {
            category.UserId = GetUserId();
            if (string.IsNullOrEmpty(category.Color)) category.Color = "#8c8c8c";
            
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetCategories", new { id = category.Id }, category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var userId = GetUserId();
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (category == null) return NotFound();

            bool hasTransactions = await _context.Transactions.AnyAsync(t => t.CategoryId == id);
            if (hasTransactions) return BadRequest("Categoria em uso.");

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}