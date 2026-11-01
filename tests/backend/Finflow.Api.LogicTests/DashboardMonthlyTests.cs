namespace Finflow.Api.LogicTests;

public class DashboardMonthlyTests
{
    [Fact]
    public async Task SharedExpense_UsesPersonalShareInReports_AndGrossAmountInCashBalance()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, category, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new DashboardSummaryController(db, finance, NullLogger<DashboardSummaryController>.Instance);
        TestContextFactory.AttachUser(controller);
        db.Transactions.Add(new Transaction { UserId = 1, AccountId = bank.Id, CategoryId = category.Id,
            Description = "Despesa compartilhada", Amount = 538.80m, ReportingAmount = 108.80m,
            Type = "Expense", Paid = true, Date = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        var date = DateTime.UtcNow.AddDays(-1);
        var result = Assert.IsType<OkObjectResult>(await controller.GetSummary(date.Month, date.Year, CancellationToken.None));
        var summary = Assert.IsType<DashboardSummaryController.DashboardSummaryResponse>(result.Value);
        Assert.Equal(108.80m, summary.Summary.Expense);
        Assert.Equal(108.80m, Assert.Single(summary.CategorySummary).Total);
        Assert.Equal(461.20m, summary.Summary.Total);
        Assert.Equal(538.80m, Assert.Single(summary.Transactions).Amount);
        Assert.Equal(108.80m, Assert.Single(summary.Transactions).ReportingAmount);
    }

    [Fact]
    public async Task SharedExpense_EditPreservesShareAndRejectsChangingGrossAmount()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, category, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var tx = new Transaction { UserId = 1, AccountId = bank.Id, CategoryId = category.Id,
            Description = "Compartilhada", Amount = 200m, ReportingAmount = 50m,
            Type = "Expense", Paid = true, Date = DateTime.UtcNow.AddDays(-1) };
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();
        var controller = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(controller);
        var request = new UpsertTransactionDto { Id = tx.Id, AccountId = bank.Id, CategoryId = category.Id,
            Description = "Compartilhada revisada", Amount = 200m, Type = "Expense", Paid = true, Date = tx.Date };
        Assert.IsType<NoContentResult>(await controller.PutTransaction(tx.Id, request));
        Assert.Equal(50m, (await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == tx.Id)).ReportingAmount);
        request.Amount = 199m;
        Assert.IsType<BadRequestObjectResult>(await controller.PutTransaction(tx.Id, request));
    }

    [Fact]
    public async Task Summary_SeparatesMonthsStatusesAndOperationalMovements_WithoutFilteringWealth()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, card, expense, income) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new DashboardSummaryController(db, finance, NullLogger<DashboardSummaryController>.Instance);
        TestContextFactory.AttachUser(controller);
        Transaction Tx(int month, int day, string type, decimal amount, bool paid = true) => new()
        {
            UserId = 1, AccountId = bank.Id, CategoryId = type == "Income" ? income.Id : expense.Id,
            Description = "Teste", Amount = amount, Type = type, Paid = paid,
            Date = new DateTime(2026, month, day, 0, 0, 0, DateTimeKind.Utc)
        };

        var transfer = Tx(11, 2, "Income", 900); transfer.IsTransfer = true;
        var payment = Tx(11, 2, "Expense", 800); payment.ReportingKind = ReportingKinds.InvoicePayment;
        var excluded = Tx(11, 2, "Expense", 700); excluded.ExcludeFromReports = true;
        var asset = Tx(11, 2, "Income", 600); asset.ReportingKind = ReportingKinds.TechnicalAdjustment;
        var installment = Tx(12, 1, "Expense", 400, false); installment.InstallmentId = "installment";
        var cardPurchase = Tx(11, 10, "Expense", 30); cardPurchase.AccountId = card.Id;
        db.Transactions.AddRange(Tx(7, 1, "Income", 10000), Tx(7, 1, "Expense", 9000),
            Tx(10, 31, "Expense", 40), Tx(11, 1, "Income", 100), Tx(11, 30, "Expense", 20),
            Tx(11, 2, "Income", 10, false), Tx(11, 2, "Expense", 15, false),
            transfer, payment, excluded, asset, installment, cardPurchase);
        await db.SaveChangesAsync();

        async Task<DashboardSummaryController.DashboardSummaryResponse> Get(int month)
        {
            var result = Assert.IsType<OkObjectResult>(await controller.GetSummary(month, 2026, CancellationToken.None));
            return Assert.IsType<DashboardSummaryController.DashboardSummaryResponse>(result.Value);
        }

        var july = await Get(7);
        var october = await Get(10);
        var november = await Get(11);
        var december = await Get(12);
        Assert.Equal(10000m, july.Summary.Income);
        Assert.Equal(9000m, july.Summary.Expense);
        Assert.Equal(40m, october.Summary.Expense);
        Assert.Equal(100m, november.Summary.Income);
        Assert.Equal(50m, november.Summary.Expense);
        Assert.Equal(10m, november.Summary.PendingIncome);
        Assert.Equal(15m, november.Summary.PendingExpense);
        Assert.Equal(0m, december.Summary.Expense);
        Assert.Equal(400m, december.Summary.PendingExpense);
        Assert.Equal(50m, Assert.Single(november.CategorySummary).Total);
        Assert.Equal(40m, Assert.Single(october.CategorySummary).Total);
        Assert.Equal(november.Summary.NetWorth, july.Summary.NetWorth);
        Assert.Equal(november.Summary.Total, october.Summary.Total);
        Assert.Equal(30m, Assert.Single(november.Cards).InvoiceAmount);
        Assert.Equal(0m, Assert.Single(october.Cards).InvoiceAmount);
    }

    [Fact]
    public async Task FreeToSpend_DoesNotProjectRecurringRuleWhenMatchingTransactionExists()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, expense, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new DashboardSummaryController(db, finance, NullLogger<DashboardSummaryController>.Instance);
        TestContextFactory.AttachUser(controller);
        var rule = new RecurringTransaction { UserId = 1, AccountId = bank.Id, CategoryId = expense.Id,
            Description = "Internet", Amount = 100m, Type = "Expense", DayOfMonth = 10, Active = true };
        db.RecurringTransactions.Add(rule);
        await db.SaveChangesAsync();
        db.Transactions.Add(new Transaction { UserId = 1, AccountId = bank.Id, CategoryId = expense.Id,
            Description = "Internet", Amount = 100m, Type = "Expense", Paid = false, RecurringRuleId = rule.Id,
            Date = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await controller.GetSummary(11, 2026, CancellationToken.None));
        var summary = Assert.IsType<DashboardSummaryController.DashboardSummaryResponse>(result.Value);
        Assert.Equal(100m, summary.Summary.PendingExpense);
        Assert.Equal(100m, summary.FreeToSpend.RecurringExpenses);
    }
}
