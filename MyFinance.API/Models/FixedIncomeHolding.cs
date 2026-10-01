using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("fixed_income_holdings")]
public sealed class FixedIncomeHolding
{
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("institution")]
    public string? Institution { get; set; }

    [Column("product_type")]
    public string ProductType { get; set; } = string.Empty;

    [Column("benchmark")]
    public string? Benchmark { get; set; }

    [Column("contracted_rate")]
    public decimal? ContractedRate { get; set; }

    [Column("contracted_rate_unit")]
    public string? ContractedRateUnit { get; set; }

    [Column("maturity_date")]
    public DateOnly? MaturityDate { get; set; }

    [Column("liquidity")]
    public string? Liquidity { get; set; }

    [Column("principal_amount")]
    public decimal? PrincipalAmount { get; set; }

    [Column("known_balance")]
    public decimal? KnownBalance { get; set; }

    [Column("balance_as_of_date")]
    public DateOnly? BalanceAsOfDate { get; set; }

    [Column("valuation_source")]
    public string? ValuationSource { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
