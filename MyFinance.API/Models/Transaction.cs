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
        // public Account? Account { get; set; }
    }
}