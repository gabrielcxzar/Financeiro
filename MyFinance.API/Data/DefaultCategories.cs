using MyFinance.API.Models;

namespace MyFinance.API.Data
{
    public static class DefaultCategories
    {
        public static List<Category> Create(int userId)
        {
            return new List<Category>
            {
                new Category { Name = "Alimentacao", Type = "Expense", Color = "#FF6B6B", Icon = "food", UserId = userId },
                new Category { Name = "Mercado", Type = "Expense", Color = "#FFA07A", Icon = "shopping", UserId = userId },
                new Category { Name = "Transporte", Type = "Expense", Color = "#4ECDC4", Icon = "transport", UserId = userId },
                new Category { Name = "Combustivel", Type = "Expense", Color = "#45B7D1", Icon = "fuel", UserId = userId },
                new Category { Name = "Moradia", Type = "Expense", Color = "#95E1D3", Icon = "home", UserId = userId },
                new Category { Name = "Contas", Type = "Expense", Color = "#7FCDCD", Icon = "bills", UserId = userId },
                new Category { Name = "Saude", Type = "Expense", Color = "#A8E6CF", Icon = "health", UserId = userId },
                new Category { Name = "Farmacia", Type = "Expense", Color = "#88D4AB", Icon = "pharmacy", UserId = userId },
                new Category { Name = "Educacao", Type = "Expense", Color = "#FFD93D", Icon = "education", UserId = userId },
                new Category { Name = "Lazer", Type = "Expense", Color = "#BA68C8", Icon = "leisure", UserId = userId },
                new Category { Name = "Compras", Type = "Expense", Color = "#FFB74D", Icon = "bag", UserId = userId },
                new Category { Name = "Salario", Type = "Income", Color = "#4CAF50", Icon = "salary", UserId = userId },
                new Category { Name = "Investimentos", Type = "Income", Color = "#81C784", Icon = "investments", UserId = userId },
                new Category { Name = "Outros", Type = "Expense", Color = "#9E9E9E", Icon = "other", UserId = userId },
                new Category { Name = "Pagamento Fatura", Type = "Expense", Color = "#595959", Icon = "invoice", UserId = userId },
                new Category { Name = "Pagamento Fatura", Type = "Income", Color = "#595959", Icon = "invoice", UserId = userId },
                new Category { Name = "Transferencia Interna", Type = "Expense", Color = "#78909C", Icon = "transfer", UserId = userId },
                new Category { Name = "Transferencia Interna", Type = "Income", Color = "#78909C", Icon = "transfer", UserId = userId }
            };
        }
    }
}
