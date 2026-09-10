namespace MyFinance.API.Mcp;

public sealed record InsightMeta(string Currency, string Timezone, DateTimeOffset GeneratedAt, DateTime? DataAsOf, bool Partial);
public sealed record InsightEnvelope<T>(T Data, InsightMeta Meta);
public sealed record PeriodInput(DateTime From, DateTime To);
public sealed record FinancialSummary(decimal Income, decimal Expense, decimal Net, decimal? SavingsRate, int TransactionCount);
public sealed record PeriodComparison(FinancialSummary Current, FinancialSummary Previous, decimal IncomeDelta, decimal ExpenseDelta, decimal NetDelta);
public sealed record AccountBalance(int Id, string Name, string Type, decimal Balance, bool IsCreditCard);
public sealed record CategorySpend(int? CategoryId, string Category, decimal Amount, decimal Share, int TransactionCount);
public sealed record TransactionItem(int Id, DateTime Date, string Description, decimal Amount, string Type, bool Paid, int? CategoryId, string? Category, int AccountId, string Account, bool IsCreditCard);
public sealed record TransactionPage(IReadOnlyList<TransactionItem> Items, string? NextCursor, bool HasMore);
public sealed record RecurringExpense(int Id, string Description, decimal Amount, int DayOfMonth, int? CategoryId, string? Category, int? AccountId, string? Account);
public sealed record FinancialGoalItem(int Id, string Name, string GoalType, decimal TargetAmount, decimal CurrentAmount, decimal Progress, DateTime? TargetDate, decimal MonthlyContribution, string Status);
public sealed record InvestmentPosition(string Ticker, decimal Shares, decimal AveragePrice, decimal CostBasis, decimal? MarketValue);
