using Microsoft.AspNetCore.Http;

namespace Finflow.Api.LogicTests;

public class FinancialCoreLogicTests
{
    [Fact]
    public async Task PaidExpense_ReducesRealBalance_WhilePendingExpenseDoesNot()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(controller);

        await controller.PostTransaction(new UpsertTransactionDto
        {
            Description = "Despesa paga",
            Amount = 100m,
            Type = "Expense",
            Paid = true,
            CategoryId = expenseCategory.Id,
            AccountId = bank.Id,
            Date = DateTime.UtcNow
        });

        await controller.PostTransaction(new UpsertTransactionDto
        {
            Description = "Despesa pendente",
            Amount = 50m,
            Type = "Expense",
            Paid = false,
            CategoryId = expenseCategory.Id,
            AccountId = bank.Id,
            Date = DateTime.UtcNow
        });

        var snapshot = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        var bankSnapshot = snapshot.AccountSnapshots.Single(s => s.AccountId == bank.Id);

        Assert.Equal(900m, bankSnapshot.RealBalance);
        Assert.Equal(850m, bankSnapshot.PendingBalance);
    }

    [Fact]
    public async Task PaidIncome_IncreasesRealBalance_WhilePendingIncomeDoesNot()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, _, incomeCategory) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(controller);

        await controller.PostTransaction(new UpsertTransactionDto
        {
            Description = "Receita paga",
            Amount = 200m,
            Type = "Income",
            Paid = true,
            CategoryId = incomeCategory.Id,
            AccountId = bank.Id,
            Date = DateTime.UtcNow
        });

        await controller.PostTransaction(new UpsertTransactionDto
        {
            Description = "Receita pendente",
            Amount = 80m,
            Type = "Income",
            Paid = false,
            CategoryId = incomeCategory.Id,
            AccountId = bank.Id,
            Date = DateTime.UtcNow
        });

        var snapshot = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        var bankSnapshot = snapshot.AccountSnapshots.Single(s => s.AccountId == bank.Id);

        Assert.Equal(1200m, bankSnapshot.RealBalance);
        Assert.Equal(1280m, bankSnapshot.PendingBalance);
    }

    [Fact]
    public async Task Transfer_MovesMoneyBetweenAccounts_WithoutCreatingExternalIncomeOrExpense()
    {
        var (db, finance) = TestContextFactory.Create();
        var bankA = new Account { UserId = 1, Name = "Conta A", Type = "Checking", InitialBalance = 1000m, CurrentBalance = 1000m };
        var bankB = new Account { UserId = 1, Name = "Conta B", Type = "Checking", InitialBalance = 200m, CurrentBalance = 200m };
        db.Accounts.AddRange(bankA, bankB);
        await db.SaveChangesAsync();

        var controller = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(controller);

        var result = await controller.Transfer(new TransferDto
        {
            FromAccountId = bankA.Id,
            ToAccountId = bankB.Id,
            Amount = 150m,
            Date = DateTime.UtcNow
        });

        Assert.IsType<OkObjectResult>(result);

        var snapshot = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(850m, snapshot.AccountSnapshots.Single(s => s.AccountId == bankA.Id).RealBalance);
        Assert.Equal(350m, snapshot.AccountSnapshots.Single(s => s.AccountId == bankB.Id).RealBalance);
        Assert.All(snapshot.Transactions, t => Assert.True(t.IsTransfer));
    }

    [Fact]
    public async Task EditingTransfer_UpdatesBothSides_AndDeletingTransfer_RemovesBothSides()
    {
        var (db, finance) = TestContextFactory.Create();
        var bankA = new Account { UserId = 1, Name = "Conta A", Type = "Checking", InitialBalance = 1000m, CurrentBalance = 1000m };
        var bankB = new Account { UserId = 1, Name = "Conta B", Type = "Checking", InitialBalance = 500m, CurrentBalance = 500m };
        db.Accounts.AddRange(bankA, bankB);
        await db.SaveChangesAsync();

        var controller = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(controller);

        await controller.Transfer(new TransferDto
        {
            FromAccountId = bankA.Id,
            ToAccountId = bankB.Id,
            Amount = 150m,
            Date = DateTime.UtcNow
        });

        var transferExpense = await db.Transactions.SingleAsync(t => t.Type == "Expense" && t.IsTransfer);
        var updatedDate = DateTime.UtcNow.AddDays(2);

        var updateResult = await controller.PutTransaction(transferExpense.Id, new UpsertTransactionDto
        {
            Id = transferExpense.Id,
            Description = "Transferencia editada",
            Amount = 225m,
            Type = "Expense",
            AccountId = bankA.Id,
            Date = updatedDate,
            Paid = false
        });

        Assert.IsType<NoContentResult>(updateResult);

        var pair = await db.Transactions
            .Where(t => t.TransferGroupId == transferExpense.TransferGroupId)
            .OrderBy(t => t.Type)
            .ToListAsync();

        Assert.Equal(2, pair.Count);
        Assert.All(pair, transaction => Assert.Equal(225m, transaction.Amount));
        Assert.All(pair, transaction => Assert.Equal(updatedDate.ToUniversalTime(), transaction.Date));
        Assert.All(pair, transaction => Assert.True(transaction.Paid));

        var deleteResult = await controller.DeleteTransaction(transferExpense.Id, false);
        Assert.IsType<NoContentResult>(deleteResult);
        Assert.Empty(await db.Transactions.ToListAsync());
    }

    [Fact]
    public async Task TransferOperation_WithOrphanedPair_ReturnsConflict()
    {
        var (db, finance) = TestContextFactory.Create();
        var bank = new Account { UserId = 1, Name = "Conta A", Type = "Checking", InitialBalance = 1000m, CurrentBalance = 1000m };
        db.Accounts.Add(bank);
        await db.SaveChangesAsync();

        db.Transactions.Add(new Transaction
        {
            UserId = 1,
            AccountId = bank.Id,
            Amount = 100m,
            Type = "Expense",
            Description = "Transferencia para conta/cartao",
            Date = DateTime.UtcNow,
            Paid = true,
            IsTransfer = true,
            TransferGroupId = "broken-transfer"
        });
        await db.SaveChangesAsync();

        var transaction = await db.Transactions.SingleAsync();
        var controller = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(controller);

        var deleteResult = await controller.DeleteTransaction(transaction.Id, false);
        Assert.IsType<ConflictObjectResult>(deleteResult);
    }

    [Fact]
    public async Task CreditCardPurchase_ChangesInvoiceButNotBankBalance_AndInvoicePaymentLowersLiability()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, card, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var transactions = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(transactions);

        await transactions.PostTransaction(new UpsertTransactionDto
        {
            Description = "Compra no cartao",
            Amount = 120m,
            Type = "Expense",
            Paid = true,
            CategoryId = expenseCategory.Id,
            AccountId = card.Id,
            Date = DateTime.UtcNow
        });

        var invoiceResult = await transactions.GetInvoiceSummary(card.Id, DateTime.UtcNow.Month, DateTime.UtcNow.Year);
        var invoiceJson = TestContextFactory.ToJsonElement(invoiceResult.Value!);
        Assert.Equal(120m, invoiceJson.GetProperty("total").GetDecimal());

        var snapshotAfterPurchase = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(1000m, snapshotAfterPurchase.AccountSnapshots.Single(s => s.AccountId == bank.Id).RealBalance);
        Assert.Equal(120m, snapshotAfterPurchase.AccountSnapshots.Single(s => s.AccountId == card.Id).OutstandingLiability);

        await transactions.Transfer(new TransferDto
        {
            FromAccountId = bank.Id,
            ToAccountId = card.Id,
            Amount = 120m,
            Date = DateTime.UtcNow
        });

        var snapshotAfterPayment = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(880m, snapshotAfterPayment.AccountSnapshots.Single(s => s.AccountId == bank.Id).RealBalance);
        Assert.Equal(0m, snapshotAfterPayment.AccountSnapshots.Single(s => s.AccountId == card.Id).OutstandingLiability);
    }

    [Fact]
    public async Task CreditCardInstallmentInProgress_CreatesOnlyRemainingParcels_WithCorrectSequence()
    {
        var (db, finance) = TestContextFactory.Create();
        var (_, card, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var transactions = new TransactionsController(db, finance);
        TestContextFactory.AttachUser(transactions);
        var baseDate = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

        var result = await transactions.PostTransaction(new UpsertTransactionDto
        {
            Description = "Revisao da moto - Moto Facil Yamaha",
            Amount = 71.73m,
            Type = "Expense",
            Paid = true,
            CategoryId = expenseCategory.Id,
            AccountId = card.Id,
            Date = baseDate,
            InstallmentNumber = 3,
            TotalInstallments = 6
        });

        Assert.IsType<CreatedAtActionResult>(result.Result);

        var created = await db.Transactions
            .Where(t => t.AccountId == card.Id)
            .OrderBy(t => t.Date)
            .ToListAsync();

        Assert.Equal(4, created.Count);
        Assert.Collection(created,
            first =>
            {
                Assert.Equal("Revisao da moto - Moto Facil Yamaha (3/6)", first.Description);
                Assert.Equal(baseDate, first.Date);
            },
            second =>
            {
                Assert.Equal("Revisao da moto - Moto Facil Yamaha (4/6)", second.Description);
                Assert.Equal(baseDate.AddMonths(1), second.Date);
            },
            third =>
            {
                Assert.Equal("Revisao da moto - Moto Facil Yamaha (5/6)", third.Description);
                Assert.Equal(baseDate.AddMonths(2), third.Date);
            },
            fourth =>
            {
                Assert.Equal("Revisao da moto - Moto Facil Yamaha (6/6)", fourth.Description);
                Assert.Equal(baseDate.AddMonths(3), fourth.Date);
            });

        Assert.Single(created.Select(t => t.InstallmentId).Distinct());
    }

    [Fact]
    public async Task RecurringFuture_AppearsInProjection_ButNotInRealBalance_AndDoesNotDuplicateGeneration()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var recurring = new RecurringController(db, finance);
        TestContextFactory.AttachUser(recurring);

        var nextMonth = DateTime.UtcNow.AddMonths(1);
        var createResult = await recurring.PostRecurring(new RecurringTransaction
        {
            Description = "Academia",
            Amount = 90m,
            Type = "Expense",
            DayOfMonth = 5,
            Active = true,
            CategoryId = expenseCategory.Id,
            AccountId = bank.Id
        });
        Assert.IsType<CreatedAtActionResult>(createResult.Result);

        var projectionResult = await recurring.GetProjection(3, nextMonth.Month, nextMonth.Year);
        var projectionObject = Assert.IsType<OkObjectResult>(projectionResult);
        var projectionJson = TestContextFactory.ToJsonElement(projectionObject.Value!);
        Assert.Equal(1000m, projectionJson.GetProperty("startBalance").GetDecimal());
        Assert.Equal(90m, projectionJson.GetProperty("items")[0].GetProperty("Expense").GetDecimal());

        var snapshotBeforeGenerate = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(1000m, snapshotBeforeGenerate.AccountSnapshots.Single(s => s.AccountId == bank.Id).RealBalance);

        var generateFirst = await recurring.GenerateTransactions(nextMonth.Month, nextMonth.Year);
        var generateSecond = await recurring.GenerateTransactions(nextMonth.Month, nextMonth.Year);
        var secondJson = TestContextFactory.ToJsonElement(((OkObjectResult)generateSecond).Value!);

        Assert.Equal("0 transacoes geradas.", secondJson.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Projection_IgnoresInternalTransferBetweenOwnAccounts_InIncomeAndExpense()
    {
        var (db, finance) = TestContextFactory.Create();
        var bankA = new Account { UserId = 1, Name = "Conta A", Type = "Checking", InitialBalance = 1000m, CurrentBalance = 1000m };
        var bankB = new Account { UserId = 1, Name = "Conta B", Type = "Checking", InitialBalance = 200m, CurrentBalance = 200m };
        db.Accounts.AddRange(bankA, bankB);
        await db.SaveChangesAsync();

        var nextMonth = DateTime.UtcNow.AddMonths(1);
        var transferGroupId = Guid.NewGuid().ToString("N");
        db.Transactions.AddRange(
            new Transaction
            {
                UserId = 1,
                AccountId = bankA.Id,
                Amount = 300m,
                Type = "Expense",
                Description = "Transferencia para conta/cartao",
                Date = nextMonth,
                Paid = true,
                IsTransfer = true,
                TransferGroupId = transferGroupId
            },
            new Transaction
            {
                UserId = 1,
                AccountId = bankB.Id,
                Amount = 300m,
                Type = "Income",
                Description = "Recebido de transferencia",
                Date = nextMonth,
                Paid = true,
                IsTransfer = true,
                TransferGroupId = transferGroupId
            });
        await db.SaveChangesAsync();

        var projection = finance.BuildProjection(
            await db.Accounts.AsNoTracking().ToListAsync(),
            await db.Transactions.AsNoTracking().ToListAsync(),
            [],
            DateTime.UtcNow,
            nextMonth.Month,
            nextMonth.Year,
            1);

        var firstMonth = projection.Items.Single();
        Assert.Equal(0m, firstMonth.Income);
        Assert.Equal(0m, firstMonth.Expense);
        Assert.Equal(0m, firstMonth.TransferImpact);
        Assert.Equal(0m, firstMonth.Net);
    }

    [Fact]
    public async Task MonthlyBudgets_DoNotOverwriteOtherMonths()
    {
        var (db, _) = TestContextFactory.Create();
        var (_, _, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new BudgetsController(db);
        TestContextFactory.AttachUser(controller);

        await controller.PostBudget(new BudgetsController.UpsertBudgetDto
        {
            CategoryId = expenseCategory.Id,
            Amount = 500m,
            Month = 7,
            Year = 2026
        });

        await controller.PostBudget(new BudgetsController.UpsertBudgetDto
        {
            CategoryId = expenseCategory.Id,
            Amount = 900m,
            Month = 8,
            Year = 2026
        });

        var july = Assert.IsType<ActionResult<IEnumerable<Budget>>>(await controller.GetBudgets(7, 2026));
        var august = Assert.IsType<ActionResult<IEnumerable<Budget>>>(await controller.GetBudgets(8, 2026));

        Assert.Equal(500m, Assert.Single(july.Value!).Amount);
        Assert.Equal(900m, Assert.Single(august.Value!).Amount);
    }

    [Fact]
    public async Task ImportingInvoicePayment_CreatesTransferInsteadOfFalseIncome()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, card, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);

        db.Transactions.Add(new Transaction
        {
            UserId = 1,
            AccountId = card.Id,
            CategoryId = expenseCategory.Id,
            Description = "Compra anterior",
            Amount = 100m,
            Type = "Expense",
            Paid = true,
            Date = DateTime.UtcNow.AddDays(-2)
        });
        await db.SaveChangesAsync();
        await finance.RecalculateAccountBalancesAsync(1);

        var controller = new ImportController(db, finance);
        TestContextFactory.AttachUser(controller);

        var csv = "Data,Valor,Ignorar,Descricao\n" +
                  $"{DateTime.UtcNow:dd/MM/yyyy},-100,x,Pagamento de fatura Cartao Principal";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "extrato.csv");

        var result = await controller.UploadStatement(file, bank.Id, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);

        var snapshot = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(900m, snapshot.AccountSnapshots.Single(s => s.AccountId == bank.Id).RealBalance);
        Assert.Equal(0m, snapshot.AccountSnapshots.Single(s => s.AccountId == card.Id).OutstandingLiability);
        Assert.DoesNotContain(snapshot.Transactions, t => t.AccountId == bank.Id && t.Type == "Income" && !t.IsTransfer);
        Assert.Contains(snapshot.Transactions, t => t.AccountId == card.Id && t.Type == "Income" && t.IsTransfer);
    }

    [Fact]
    public async Task ImportingInvoicePayment_WithMultipleCardsAndAmbiguousDescription_CreatesManualReview()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, card, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        db.Accounts.Add(new Account
        {
            UserId = 1,
            Name = "Cartao Secundario",
            Type = "Checking",
            IsCreditCard = true,
            CreditLimit = 3000m,
            ClosingDay = 20,
            DueDay = 10
        });
        db.Transactions.Add(new Transaction
        {
            UserId = 1,
            AccountId = card.Id,
            CategoryId = expenseCategory.Id,
            Description = "Compra anterior",
            Amount = 80m,
            Type = "Expense",
            Paid = true,
            Date = DateTime.UtcNow.AddDays(-2)
        });
        await db.SaveChangesAsync();

        var controller = new ImportController(db, finance);
        TestContextFactory.AttachUser(controller);
        var csv = "Data,Valor,Ignorar,Descricao\n" +
                  $"{DateTime.UtcNow:dd/MM/yyyy},-80,x,Pagamento de fatura";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "extrato.csv");

        var result = await controller.UploadStatement(file, bank.Id, CancellationToken.None);
        var payload = TestContextFactory.ToJsonElement(((OkObjectResult)result).Value!);

        Assert.Equal(1, payload.GetProperty("manualReviewCount").GetInt32());
        Assert.Contains(await db.Transactions.ToListAsync(), t => t.Description.StartsWith("REVISAR MANUALMENTE:", StringComparison.Ordinal));
        Assert.DoesNotContain(await db.Transactions.ToListAsync(), t => t.IsTransfer && t.AccountId == bank.Id && t.Amount == 80m);
    }

    [Fact]
    public async Task ImportingInvoicePayment_WithoutExistingCard_ProvisionsCreditCardAndCreatesTransfer()
    {
        var (db, finance) = TestContextFactory.Create();
        var bank = new Account
        {
            UserId = 1,
            Name = "Nubank",
            Type = "Checking",
            InitialBalance = 1000m,
            CurrentBalance = 1000m
        };
        db.Accounts.Add(bank);
        await db.SaveChangesAsync();

        var controller = new ImportController(db, finance);
        TestContextFactory.AttachUser(controller);

        var csv = "Data,Valor,Ignorar,Descricao\n" +
                  $"{DateTime.UtcNow:dd/MM/yyyy},-80,x,Pagamento de fatura";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        IFormFile file = new FormFile(stream, 0, stream.Length, "file", "extrato.csv");

        var result = await controller.UploadStatement(file, bank.Id, CancellationToken.None);
        var payload = TestContextFactory.ToJsonElement(((OkObjectResult)result).Value!);

        Assert.Equal(0, payload.GetProperty("manualReviewCount").GetInt32());

        var card = await db.Accounts.SingleAsync(a => a.IsCreditCard);
        Assert.Equal("Cartao Nubank", card.Name);
        Assert.Contains(await db.Transactions.ToListAsync(), t => t.IsTransfer && t.AccountId == bank.Id && t.Type == "Expense" && t.Amount == 80m);
        Assert.Contains(await db.Transactions.ToListAsync(), t => t.IsTransfer && t.AccountId == card.Id && t.Type == "Income" && t.Amount == 80m);
    }

    [Fact]
    public async Task ImportingChargeback_OnCreditCard_ReducesLiability_ForTotalAndPartialReversal()
    {
        var (db, finance) = TestContextFactory.Create();
        var (_, card, expenseCategory, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        db.Transactions.Add(new Transaction
        {
            UserId = 1,
            AccountId = card.Id,
            CategoryId = expenseCategory.Id,
            Description = "Compra original",
            Amount = 150m,
            Type = "Expense",
            Paid = true,
            Date = DateTime.UtcNow.AddDays(-5)
        });
        await db.SaveChangesAsync();

        var controller = new ImportController(db, finance);
        TestContextFactory.AttachUser(controller);

        async Task ImportLineAsync(decimal amount, string description)
        {
            var csv = "Data,Valor,Ignorar,Descricao\n" +
                      $"{DateTime.UtcNow:dd/MM/yyyy},{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)},x,{description}";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            IFormFile file = new FormFile(stream, 0, stream.Length, "file", "extrato.csv");
            await controller.UploadStatement(file, card.Id, CancellationToken.None);
        }

        await ImportLineAsync(50m, "Estorno parcial compra original");
        var afterPartial = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(100m, afterPartial.AccountSnapshots.Single(s => s.AccountId == card.Id).OutstandingLiability);

        await ImportLineAsync(100m, "Estorno total compra original");
        var afterTotal = await finance.BuildUserSnapshotAsync(1, DateTime.UtcNow);
        Assert.Equal(0m, afterTotal.AccountSnapshots.Single(s => s.AccountId == card.Id).OutstandingLiability);
    }

    [Fact]
    public async Task ImportingSameStatementTwice_DoesNotDuplicateTransactions()
    {
        var (db, finance) = TestContextFactory.Create();
        var (bank, _, _, _) = await TestContextFactory.SeedFinanceBaseAsync(db);
        var controller = new ImportController(db, finance);
        TestContextFactory.AttachUser(controller);

        var csv = "Data,Valor,Ignorar,Descricao\n" +
                  $"{DateTime.UtcNow:dd/MM/yyyy},-42.50,x,Compra QA";

        async Task UploadAsync()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
            IFormFile file = new FormFile(stream, 0, stream.Length, "file", "extrato.csv");
            await controller.UploadStatement(file, bank.Id, CancellationToken.None);
        }

        await UploadAsync();
        await UploadAsync();

        Assert.Single(await db.Transactions.Where(t => t.AccountId == bank.Id && t.Description == "Compra QA").ToListAsync());
    }
}
