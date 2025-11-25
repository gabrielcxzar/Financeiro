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

        [Column("user_id")]
        public int UserId { get; set; }
    }
}