using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("import_batches")]
public class ImportBatch
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("account_id")] public int? AccountId { get; set; }
    public Account? Account { get; set; }
    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("file_type")] public string FileType { get; set; } = string.Empty;
    [Column("source")] public string Source { get; set; } = string.Empty;
    [Column("file_hash")] public string FileHash { get; set; } = string.Empty;
    [Column("ledger_balance")] public decimal? LedgerBalance { get; set; }
    [Column("period_start")] public DateTime? PeriodStart { get; set; }
    [Column("period_end")] public DateTime? PeriodEnd { get; set; }
    [Column("status")] public string Status { get; set; } = ImportBatchStatuses.Pending;
    [Column("total_items")] public int TotalItems { get; set; }
    [Column("imported_count")] public int ImportedCount { get; set; }
    [Column("duplicate_count")] public int DuplicateCount { get; set; }
    [Column("review_count")] public int ReviewCount { get; set; }
    [Column("ignored_count")] public int IgnoredCount { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("confirmed_at")] public DateTime? ConfirmedAt { get; set; }
    public ICollection<ImportedStatementItem> Items { get; set; } = [];
}

public static class ImportBatchStatuses
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
}
