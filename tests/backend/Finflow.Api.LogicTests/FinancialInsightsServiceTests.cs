using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Mcp;
using MyFinance.API.Models;
using MyFinance.API.Services;
using ModelContextProtocol.Server;

namespace Finflow.Api.LogicTests;

[Collection("Finflow PostgreSQL")]
public sealed class FinancialInsightsServiceTests
{
    static FinancialInsightsServiceTests() =>
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    [PostgresFact]
    public async Task Summary_IsolatesUserAndReturnsNullSavingsRateWhenIncomeIsZero()
    {
        await using var db = CreateDb();
        db.Accounts.AddRange(new Account { Id = 1, UserId = 1, Name = "A", InitialBalance = 0 }, new Account { Id = 2, UserId = 2, Name = "B", InitialBalance = 0 });
        db.Transactions.AddRange(
            new Transaction { UserId = 1, AccountId = 1, Date = new DateTime(2026, 1, 10), Amount = 100, Type = "Expense", ReportingKind = ReportingKinds.Normal },
            new Transaction { UserId = 2, AccountId = 2, Date = new DateTime(2026, 1, 10), Amount = 999, Type = "Income", ReportingKind = ReportingKinds.Normal });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.GetSummaryAsync(1, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), false, 0, default);

        Assert.Equal(100m, result.Data.Expense);
        Assert.Equal(0m, result.Data.Income);
        Assert.Null(result.Data.SavingsRate);
    }

    [PostgresFact]
    public async Task Transactions_RejectTamperedCursor()
    {
        await using var db = CreateDb();
        db.Accounts.Add(new Account { Id = 1, UserId = 1, Name = "A", InitialBalance = 0 });
        for (var i = 0; i < 3; i++) db.Transactions.Add(new Transaction { UserId = 1, AccountId = 1, Date = new DateTime(2026, 1, i + 1), Amount = i + 1, Type = "Expense", ReportingKind = ReportingKinds.Normal });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var first = await service.GetTransactionsAsync(1, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), null, null, "all", "all", null, null, false, 2, null, default);

        Assert.NotNull(first.Data.NextCursor);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTransactionsAsync(1, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), null, null, "all", "all", null, null, false, 2, first.Data.NextCursor![..^2] + "xx", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetTransactionsAsync(1, new DateTime(2026, 1, 1), new DateTime(2026, 2, 1), null, null, "income", "all", null, null, false, 2, first.Data.NextCursor, default));
    }

    [Fact]
    public void McpToolSurface_ContainsExactlyEightReadOnlyStructuredTools()
    {
        var expected = new[] { "get_financial_summary", "compare_periods", "get_spending_by_category", "get_transactions", "get_account_balances", "get_recurring_expenses", "get_financial_goals", "get_investment_positions" };
        var methods = typeof(FinflowMcpTools).GetMethods().Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0).ToArray();
        Assert.Equal(expected.Order(), methods.Select(m => ((McpServerToolAttribute)m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single()).Name).Order());
        Assert.All(methods, method =>
        {
            var attribute = (McpServerToolAttribute)method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Single();
            Assert.True(attribute.ReadOnly);
            Assert.False(attribute.Destructive);
            Assert.True(attribute.Idempotent);
            Assert.False(attribute.OpenWorld);
            Assert.True(attribute.UseStructuredContent);
        });
    }

    [PostgresFact]
    public async Task AllInsights_IsolateUsersAndDoNotMutateFinancialState()
    {
        await using var db = CreateDb();
        db.Accounts.AddRange(new Account { UserId = 1, Name = "A", InitialBalance = 1000 }, new Account { UserId = 2, Name = "B", InitialBalance = 2000 });
        db.Categories.AddRange(new Category { UserId = 1, Name = "A category", Type = "Expense" }, new Category { UserId = 2, Name = "B category", Type = "Expense" });
        await db.SaveChangesAsync();
        var a = db.Accounts.Single(x => x.UserId == 1);
        var ac = db.Categories.Single(x => x.UserId == 1);
        db.Transactions.Add(new Transaction { UserId = 1, AccountId = a.Id, CategoryId = ac.Id, Date = new DateTime(2026, 2, 1), Amount = 25, Type = "Expense", Paid = true, Description = "A", ReportingKind = ReportingKinds.Normal });
        db.RecurringTransactions.Add(new RecurringTransaction { UserId = 1, Description = "A recurring", Amount = 30, Type = "Expense", DayOfMonth = 5, Active = true, AccountId = a.Id, CategoryId = ac.Id });
        db.FinancialGoals.Add(new FinancialGoal { UserId = 1, Name = "A goal", TargetAmount = 100, CurrentAmount = 10, Status = "Active" });
        db.FiiHoldings.Add(new FiiHolding { UserId = 1, Ticker = "A11", Shares = 1, AvgPrice = 10 });
        await db.SaveChangesAsync();
        var before = await Fingerprint(db);
        var service = CreateService(db);
        var period = (new DateTime(2026, 2, 1), new DateTime(2026, 3, 1));
        Assert.Equal(1, (await service.GetSummaryAsync(1, period.Item1, period.Item2, false, 0, default)).Data.TransactionCount);
        Assert.Equal("A category", (await service.GetSpendingByCategoryAsync(1, period.Item1, period.Item2, 10, true, default)).Data.Categories.Single().Category);
        Assert.Equal("A", (await service.GetTransactionsAsync(1, period.Item1, period.Item2, null, null, "all", "all", null, null, false, 50, null, default)).Data.Items.Single().Description);
        Assert.Equal("A recurring", (await service.GetRecurringExpensesAsync(1, null, null, default)).Data.Items.Single().Description);
        Assert.Equal("A goal", (await service.GetFinancialGoalsAsync(1, "active", default)).Data.Items.Single().Name);
        Assert.Equal("A11", (await service.GetInvestmentPositionsAsync(1, true, true, default)).Data.FiiHoldings.Single().Ticker);
        Assert.Empty((await service.GetSummaryAsync(2, period.Item1, period.Item2, false, 0, default)).Data.TransactionCount == 0 ? Array.Empty<int>() : new[] { 1 });
        Assert.DoesNotContain("B", (await service.GetAccountBalancesAsync(1, true, true, default)).Data.CashAccounts.Select(x => x.Name));
        Assert.Equal(before, await Fingerprint(db));
    }

    private static FinancialInsightsService CreateService(AppDbContext db) => new(db, DataProtectionProvider.Create("FinflowTests"), new FinancialSnapshotService(db));

    private static AppDbContext CreateDb()
    {
        if (Environment.GetEnvironmentVariable("FINFLOW_POSTGRES_TEST_ISOLATED") != "1")
            throw new InvalidOperationException("As provas PostgreSQL destrutivas exigem FINFLOW_POSTGRES_TEST_ISOLATED=1.");

        var connection = Environment.GetEnvironmentVariable("FINFLOW_POSTGRES_TEST_URL")!;
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<string> Fingerprint(AppDbContext db) => $"{await db.Accounts.CountAsync()}|{await db.Transactions.CountAsync()}|{await db.RecurringTransactions.CountAsync()}|{await db.FinancialGoals.CountAsync()}|{await db.FiiHoldings.CountAsync()}|{await db.Transactions.SumAsync(x => (decimal?)x.Amount) ?? 0m}";
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FINFLOW_POSTGRES_TEST_URL")) ||
            Environment.GetEnvironmentVariable("FINFLOW_POSTGRES_TEST_ISOLATED") != "1")
            Skip = "Defina FINFLOW_POSTGRES_TEST_URL e FINFLOW_POSTGRES_TEST_ISOLATED=1 para usar apenas um PostgreSQL isolado.";
    }
}

[CollectionDefinition("Finflow PostgreSQL", DisableParallelization = true)]
public sealed class FinflowPostgresCollection { }
