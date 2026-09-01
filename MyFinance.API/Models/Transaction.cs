using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("transactions")]
    public class Transaction
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("date")]
        public DateTime Date { get; set; }

        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Column("paid")]
        public bool Paid { get; set; }
        
        [Column("categoryid")]
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }
        
        [Column("accountid")]
        public int AccountId { get; set; }
        public Account? Account { get; set; }
        
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("is_transfer")]
        public bool IsTransfer { get; set; }

        [Column("transfer_group_id")]
        public string? TransferGroupId { get; set; }

        [Column("recurring_rule_id")]
        public int? RecurringRuleId { get; set; }

        [NotMapped]
        public int Installments { get; set; } = 1;

        [Column("installment_id")]
        public string? InstallmentId { get; set; }

        [Column("source")]
        public string? Source { get; set; }

        [Column("source_file")]
        public string? SourceFile { get; set; }

        [Column("external_id")]
        public string? ExternalId { get; set; }

        [Column("raw_memo")]
        public string? RawMemo { get; set; }

        [Column("imported_at")]
        public DateTime? ImportedAt { get; set; }

        [Column("import_batch_id")]
        public int? ImportBatchId { get; set; }

        public ImportBatch? ImportBatch { get; set; }

        [Column("exclude_from_reports")]
        public bool ExcludeFromReports { get; set; }

        [Column("reporting_kind")]
        public string ReportingKind { get; set; } = ReportingKinds.Normal;
    }

    public static class ReportingKinds
    {
        public const string Normal = "normal";
        public const string InternalTransfer = "internal_transfer";
        public const string InvoicePayment = "invoice_payment";
        public const string PassThrough = "pass_through";
        public const string TechnicalAdjustment = "technical_adjustment";

        public static readonly HashSet<string> ExcludedByDefault =
        [
            InternalTransfer,
            InvoicePayment,
            PassThrough,
            TechnicalAdjustment
        ];

        public static bool IsValid(string? value) =>
            value is Normal or InternalTransfer or InvoicePayment or PassThrough or TechnicalAdjustment;
    }
}
