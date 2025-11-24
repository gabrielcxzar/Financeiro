public class Transaction
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty; // Income, Expense
    public bool Paid { get; set; }
    
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    
    public int AccountId { get; set; }
    // Adicione a prop de navegação Account se criar o Model Account
}