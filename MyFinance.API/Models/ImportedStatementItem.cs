using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("imported_statement_items")]
public class ImportedStatementItem
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("import_batch_id")] public int ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    [Column("account_id")] public int AccountId { get; set; }
    public Account? Account { get; set; }
    [Column("source") ] public string Source { get; set; } = string.Empty;
    [Column("external_id")] public string? ExternalId { get; set; }
    [Column("posted_at")] public DateTime PostedAt { get; set; }
    [Column("signed_amount")] public decimal SignedAmount { get; set; }
    [Column("memo")] public string Memo { get; set; } = string.Empty;
    [Column("raw_data")] public string? RawData { get; set; }
    [Column("status")] public string Status { get; set; } = ImportItemStatuses.NeedsReview;
    [Column("reason")] public string? Reason { get; set; }
    [Column("transaction_id")] public int? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    [Column("category_id")] public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    [Column("recurring_rule_id")] public int? RecurringRuleId { get; set; }
    [Column("target_account_id")] public int? TargetAccountId { get; set; }
    [Column("reporting_kind")] public string ReportingKind { get; set; } = ReportingKinds.Normal;
    [Column("resolved_description")] public string? ResolvedDescription { get; set; }
    [Column("resolved_date")] public DateTime? ResolvedDate { get; set; }
    [Column("resolved_amount")] public decimal? ResolvedAmount { get; set; }
    [Column("resolved_type")] public string? ResolvedType { get; set; }
    [Column("installment_number")] public int? InstallmentNumber { get; set; }
    [Column("total_installments")] public int? TotalInstallments { get; set; }
    [Column("resolved_at")] public DateTime? ResolvedAt { get; set; }
}

public static class ImportItemStatuses
{
    public const string Ready = "imported";
    public const string Duplicate = "probable_duplicate";
    public const string NeedsReview = "needs_review";
    public const string Ignored = "ignored";
    public const string Committed = "committed";
}
