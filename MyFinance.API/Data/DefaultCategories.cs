using MyFinance.API.Models;

namespace MyFinance.API.Data
{
    public static class DefaultCategories
    {
        public static List<Category> Create(int userId)
        {
            return new List<Category>
            {
                new Category { Name = "Alimentação", Type = "Expense", Color = "#FF6B6B", Icon = "???", UserId = userId },
                new Category { Name = "Mercado", Type = "Expense", Color = "#FFA07A", Icon = "??", UserId = userId },
                new Category { Name = "Transporte", Type = "Expense", Color = "#4ECDC4", Icon = "??", UserId = userId },
                new Category { Name = "Combustível", Type = "Expense", Color = "#45B7D1", Icon = "?", UserId = userId },
                new Category { Name = "Moradia", Type = "Expense", Color = "#95E1D3", Icon = "??", UserId = userId },
                new Category { Name = "Contas", Type = "Expense", Color = "#7FCDCD", Icon = "??", UserId = userId },
                new Category { Name = "Saúde", Type = "Expense", Color = "#A8E6CF", Icon = "??", UserId = userId },
                new Category { Name = "Farmácia", Type = "Expense", Color = "#88D4AB", Icon = "??", UserId = userId },
                new Category { Name = "Educação", Type = "Expense", Color = "#FFD93D", Icon = "??", UserId = userId },
                new Category { Name = "Lazer", Type = "Expense", Color = "#BA68C8", Icon = "??", UserId = userId },
                new Category { Name = "Compras", Type = "Expense", Color = "#FFB74D", Icon = "???", UserId = userId },
                new Category { Name = "Salário", Type = "Income", Color = "#4CAF50", Icon = "??", UserId = userId },
                new Category { Name = "Investimentos", Type = "Income", Color = "#81C784", Icon = "??", UserId = userId },
                new Category { Name = "Outros", Type = "Expense", Color = "#9E9E9E", Icon = "??", UserId = userId },
                new Category { Name = "Pagamento Fatura", Type = "Expense", Color = "#595959", Icon = "??", UserId = userId },
                new Category { Name = "Pagamento Fatura", Type = "Income", Color = "#595959", Icon = "??", UserId = userId },
                new Category { Name = "Transferência Interna", Type = "Expense", Color = "#78909C", Icon = "??", UserId = userId },
                new Category { Name = "Transferência Interna", Type = "Income", Color = "#78909C", Icon = "??", UserId = userId }
            };
        }
    }
}
