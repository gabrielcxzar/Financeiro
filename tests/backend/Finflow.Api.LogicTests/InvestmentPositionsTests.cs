using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Controllers;
using MyFinance.API.Mcp;
using MyFinance.API.Models;
using MyFinance.API.Services;

namespace Finflow.Api.LogicTests;

[Collection("Finflow PostgreSQL")]
public sealed class InvestmentPositionsTests
{
    private static readonly DateOnly SnapshotDate = new(2026, 1, 1);

    [Fact]
    public async Task InvestmentPositions_CalculatesCostMarketValuesAllocationAndKeepsLedgerSeparate()
    {
        var (db, snapshots) = TestContextFactory.Create();
        db.Users.AddRange(
            new User { Id = 1, Name = "Portfolio Owner", Email = "owner@example.test", PasswordHash = "test" },
            new User { Id = 2, Name = "Other", Email = "other@example.test", PasswordHash = "test" });
        db.Accounts.Add(new Account { UserId = 1, Name = "Test Bank", Type = "Checking", InitialBalance = 123.45m, CurrentBalance = 123.45m });
        db.FixedIncomeHoldings.AddRange(
            new FixedIncomeHolding { UserId = 1, Name = "RDB Teste", Institution = "Instituicao Teste", ProductType = "RDB", Benchmark = "CDI", ContractedRate = 100m, ContractedRateUnit = "percent_of_benchmark", KnownBalance = 1000m, BalanceAsOfDate = SnapshotDate, ValuationSource = "test manual snapshot" },
            new FixedIncomeHolding { UserId = 1, Name = "CDB Teste", Institution = "Banco Teste", ProductType = "CDB", Benchmark = "Prefixado", ContractedRate = 12.5m, ContractedRateUnit = "annual_percent", KnownBalance = 2000m, BalanceAsOfDate = SnapshotDate, ValuationSource = "test manual snapshot" },
            new FixedIncomeHolding { UserId = 2, Name = "Other user", ProductType = "CDB", KnownBalance = 999999m, BalanceAsOfDate = SnapshotDate });
        db.FiiHoldings.AddRange(
            Fii("TEST11", 4, 10m, 12m),
            Fii("TEST12", 2, 25m, 24m),
            Fii("TEST13", 10, 5m, 5m),
            Fii("TEST14", 1, 100m, 110m),
            new FiiHolding { UserId = 2, Ticker = "ABCD11", Shares = 10, AvgPrice = 10, CurrentPrice = 100 });
        db.Transactions.Add(new Transaction { UserId = 1, AccountId = 1, Amount = 25m, Type = "Expense", Date = new DateTime(2026, 9, 2) });
        await db.SaveChangesAsync();
        var transactionCount = await db.Transactions.CountAsync();
        var service = CreateService(db, snapshots);

        var result = await service.GetInvestmentPositionsAsync(1, true, true, default);

        Assert.Equal(2, result.Data.FixedIncome.Count);
        Assert.Equal(new[] { "TEST11", "TEST12", "TEST13", "TEST14" }, result.Data.FiiHoldings.Select(x => x.Ticker));
        Assert.Equal(40m, result.Data.FiiHoldings[0].CostBasis);
        Assert.Equal(48m, result.Data.FiiHoldings[0].MarketValue);
        Assert.Equal(8m, result.Data.FiiHoldings[0].UnrealizedGain);
        Assert.Equal(decimal.Round(8m / 40m * 100m, 2), result.Data.FiiHoldings[0].UnrealizedReturnPercent);
        Assert.Equal(240m, result.Data.FiiHoldings.Sum(x => x.CostBasis));
        Assert.Equal(256m, result.Data.TotalMarketValue);
        Assert.Equal(3256m, result.Data.TotalKnownValue);
        Assert.Equal(240m, result.Data.TotalCostBasis);
        Assert.False(result.Data.CostBasisComplete);
        Assert.Equal(3000m, result.Data.Allocation.Single(x => x.AssetType == "fixed_income").Value);
        Assert.Equal(decimal.Round(3000m / 3256m * 100m, 2), result.Data.Allocation.Single(x => x.AssetType == "fixed_income").SharePercent);
        Assert.Equal(SnapshotDate, result.Data.FixedIncome[0].ValueAsOfDate);
        Assert.Equal("test manual snapshot", result.Data.FixedIncome[0].ValuationSource);
        Assert.Null(result.Data.FixedIncome[0].MaturityDate);
        Assert.Null(result.Data.FixedIncome[0].Liquidity);
        Assert.Null(result.Data.FixedIncome[0].PrincipalAmount);
        Assert.True(result.Meta.Partial);
        Assert.Single(result.Data.FixedIncome, x => x.Name == "RDB Teste");
        Assert.Equal(transactionCount, await db.Transactions.CountAsync());

        var balances = await service.GetAccountBalancesAsync(1, false, true, default);
        Assert.Equal(123.45m, balances.Data.Totals.KnownCash);
    }

    [Fact]
    public async Task InvestmentPositions_LeavesMissingQuotesAndUnknownFixedIncomeFieldsNull()
    {
        var (db, snapshots) = TestContextFactory.Create();
        db.Users.Add(new User { Id = 1, Name = "Portfolio Owner", Email = "g@example.test", PasswordHash = "test" });
        db.FixedIncomeHoldings.Add(new FixedIncomeHolding { UserId = 1, Name = "RDB Teste sem vencimento", ProductType = "RDB", KnownBalance = 1000m, BalanceAsOfDate = SnapshotDate, ValuationSource = "manual" });
        db.FiiHoldings.Add(new FiiHolding { UserId = 1, Ticker = "TEST11", Shares = 4, AvgPrice = 10m });
        await db.SaveChangesAsync();
        var service = CreateService(db, snapshots);

        var result = await service.GetInvestmentPositionsAsync(1, false, true, default);

        var fii = Assert.Single(result.Data.FiiHoldings);
        Assert.Equal(40m, fii.CostBasis);
        Assert.Null(fii.CurrentPrice);
        Assert.Null(fii.MarketValue);
        Assert.Null(fii.UnrealizedGain);
        Assert.Null(fii.UnrealizedReturnPercent);
        Assert.Equal(1000m, result.Data.TotalKnownValue);
        Assert.Equal("confirmed_manual", result.Data.FixedIncome.Single().ValuationType);
        Assert.Null(result.Data.FixedIncome.Single().MaturityDate);
        Assert.Null(result.Data.FixedIncome.Single().Liquidity);
        Assert.Null(result.Data.FixedIncome.Single().PrincipalAmount);
        Assert.Equal(1, result.Data.Coverage.FiiWithoutMarketQuoteCount);
        Assert.Equal(1, result.Data.Coverage.UnvaluedPositionCount);
    }

    [Fact]
    public async Task FiiQuoteRefresh_IsolatesUserAndPreservesPersistedQuoteWhenProviderFails()
    {
        var (db, _) = TestContextFactory.Create();
        var userOne = new FiiHolding { UserId = 1, Ticker = "TEST11", Shares = 4, AvgPrice = 10m, CurrentPrice = 9.75m, QuoteAsOfDate = SnapshotDate, QuoteSource = "manual" };
        db.FiiHoldings.AddRange(userOne, new FiiHolding { UserId = 2, Ticker = "TEST12", Shares = 2, AvgPrice = 25m, CurrentPrice = 24m, QuoteAsOfDate = SnapshotDate, QuoteSource = "manual test snapshot" });
        await db.SaveChangesAsync();
        var controller = new FiiHoldingsController(db, new StubQuoteProvider(true, new(false, null, "provider_unavailable")));
        TestContextFactory.AttachUser(controller, 1);

        var response = await controller.RefreshQuotes(default);

        var body = Assert.IsType<OkObjectResult>(response.Result).Value as QuoteRefreshResponse;
        Assert.NotNull(body);
        Assert.Equal(1, body.FailedCount);
        Assert.Equal("TEST11", Assert.Single(body.Items).Ticker);
        Assert.Equal(9.75m, userOne.CurrentPrice);
        Assert.Equal(SnapshotDate, userOne.QuoteAsOfDate);

        var positionEdit = await controller.UpsertHolding(new FiiHoldingRequest("TEST11", 5m, 9.5m, "updated shares"));
        var updatedPosition = Assert.IsType<FiiHolding>(Assert.IsType<OkObjectResult>(positionEdit.Result).Value);
        Assert.Equal(5m, updatedPosition.Shares);
        Assert.Equal(9.75m, updatedPosition.CurrentPrice);
        Assert.Equal("manual", updatedPosition.QuoteSource);

        var otherUserHolding = await db.FiiHoldings.AsNoTracking().SingleAsync(x => x.UserId == 2);
        var crossUserUpdate = await controller.UpdateManualQuote(otherUserHolding.Id, new(100m, SnapshotDate, "manual"));
        Assert.IsType<NotFoundResult>(crossUserUpdate.Result);
    }

    [Fact]
    public async Task FiiQuoteRefreshWithoutProviderKeyDoesNotMakeExternalChanges()
    {
        var (db, _) = TestContextFactory.Create();
        db.FiiHoldings.Add(new FiiHolding { UserId = 1, Ticker = "TEST11", Shares = 4, AvgPrice = 10m, CurrentPrice = 9.75m });
        await db.SaveChangesAsync();
        var controller = new FiiHoldingsController(db, new StubQuoteProvider(false, new(false, null, "provider_not_configured")));
        TestContextFactory.AttachUser(controller, 1);

        var response = await controller.RefreshQuotes(default);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(response.Result).StatusCode);
        Assert.Equal(9.75m, await db.FiiHoldings.Select(x => x.CurrentPrice).SingleAsync());
    }

    [Fact]
    public async Task FiiQuoteRefreshDoesNotReplaceManualQuoteWithSameDayDataButAcceptsNewerMarketDate()
    {
        var (db, _) = TestContextFactory.Create();
        db.FiiHoldings.Add(new FiiHolding { UserId = 1, Ticker = "TEST11", Shares = 4, AvgPrice = 10m, CurrentPrice = 9.75m, QuoteAsOfDate = SnapshotDate, QuoteSource = "manual test snapshot" });
        await db.SaveChangesAsync();
        var sameDayProvider = new StubQuoteProvider(true, new(true, new("TEST11", 10m, new DateTimeOffset(2026, 1, 1, 18, 0, 0, TimeSpan.Zero), "brapi")));
        var sameDayController = new FiiHoldingsController(db, sameDayProvider);
        TestContextFactory.AttachUser(sameDayController, 1);

        var sameDayResponse = await sameDayController.RefreshQuotes(default);

        var sameDayBody = Assert.IsType<QuoteRefreshResponse>(Assert.IsType<OkObjectResult>(sameDayResponse.Result).Value);
        Assert.Equal(0, sameDayBody.UpdatedCount);
        Assert.Equal(9.75m, await db.FiiHoldings.Select(x => x.CurrentPrice).SingleAsync());

        var newerProvider = new StubQuoteProvider(true, new(true, new("TEST11", 10.25m, new DateTimeOffset(2026, 1, 2, 18, 0, 0, TimeSpan.Zero), "brapi")));
        var newerController = new FiiHoldingsController(db, newerProvider);
        TestContextFactory.AttachUser(newerController, 1);
        var newerResponse = await newerController.RefreshQuotes(default);

        var newerBody = Assert.IsType<QuoteRefreshResponse>(Assert.IsType<OkObjectResult>(newerResponse.Result).Value);
        Assert.Equal(1, newerBody.UpdatedCount);
        var refreshed = await db.FiiHoldings.SingleAsync();
        Assert.Equal(10.25m, refreshed.CurrentPrice);
        Assert.Equal(new DateOnly(2026, 1, 2), refreshed.QuoteAsOfDate);
        Assert.Equal("brapi", refreshed.QuoteSource);
    }

    [Fact]
    public async Task FixedIncomeEndpoints_SaveKnownValueWithSourceDateAndEnforceUserScopeWithoutTransactions()
    {
        var (db, _) = TestContextFactory.Create();
        db.Users.AddRange(
            new User { Id = 1, Name = "Portfolio Owner", Email = "one@example.test", PasswordHash = "test" },
            new User { Id = 2, Name = "Other", Email = "two@example.test", PasswordHash = "test" });
        db.FixedIncomeHoldings.Add(new FixedIncomeHolding { UserId = 2, Name = "Private CDB", ProductType = "CDB", KnownBalance = 100m, BalanceAsOfDate = SnapshotDate, ValuationSource = "manual" });
        await db.SaveChangesAsync();
        var controller = new FixedIncomeHoldingsController(db);
        TestContextFactory.AttachUser(controller, 1);
        var request = new FixedIncomeHoldingsController.FixedIncomeHoldingRequest(
            "RDB Teste", "Instituicao Teste", "RDB", "CDI", 100m, "percent_of_benchmark", null, null, null,
            1000m, SnapshotDate, "test manual snapshot", null);

        var created = await controller.Upsert(request, default);

        var holding = Assert.IsType<FixedIncomeHolding>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);
        Assert.Equal(1, holding.UserId);
        Assert.Null(holding.MaturityDate);
        Assert.Null(holding.Liquidity);
        Assert.Null(holding.PrincipalAmount);
        Assert.Equal(1000m, holding.KnownBalance);
        Assert.Equal(SnapshotDate, holding.BalanceAsOfDate);
        Assert.Equal("test manual snapshot", holding.ValuationSource);

        var unvalued = await controller.Upsert(new FixedIncomeHoldingsController.FixedIncomeHoldingRequest(
            "RDB Teste sem saldo", null, "RDB", null, null, null, null, null, null, null, null, null, null), default);
        var unvaluedHolding = Assert.IsType<FixedIncomeHolding>(Assert.IsType<CreatedAtActionResult>(unvalued.Result).Value);
        Assert.Null(unvaluedHolding.KnownBalance);
        Assert.Null(unvaluedHolding.BalanceAsOfDate);

        var otherUserId = await db.FixedIncomeHoldings.Where(x => x.UserId == 2).Select(x => x.Id).SingleAsync();
        var crossUser = await controller.UpdateBalance(otherUserId, new(50m, SnapshotDate, "manual"), default);
        Assert.IsType<NotFoundResult>(crossUser.Result);

        var update = await controller.UpdateBalance(holding.Id, new(1010m, new DateOnly(2026, 1, 2), "bank statement"), default);
        var updated = Assert.IsType<FixedIncomeHolding>(Assert.IsType<OkObjectResult>(update.Result).Value);
        Assert.Equal(1010m, updated.KnownBalance);
        Assert.Equal(new DateOnly(2026, 1, 2), updated.BalanceAsOfDate);
        Assert.Equal("bank statement", updated.ValuationSource);
        Assert.Equal(0, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task BrapiProviderUsesConfiguredTokenCachesSuccessAndPreservesMarketTimestamp()
    {
        var requests = 0;
        var handler = new StubHandler((request, _) =>
        {
            requests++;
            Assert.Equal("Bearer test-key", request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[{\"symbol\":\"TEST11\",\"currency\":\"BRL\",\"regularMarketPrice\":9.87,\"regularMarketTime\":\"2026-01-01T18:00:00Z\"},{\"symbol\":\"TEST12\",\"currency\":\"BRL\",\"regularMarketPrice\":24.25,\"regularMarketTime\":\"2026-01-01T18:00:00Z\"}]}", Encoding.UTF8, "application/json")
            });
        });
        using var client = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BRAPI_API_KEY"] = "test-key" }).Build();
        var provider = new BrapiMarketQuoteProvider(client, cache, config);

        var first = await provider.GetQuotesAsync(["test11", "TEST12"], default);
        var second = await provider.GetQuoteAsync("TEST11", default);

        Assert.True(first["TEST11"].Success);
        Assert.True(first["TEST12"].Success);
        Assert.Equal(9.87m, first["TEST11"].Quote!.Price);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T18:00:00Z"), first["TEST11"].Quote!.AsOf);
        Assert.Equal("brapi", first["TEST11"].Quote!.Source);
        Assert.Same(first["TEST11"].Quote, second.Quote);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task BrapiProviderReportsUnavailableOrTimeoutWithoutQuotes()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var unconfigured = new BrapiMarketQuoteProvider(new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException())), cache,
            new ConfigurationBuilder().Build());
        Assert.Equal("provider_not_configured", (await unconfigured.GetQuoteAsync("TEST11", default)).ErrorCode);

        var timeoutClient = new HttpClient(new StubHandler((_, _) => throw new TaskCanceledException("timeout")));
        var configured = new BrapiMarketQuoteProvider(timeoutClient, cache,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BRAPI_API_KEY"] = "test-key" }).Build());
        var result = await configured.GetQuoteAsync("TEST11", default);
        Assert.False(result.Success);
        Assert.Equal("provider_timeout", result.ErrorCode);
        Assert.Null(result.Quote);
    }

    private static FiiHolding Fii(string ticker, decimal shares, decimal avgPrice, decimal currentPrice) => new()
    {
        UserId = 1,
        Ticker = ticker,
        Shares = shares,
        AvgPrice = avgPrice,
        CurrentPrice = currentPrice,
        QuoteAsOfDate = SnapshotDate,
        QuoteSource = "manual test snapshot"
    };

    private static FinancialInsightsService CreateService(AppDbContext db, MyFinance.API.Services.IFinancialSnapshotService snapshots) =>
        new(db, DataProtectionProvider.Create("FinflowInvestmentTests"), snapshots);

    private sealed class StubQuoteProvider(bool isConfigured, MarketQuoteResult result) : IMarketQuoteProvider
    {
        public bool IsConfigured => isConfigured;
        public Task<MarketQuoteResult> GetQuoteAsync(string ticker, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
