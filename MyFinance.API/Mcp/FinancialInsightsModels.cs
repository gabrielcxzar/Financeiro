namespace MyFinance.API.Mcp;

public sealed record InsightMeta(string Currency, string Timezone, DateTimeOffset GeneratedAt, DateTime? DataAsOf, bool Partial);
public sealed record InsightEnvelope<T>(T Data, InsightMeta Meta);
public sealed record PeriodInput(DateTime From, DateTime To);
public sealed record FinancialSummary(decimal Income, decimal Expense, decimal Net, decimal SavingsRate, int TransactionCount);
public sealed record PeriodComparison(FinancialSummary Current, FinancialSummary Previous, decimal IncomeDelta, decimal ExpenseDelta, decimal NetDelta);
public sealed record AccountBalance(int Id, string Name, string Type, decimal Balance, bool IsCreditCard);
