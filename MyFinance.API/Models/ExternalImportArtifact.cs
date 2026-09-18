using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("external_import_artifacts")]
public class ExternalImportArtifact
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("provider")] public string Provider { get; set; } = "gmail";
    [Column("external_message_id")] public string ExternalMessageId { get; set; } = string.Empty;
    [Column("external_attachment_id")] public string ExternalAttachmentId { get; set; } = string.Empty;
    [Column("file_name")] public string FileName { get; set; } = string.Empty;
    [Column("file_hash")] public string? FileHash { get; set; }
    [Column("import_batch_id")] public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    [Column("discovered_at")] public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    [Column("imported_at")] public DateTime? ImportedAt { get; set; }
}
