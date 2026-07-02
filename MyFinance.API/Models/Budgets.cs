using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("budgets")]
    public class Budget
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Column("month")]
        public int Month { get; set; }

        [Column("year")]
        public int Year { get; set; }

        [Column("allow_rollover")]
        public bool AllowRollover { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }
    }
}
