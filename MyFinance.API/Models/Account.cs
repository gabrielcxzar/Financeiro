namespace MyFinance.API.Models
{
    public class Account
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal InitialBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public string Type { get; set; } = "Checking"; // Ex: Checking, Investment, Cash
    }
}