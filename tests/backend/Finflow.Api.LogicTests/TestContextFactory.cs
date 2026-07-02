namespace Finflow.Api.LogicTests;

internal static class TestContextFactory
{
    public static (AppDbContext Db, IFinancialSnapshotService Finance) Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var db = new AppDbContext(options);
        var finance = new FinancialSnapshotService(db);
        return (db, finance);
    }

    public static void AttachUser(ControllerBase controller, int userId = 1)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString())
                ], "TestAuth"))
            }
        };
    }

    public static async Task<(Account Bank, Account Card, Category ExpenseCategory, Category IncomeCategory)> SeedFinanceBaseAsync(AppDbContext db, int userId = 1)
    {
        var bank = new Account
        {
            UserId = userId,
            Name = "Conta Banco",
            Type = "Checking",
            InitialBalance = 1000m,
            CurrentBalance = 1000m
        };

        var card = new Account
        {
            UserId = userId,
            Name = "Cartao Principal",
            Type = "Checking",
            IsCreditCard = true,
            InitialBalance = 0m,
            CurrentBalance = 0m,
            ClosingDay = 25,
            DueDay = 5,
            CreditLimit = 5000m
        };

        var expenseCategory = new Category
        {
            UserId = userId,
            Name = "Despesa Teste",
            Type = "Expense",
            Icon = "EX",
            Color = "#111111"
        };

        var incomeCategory = new Category
        {
            UserId = userId,
            Name = "Receita Teste",
            Type = "Income",
            Icon = "IN",
            Color = "#222222"
        };

        db.Accounts.AddRange(bank, card);
        db.Categories.AddRange(expenseCategory, incomeCategory);
        await db.SaveChangesAsync();

        return (bank, card, expenseCategory, incomeCategory);
    }

    public static JsonElement ToJsonElement(object value)
    {
        return JsonSerializer.SerializeToElement(value);
    }
}
