using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using MyFinance.API.Services;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FiiHoldingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMarketQuoteProvider _quoteProvider;

        public FiiHoldingsController(AppDbContext context, IMarketQuoteProvider quoteProvider)
        {
            _context = context;
            _quoteProvider = quoteProvider;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FiiHolding>>> GetHoldings()
        {
            var userId = GetUserId();
            return await _context.FiiHoldings
                .AsNoTracking()
                .Where(h => h.UserId == userId)
                .OrderBy(h => h.Ticker)
                .ToListAsync();
        }

        [HttpPut("{id:int}/quote")]
        public async Task<ActionResult<FiiHolding>> UpdateManualQuote(int id, ManualFiiQuoteRequest request)
        {
            if (request.Price <= 0) return BadRequest("Price must be positive.");
            if (request.AsOfDate == default) return BadRequest("Quote date is required.");
            var holding = await _context.FiiHoldings.FirstOrDefaultAsync(h => h.Id == id && h.UserId == GetUserId());
            if (holding is null) return NotFound();

            holding.CurrentPrice = request.Price;
            holding.QuoteAsOfDate = request.AsOfDate;
            holding.QuoteAsOf = null;
            holding.QuoteSource = string.IsNullOrWhiteSpace(request.Source) ? "manual" : $"manual: {request.Source.Trim()}";
            holding.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(holding);
        }

        [HttpPost("quotes/refresh")]
        public async Task<ActionResult<QuoteRefreshResponse>> RefreshQuotes(CancellationToken cancellationToken)
        {
            if (!_quoteProvider.IsConfigured) return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "quote_provider_not_configured" });

            var userId = GetUserId();
            var holdings = await _context.FiiHoldings.Where(h => h.UserId == userId).OrderBy(h => h.Ticker).ToListAsync(cancellationToken);
            var quotes = await _quoteProvider.GetQuotesAsync(holdings.Select(x => x.Ticker).ToArray(), cancellationToken);

            var items = new List<QuoteRefreshItem>();
            var changed = false;
            foreach (var holding in holdings)
            {
                var result = quotes[holding.Ticker];
                if (!result.Success || result.Quote is null)
                {
                    items.Add(new(holding.Ticker, false, result.ErrorCode, holding.CurrentPrice, holding.QuoteAsOfDate, holding.QuoteSource));
                    continue;
                }

                var quote = result.Quote;
                var quoteDate = DateOnly.FromDateTime(quote.AsOf.UtcDateTime);
                var isOlder = holding.QuoteAsOf is not null
                    ? quote.AsOf <= holding.QuoteAsOf
                    : holding.QuoteAsOfDate is not null && quoteDate <= holding.QuoteAsOfDate;
                if (isOlder)
                {
                    items.Add(new(holding.Ticker, false, "quote_not_newer_than_saved", holding.CurrentPrice, holding.QuoteAsOfDate, holding.QuoteSource));
                    continue;
                }

                holding.CurrentPrice = quote.Price;
                holding.QuoteAsOf = quote.AsOf;
                holding.QuoteAsOfDate = quoteDate;
                holding.QuoteSource = quote.Source;
                holding.UpdatedAt = DateTime.UtcNow;
                changed = true;
                items.Add(new(holding.Ticker, true, null, holding.CurrentPrice, holding.QuoteAsOfDate, holding.QuoteSource));
            }

            if (changed) await _context.SaveChangesAsync(cancellationToken);
            return Ok(new QuoteRefreshResponse(items.Count(x => x.Updated), items.Count(x => !x.Updated), items));
        }

        [HttpPost]
        public async Task<ActionResult<FiiHolding>> UpsertHolding(FiiHoldingRequest request)
        {
            var userId = GetUserId();
            var ticker = request.Ticker.Trim().ToUpperInvariant();

            var existing = await _context.FiiHoldings
                .FirstOrDefaultAsync(h => h.UserId == userId && h.Ticker == ticker);

            if (existing != null)
            {
                existing.Shares = request.Shares;
                existing.AvgPrice = request.AvgPrice;
                existing.Notes = request.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var holding = new FiiHolding
            {
                UserId = userId,
                Ticker = ticker,
                Shares = request.Shares,
                AvgPrice = request.AvgPrice,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

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

    public sealed record ManualFiiQuoteRequest(
        [Required, Range(typeof(decimal), "0.0001", "999999999999.9999")] decimal Price,
        [Required] DateOnly AsOfDate,
        [StringLength(100)] string? Source = null);

    public sealed record QuoteRefreshItem(string Ticker, bool Updated, string? ErrorCode, decimal? CurrentPrice, DateOnly? QuoteAsOfDate, string? QuoteSource);
    public sealed record QuoteRefreshResponse(int UpdatedCount, int FailedCount, IReadOnlyList<QuoteRefreshItem> Items);
    public sealed record FiiHoldingRequest(
        [Required, StringLength(20)] string Ticker,
        [Range(typeof(decimal), "0.0001", "999999999999.9999")] decimal Shares,
        [Range(typeof(decimal), "0.0001", "999999999999.9999")] decimal AvgPrice,
        [StringLength(1000)] string? Notes);
}
