using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("financial_goals")]
    public class FinancialGoal
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("goal_type")]
        public string GoalType { get; set; } = "Saving";

        [Column("target_amount")]
        public decimal TargetAmount { get; set; }

        [Column("current_amount")]
        public decimal CurrentAmount { get; set; }

        [Column("target_date")]
        public DateTime? TargetDate { get; set; }

        [Column("monthly_contribution")]
        public decimal MonthlyContribution { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Active";

        [Column("linked_account_id")]
        public int? LinkedAccountId { get; set; }
        public Account? LinkedAccount { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }
    }
}
