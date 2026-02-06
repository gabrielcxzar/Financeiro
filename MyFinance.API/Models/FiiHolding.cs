using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("fii_holdings")]
    public class FiiHolding
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("ticker")]
        public string Ticker { get; set; } = string.Empty;

        [Column("shares")]
        public decimal Shares { get; set; }

        [Column("avg_price")]
        public decimal AvgPrice { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("user_id")]
        public int UserId { get; set; }
    }
}
