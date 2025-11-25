using System.ComponentModel.DataAnnotations.Schema;

namespace MyFinance.API.Models
{
    [Table("users")]
    public class User
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("name")]
        public string Name { get; set; } = string.Empty;
        [Column("email")]
        public string Email { get; set; } = string.Empty;
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;
    }
}