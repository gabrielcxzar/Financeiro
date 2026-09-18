using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("gmail_integrations")]
public class GmailIntegration
{
    [Column("id")] public int Id { get; set; }
    [Column("user_id")] public int UserId { get; set; }
    [Column("google_email")] public string? GoogleEmail { get; set; }
    [Column("encrypted_refresh_token")] public string EncryptedRefreshToken { get; set; } = string.Empty;
    [Column("connected_at")] public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    [Column("last_sync_at")] public DateTime? LastSyncAt { get; set; }
    [Column("last_successful_sync_at")] public DateTime? LastSuccessfulSyncAt { get; set; }
    [Column("last_error_code")] public string? LastErrorCode { get; set; }
    [Column("enabled")] public bool Enabled { get; set; } = true;
    [Column("default_account_id")] public int? DefaultAccountId { get; set; }
    public Account? DefaultAccount { get; set; }
    [Column("search_query")] public string SearchQuery { get; set; } = "has:attachment filename:ofx newer_than:90d";
}
