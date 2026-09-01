using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("categorization_rules")]
public class CategorizationRule
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("text_pattern")] public string TextPattern { get; set; } = string.Empty;
    [Column("value_operator")] public string? ValueOperator { get; set; }
    [Column("value_threshold")] public decimal? ValueThreshold { get; set; }
    [Column("type")] public string Type { get; set; } = "Expense";
    [Column("category_id")] public int CategoryId { get; set; }
    public Category? Category { get; set; }
    [Column("account_id")] public int? AccountId { get; set; }
    [Column("priority")] public int Priority { get; set; }
    [Column("confidence")] public decimal Confidence { get; set; } = 1m;
    [Column("active")] public bool Active { get; set; } = true;
}
