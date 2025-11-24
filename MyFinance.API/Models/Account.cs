using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("accounts")] // <--- Nome da tabela no banco (minúsculo)
    public class Account
    {
        [Column("id")] // <--- Nome da coluna no banco
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("initialbalance")]
        public decimal InitialBalance { get; set; }

        [Column("currentbalance")]
        public decimal CurrentBalance { get; set; }

        [Column("type")]
        public string Type { get; set; } = "Checking";
    }
}