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
    public class FiiHoldingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FiiHoldingsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FiiHolding>>> GetHoldings()
        {
            var userId = GetUserId();
            return await _context.FiiHoldings
                .Where(h => h.UserId == userId)
                .OrderBy(h => h.Ticker)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<FiiHolding>> UpsertHolding(FiiHolding holding)
        {
            var userId = GetUserId();
            var ticker = holding.Ticker.Trim().ToUpperInvariant();

            var existing = await _context.FiiHoldings
                .FirstOrDefaultAsync(h => h.UserId == userId && h.Ticker == ticker);

            if (existing != null)
            {
                existing.Shares = holding.Shares;
                existing.AvgPrice = holding.AvgPrice;
                existing.Notes = holding.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            holding.UserId = userId;
            holding.Ticker = ticker;
            holding.CreatedAt = DateTime.UtcNow;
            holding.UpdatedAt = DateTime.UtcNow;

            _context.FiiHoldings.Add(holding);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetHoldings), new { id = holding.Id }, holding);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHolding(int id)
        {
            var userId = GetUserId();
            var holding = await _context.FiiHoldings
                .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

            if (holding == null) return NotFound();

            _context.FiiHoldings.Remove(holding);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
