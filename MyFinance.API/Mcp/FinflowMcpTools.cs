using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;

namespace MyFinance.API.Mcp;

[McpServerToolType]
public sealed class FinflowMcpTools(IFinancialInsightsService insights, IHttpContextAccessor http, McpDetailRateLimiter detailRateLimiter)
{
    [McpServerTool(Name = "get_financial_summary", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Retorna resumo financeiro agregado do usuário autenticado.")]
    public Task<InsightEnvelope<FinancialSummary>> GetFinancialSummary(DateTime? startDate = null, DateTime? endDate = null, bool includeMonthlyBreakdown = false, int topCategories = 5, CancellationToken cancellationToken = default)
    {
        var start = startDate?.Date ?? CurrentCivilMonthStart();
        var end = endDate.HasValue ? InclusiveEnd(endDate.Value) : start.AddMonths(1);
        return insights.GetSummaryAsync(UserId(), start, end, includeMonthlyBreakdown, topCategories, cancellationToken);
    }

    [McpServerTool(Name = "compare_periods", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Compara dois períodos financeiros do usuário autenticado.")]
    public Task<InsightEnvelope<PeriodComparison>> ComparePeriods(PeriodInput basePeriod, PeriodInput comparisonPeriod, CancellationToken cancellationToken)
        => insights.ComparePeriodsAsync(UserId(),
            basePeriod with { EndDate = InclusiveEnd(basePeriod.EndDate) },
            comparisonPeriod with { EndDate = InclusiveEnd(comparisonPeriod.EndDate) },
            cancellationToken);

    [McpServerTool(Name = "get_account_balances", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Retorna saldos conhecidos das contas do usuário autenticado.")]
    public Task<InsightEnvelope<AccountBalancesData>> GetAccountBalances(bool includeProjected = true, bool includeCreditCards = true, CancellationToken cancellationToken = default)
        => insights.GetAccountBalancesAsync(UserId(), includeProjected, includeCreditCards, cancellationToken);

    [McpServerTool(Name = "get_spending_by_category", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Agrega despesas por categoria do usuário autenticado.")]
    public Task<InsightEnvelope<CategorySpendingData>> GetSpendingByCategory(DateTime startDate, DateTime endDate, int limit = 10, bool includeUncategorized = true, CancellationToken cancellationToken = default)
        => insights.GetSpendingByCategoryAsync(UserId(), startDate.Date, InclusiveEnd(endDate), limit, includeUncategorized, cancellationToken);

    [McpServerTool(Name = "get_transactions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Lista transações do período informado com paginação limitada.")]
    public Task<InsightEnvelope<TransactionPage>> GetTransactions(DateTime startDate, DateTime endDate, int? accountId = null, int? categoryId = null, string? type = "all", string? status = "all", decimal? minAmount = null, decimal? maxAmount = null, bool includeNonOperational = false, int limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
    {
        var subject = http.HttpContext?.User.FindFirstValue("sub") ?? http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        if (!detailRateLimiter.TryAcquire(subject)) throw new McpRateLimitException();
        return insights.GetTransactionsAsync(UserId(), startDate.Date, InclusiveEnd(endDate), accountId, categoryId, type, status, minAmount, maxAmount, includeNonOperational, limit, cursor, cancellationToken);
    }

    [McpServerTool(Name = "get_recurring_expenses", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Retorna despesas recorrentes ativas.")]
    public Task<InsightEnvelope<RecurringExpensesData>> GetRecurringExpenses(int? accountId = null, int? categoryId = null, CancellationToken cancellationToken = default)
        => insights.GetRecurringExpensesAsync(UserId(), accountId, categoryId, cancellationToken);

    [McpServerTool(Name = "get_financial_goals", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Retorna metas financeiras do usuário autenticado.")]
    public Task<InsightEnvelope<FinancialGoalsData>> GetFinancialGoals(string? status = "active", CancellationToken cancellationToken = default)
        => insights.GetFinancialGoalsAsync(UserId(), status, cancellationToken);

    [McpServerTool(Name = "get_investment_positions", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true), Description("Retorna posições de investimento conhecidas sem inventar cotação de mercado.")]
    public Task<InsightEnvelope<InvestmentPositionsData>> GetInvestmentPositions(bool includeInvestmentAccounts = true, bool includeFiiHoldings = true, CancellationToken cancellationToken = default)
        => insights.GetInvestmentPositionsAsync(UserId(), includeInvestmentAccounts, includeFiiHoldings, cancellationToken);

    private int UserId()
    {
        var value = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.HttpContext?.User.FindFirstValue("sub");
        return int.TryParse(value, out var id) && id > 0 ? id : throw new UnauthorizedAccessException();
    }

    private static DateTime InclusiveEnd(DateTime value) => value.TimeOfDay == TimeSpan.Zero ? value.Date.AddDays(1) : value;

    private static DateTime CurrentCivilMonthStart()
    {
        var utcNow = DateTime.UtcNow;
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }

        var civilNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
        return new DateTime(civilNow.Year, civilNow.Month, 1);
    }
}
