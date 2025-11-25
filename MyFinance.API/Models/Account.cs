using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("accounts")]
    public class Account
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("initialbalance")]
        public decimal InitialBalance { get; set; }

        [Column("currentbalance")]
        public decimal CurrentBalance { get; set; }

        [Column("type")]
        public string Type { get; set; } = "Checking"; // Checking, Investment

        // --- NOVOS CAMPOS PARA CARTÃO DE CRÉDITO ---
        [Column("is_credit_card")]
        public bool IsCreditCard { get; set; }

        [Column("closing_day")]
        public int? ClosingDay { get; set; } // Dia que a fatura fecha

        [Column("due_day")]
        public int? DueDay { get; set; } // Dia que vence

        [Column("credit_limit")]
        public decimal? CreditLimit { get; set; } // Limite total
    }
}