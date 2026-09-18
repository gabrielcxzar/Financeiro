using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models;

[Table("gmail_oauth_states")]
public class GmailOAuthState
{
    [Column("id")] public int Id { get; set; }
    [Column("state_hash")] public string StateHash { get; set; } = string.Empty;
    [Column("user_id")] public int UserId { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("used_at")] public DateTime? UsedAt { get; set; }
}
