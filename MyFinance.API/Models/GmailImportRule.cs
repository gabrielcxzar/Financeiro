using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("gmail_import_rules")]
public class GmailImportRule
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("name")] public string Name { get; set; } = string.Empty;
    [Column("search_query")] public string SearchQuery { get; set; } = string.Empty;
    [Column("target_account_id")] public int TargetAccountId { get; set; }
    public Account? TargetAccount { get; set; }
    [Column("enabled")] public bool Enabled { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
