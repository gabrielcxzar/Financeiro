using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;

namespace MyFinance.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class FixedIncomeHoldingsController(AppDbContext db) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FixedIncomeHolding>>> Get(CancellationToken cancellationToken) =>
        await db.FixedIncomeHoldings.AsNoTracking().Where(x => x.UserId == UserId).OrderBy(x => x.Name).ToListAsync(cancellationToken);

    [HttpPost]
    public async Task<ActionResult<FixedIncomeHolding>> Upsert(FixedIncomeHoldingRequest request, CancellationToken cancellationToken)
    {
        if (request.KnownBalance < 0 || request.PrincipalAmount < 0) return BadRequest("Investment values cannot be negative.");
        if (request.KnownBalance.HasValue != request.BalanceAsOfDate.HasValue)
            return BadRequest("A known balance and its date must be supplied together.");
        if (request.KnownBalance is not null && request.BalanceAsOfDate == default)
            return BadRequest("A known balance requires a valid date.");

        var name = request.Name.Trim();
        var holding = await db.FixedIncomeHoldings.FirstOrDefaultAsync(
            x => x.UserId == UserId && x.Name == name, cancellationToken);
        var isNew = holding is null;
        if (isNew)
        {
            holding = new FixedIncomeHolding { UserId = UserId, Name = name, CreatedAt = DateTime.UtcNow };
            db.FixedIncomeHoldings.Add(holding);
        }

        holding!.Name = name;
        holding.Institution = Clean(request.Institution);
        holding.ProductType = request.ProductType.Trim();
        holding.Benchmark = Clean(request.Benchmark);
        holding.ContractedRate = request.ContractedRate;
        holding.ContractedRateUnit = Clean(request.ContractedRateUnit);
        holding.MaturityDate = request.MaturityDate;
        holding.Liquidity = Clean(request.Liquidity);
        holding.PrincipalAmount = request.PrincipalAmount;
        holding.KnownBalance = request.KnownBalance;
        holding.BalanceAsOfDate = request.BalanceAsOfDate;
        holding.ValuationSource = request.KnownBalance is null ? null : Clean(request.ValuationSource) ?? "manual";
        holding.Notes = Clean(request.Notes);
        holding.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return isNew ? CreatedAtAction(nameof(Get), new { id = holding.Id }, holding) : Ok(holding);
    }

    [HttpPut("{id:int}/balance")]
    public async Task<ActionResult<FixedIncomeHolding>> UpdateBalance(int id, FixedIncomeBalanceRequest request, CancellationToken cancellationToken)
    {
        if (request.KnownBalance < 0) return BadRequest("Known balance cannot be negative.");
        if (request.AsOfDate == default) return BadRequest("A valid balance date is required.");
        var holding = await db.FixedIncomeHoldings.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, cancellationToken);
        if (holding is null) return NotFound();

        holding.KnownBalance = request.KnownBalance;
        holding.BalanceAsOfDate = request.AsOfDate;
        holding.ValuationSource = Clean(request.Source) ?? "manual";
        holding.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(holding);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var holding = await db.FixedIncomeHoldings.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, cancellationToken);
        if (holding is null) return NotFound();
        db.FixedIncomeHoldings.Remove(holding);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record FixedIncomeBalanceRequest(
        [Required, Range(typeof(decimal), "0", "9999999999999999")] decimal KnownBalance,
        [Required] DateOnly AsOfDate,
        [StringLength(120)] string? Source = null);

    public sealed record FixedIncomeHoldingRequest(
        [Required, StringLength(160)] string Name,
        [StringLength(120)] string? Institution,
        [Required, StringLength(40)] string ProductType,
        [StringLength(40)] string? Benchmark,
        [Range(typeof(decimal), "0", "999999.9999")] decimal? ContractedRate,
        [StringLength(40)] string? ContractedRateUnit,
        DateOnly? MaturityDate,
        [StringLength(100)] string? Liquidity,
        [Range(typeof(decimal), "0", "9999999999999999")] decimal? PrincipalAmount,
        [Range(typeof(decimal), "0", "9999999999999999")] decimal? KnownBalance,
        DateOnly? BalanceAsOfDate,
        [StringLength(120)] string? ValuationSource,
        [StringLength(1000)] string? Notes);
}
