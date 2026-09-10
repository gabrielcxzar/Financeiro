using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;

namespace MyFinance.API.Mcp;

public interface IFinancialInsightsService
{
    Task<InsightEnvelope<FinancialSummary>> GetSummaryAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<InsightEnvelope<PeriodComparison>> ComparePeriodsAsync(int userId, PeriodInput current, PeriodInput previous, CancellationToken cancellationToken);
    Task<InsightEnvelope<IReadOnlyList<AccountBalance>>> GetAccountBalancesAsync(int userId, CancellationToken cancellationToken);
    Task<InsightEnvelope<IReadOnlyList<CategorySpend>>> GetSpendingByCategoryAsync(int userId, DateTime from, DateTime to, int limit, CancellationToken cancellationToken);
    Task<InsightEnvelope<TransactionPage>> GetTransactionsAsync(int userId, DateTime from, DateTime to, int limit, string? cursor, CancellationToken cancellationToken);
    Task<InsightEnvelope<IReadOnlyList<RecurringExpense>>> GetRecurringExpensesAsync(int userId, CancellationToken cancellationToken);
    Task<InsightEnvelope<IReadOnlyList<FinancialGoalItem>>> GetFinancialGoalsAsync(int userId, CancellationToken cancellationToken);
    Task<InsightEnvelope<IReadOnlyList<InvestmentPosition>>> GetInvestmentPositionsAsync(int userId, CancellationToken cancellationToken);
}

public sealed class FinancialInsightsService(AppDbContext db) : IFinancialInsightsService
{
    private const string Timezone = "America/Sao_Paulo";

    public async Task<InsightEnvelope<FinancialSummary>> GetSummaryAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        ValidatePeriod(from, to, 24);
        var rows = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= from && t.Date < to && t.ReportingKind == ReportingKinds.Normal && !t.IsTransfer && !t.ExcludeFromReports)
            .Select(t => new { t.Amount, t.Type })
            .ToListAsync(cancellationToken);
        var income = rows.Where(x => string.Equals(x.Type, "Income", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount);
        var expense = rows.Where(x => !string.Equals(x.Type, "Income", StringComparison.OrdinalIgnoreCase)).Sum(x => Math.Abs(x.Amount));
        var net = income - expense;
        decimal? savings = income == 0 ? null : decimal.Round(net / income * 100m, 2);
        return Envelope(new FinancialSummary(income, expense, net, savings, rows.Count), to.AddTicks(-1));
    }

    public async Task<InsightEnvelope<PeriodComparison>> ComparePeriodsAsync(int userId, PeriodInput current, PeriodInput previous, CancellationToken cancellationToken)
    {
        ValidatePeriod(current.From, current.To, 12);
        ValidatePeriod(previous.From, previous.To, 12);
        var a = await GetSummaryAsync(userId, current.From, current.To, cancellationToken);
        var b = await GetSummaryAsync(userId, previous.From, previous.To, cancellationToken);
        var data = new PeriodComparison(a.Data, b.Data, a.Data.Income - b.Data.Income, a.Data.Expense - b.Data.Expense, a.Data.Net - b.Data.Net);
        return Envelope(data, current.To.AddTicks(-1));
    }

    public async Task<InsightEnvelope<IReadOnlyList<AccountBalance>>> GetAccountBalancesAsync(int userId, CancellationToken cancellationToken)
    {
        var data = await db.Accounts.AsNoTracking().Where(a => a.UserId == userId)
            .OrderBy(a => a.Id).Select(a => new AccountBalance(a.Id, a.Name, a.Type, a.CurrentBalance, a.IsCreditCard)).ToListAsync(cancellationToken);
        return Envelope<IReadOnlyList<AccountBalance>>(data, null);
    }

    public async Task<InsightEnvelope<IReadOnlyList<CategorySpend>>> GetSpendingByCategoryAsync(int userId, DateTime from, DateTime to, int limit, CancellationToken cancellationToken)
    {
        ValidatePeriod(from, to, 24); limit = Math.Clamp(limit, 1, 50);
        var rows = await db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= from && t.Date < to && t.ReportingKind == ReportingKinds.Normal && !t.IsTransfer && !t.ExcludeFromReports && t.Type != "Income")
            .GroupBy(t => new { t.CategoryId, Name = t.Category == null ? "Sem categoria" : t.Category.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Amount = g.Sum(t => Math.Abs(t.Amount)), Count = g.Count() }).OrderByDescending(x => x.Amount).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var total = rows.Sum(x => x.Amount);
        rows = rows.Take(limit).ToList();
        return Envelope<IReadOnlyList<CategorySpend>>(rows.Select(x => new CategorySpend(x.CategoryId, x.Name, x.Amount, total == 0 ? 0 : decimal.Round(x.Amount / total * 100m, 2), x.Count)).ToList(), to.AddTicks(-1));
    }

    public async Task<InsightEnvelope<TransactionPage>> GetTransactionsAsync(int userId, DateTime from, DateTime to, int limit, string? cursor, CancellationToken cancellationToken)
    {
        if (from >= to || (to - from).TotalDays > 92) throw new ArgumentException("PERIOD_TOO_LARGE");
        limit = Math.Clamp(limit, 1, 100);
        var query = db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= from && t.Date < to && t.ReportingKind == ReportingKinds.Normal && !t.IsTransfer && !t.ExcludeFromReports);
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            try
            {
                var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|', 2);
                if (parts.Length != 2 || !DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var cursorDate) || !int.TryParse(parts[1], out var cursorId)) throw new FormatException();
                query = query.Where(t => t.Date < cursorDate || (t.Date == cursorDate && t.Id < cursorId));
            }
            catch (FormatException) { throw new ArgumentException("INVALID_CURSOR"); }
        }
        var ordered = query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
        var rows = await ordered.Take(limit + 1).Select(t => new TransactionItem(t.Id, t.Date, t.Description, t.Amount, t.Type, t.Paid, t.CategoryId, t.Category == null ? null : t.Category.Name, t.AccountId, t.Account == null ? "Conta desconhecida" : t.Account.Name, t.Account != null && t.Account.IsCreditCard)).ToListAsync(cancellationToken);
        var more = rows.Count > limit; if (more) rows.RemoveAt(rows.Count - 1);
        var next = more && rows.Count > 0 ? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{rows[^1].Date:O}|{rows[^1].Id}")) : null;
        return Envelope(new TransactionPage(rows, next, more), to.AddTicks(-1));
    }

    public async Task<InsightEnvelope<IReadOnlyList<RecurringExpense>>> GetRecurringExpensesAsync(int userId, CancellationToken cancellationToken)
    {
        var rows = await db.RecurringTransactions.AsNoTracking().Where(x => x.UserId == userId && x.Active && x.Type == "Expense").OrderBy(x => x.DayOfMonth).Select(x => new RecurringExpense(x.Id, x.Description, x.Amount, x.DayOfMonth, x.CategoryId, x.Category == null ? null : x.Category.Name, x.AccountId, x.Account == null ? null : x.Account.Name)).ToListAsync(cancellationToken);
        return Envelope<IReadOnlyList<RecurringExpense>>(rows, null);
    }

    public async Task<InsightEnvelope<IReadOnlyList<FinancialGoalItem>>> GetFinancialGoalsAsync(int userId, CancellationToken cancellationToken)
    {
        var rows = await db.FinancialGoals.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id).Select(x => new FinancialGoalItem(x.Id, x.Name, x.GoalType, x.TargetAmount, x.CurrentAmount, x.TargetAmount == 0 ? 0 : decimal.Round(x.CurrentAmount / x.TargetAmount * 100m, 2), x.TargetDate, x.MonthlyContribution, x.Status)).ToListAsync(cancellationToken);
        return Envelope<IReadOnlyList<FinancialGoalItem>>(rows, null);
    }

    public async Task<InsightEnvelope<IReadOnlyList<InvestmentPosition>>> GetInvestmentPositionsAsync(int userId, CancellationToken cancellationToken)
    {
        var rows = await db.FiiHoldings.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Ticker).Select(x => new InvestmentPosition(x.Ticker, x.Shares, x.AvgPrice, x.Shares * x.AvgPrice, null)).ToListAsync(cancellationToken);
        return Envelope<IReadOnlyList<InvestmentPosition>>(rows, null);
    }

    private static void ValidatePeriod(DateTime from, DateTime to, int maxMonths)
    {
        if (from >= to || (to - from).TotalDays > maxMonths * 31)
            throw new ArgumentException("PERIOD_TOO_LARGE");
    }

    private static InsightEnvelope<T> Envelope<T>(T data, DateTime? asOf) => new(data, new InsightMeta("BRL", Timezone, DateTimeOffset.UtcNow, asOf, false));
}
