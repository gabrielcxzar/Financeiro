using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("recurring_transactions")]
    public class RecurringTransaction
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("type")]
        public string Type { get; set; } = "Expense";

        [Column("day_of_month")]
        public int DayOfMonth { get; set; } // Dia 5, Dia 10...

        [Column("active")]
        public bool Active { get; set; } = true;

        [Column("category_id")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Column("account_id")]
        public int AccountId { get; set; }
        public Account? Account { get; set; }
    }
}