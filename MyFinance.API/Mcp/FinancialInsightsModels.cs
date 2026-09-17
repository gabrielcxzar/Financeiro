using System.Text.Json.Serialization;

namespace MyFinance.API.Mcp;

public sealed record InsightMeta(string Currency, string Timezone, DateTimeOffset GeneratedAt, DateTime? DataAsOf, bool Partial, IReadOnlyList<string>? Warnings = null);
public sealed record InsightEnvelope<T>(T Data, InsightMeta Meta);
public sealed record PeriodInput(
    [property: JsonPropertyName("startDate")] DateTime StartDate,
    [property: JsonPropertyName("endDate")] DateTime EndDate)
{
    [JsonIgnore] public DateTime From => StartDate;
    [JsonIgnore] public DateTime To => EndDate;
}
public sealed record MonthlySummary(int Year, int Month, decimal Income, decimal Expense, decimal Net);
public sealed record FinancialSummary(
    [property: JsonPropertyName("income")] decimal Income,
    [property: JsonPropertyName("expenses")] decimal Expense,
    [property: JsonPropertyName("netCashFlow")] decimal Net,
    decimal? SavingsRate,
    int TransactionCount,
    IReadOnlyList<MonthlySummary> Monthly,
    IReadOnlyList<CategorySpend> TopSpendingCategories);
public sealed record PeriodComparison(
    [property: JsonPropertyName("base")] FinancialSummary Current,
    [property: JsonPropertyName("comparison")] FinancialSummary Previous,
    decimal IncomeDelta,
    decimal ExpenseDelta,
    decimal NetDelta);
public sealed record AccountBalance(int Id, string Name, string Type, decimal Balance, bool IsCreditCard, decimal PendingBalance = 0, decimal ProjectedBalance = 0, decimal OutstandingLiability = 0, decimal? CreditLimit = null);
public sealed record AccountBalanceTotals(decimal KnownCash, decimal KnownCardLiability, decimal KnownLedgerNetWorth);
public sealed record AccountBalancesData(DateTime AsOfDate, IReadOnlyList<AccountBalance> CashAccounts, IReadOnlyList<AccountBalance> CreditCards, AccountBalanceTotals Totals);
public sealed record CategorySpend(int? CategoryId, [property: JsonPropertyName("name")] string Category, decimal Amount, decimal Share, int TransactionCount);
public sealed record CategorySpendingData(DateTime StartDate, DateTime EndDate, decimal TotalExpenses, IReadOnlyList<CategorySpend> Categories);
public sealed record TransactionItem(int Id, DateTime Date, string Description, decimal Amount, string Type, bool Paid, int? CategoryId, string? Category, int AccountId, string Account, bool IsCreditCard);
public sealed record TransactionPage(IReadOnlyList<TransactionItem> Items, string? NextCursor, bool HasMore);
public sealed record RecurringExpense(int Id, string Description, decimal Amount, int DayOfMonth, int? CategoryId, string? Category, int? AccountId, string? Account);
public sealed record RecurringExpensesData(decimal MonthlyTotal, IReadOnlyList<RecurringExpense> Items);
public sealed record FinancialGoalItem(int Id, string Name, string GoalType, decimal TargetAmount, decimal CurrentAmount, decimal Progress, DateTime? TargetDate, decimal MonthlyContribution, string Status);
public sealed record FinancialGoalsData(decimal MonthlyContributionTotal, IReadOnlyList<FinancialGoalItem> Items);
public sealed record InvestmentPosition(string Ticker, decimal Shares, decimal AveragePrice, decimal CostBasis, decimal? MarketValue);
public sealed record InvestmentAccountPosition(int Id, string Name, decimal Balance);
public sealed record InvestmentPositionsData(IReadOnlyList<InvestmentAccountPosition> InvestmentAccounts, IReadOnlyList<InvestmentPosition> FiiHoldings, decimal TotalCostBasis, string Valuation);
