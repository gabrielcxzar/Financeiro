using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;

namespace MyFinance.API.Mcp;

public interface IFinancialInsightsService
{
    Task<InsightEnvelope<FinancialSummary>> GetSummaryAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken);
    Task<InsightEnvelope<PeriodComparison>> ComparePeriodsAsync(int userId, PeriodInput current, PeriodInput previous, CancellationToken cancellationToken);
    Task<InsightEnvelope<IReadOnlyList<AccountBalance>>> GetAccountBalancesAsync(int userId, CancellationToken cancellationToken);
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
        var savings = income == 0 ? 0 : decimal.Round(net / income * 100m, 2);
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

    private static void ValidatePeriod(DateTime from, DateTime to, int maxMonths)
    {
        if (from >= to || from.Kind == DateTimeKind.Unspecified && to.Kind == DateTimeKind.Unspecified && (to - from).TotalDays > maxMonths * 31)
            throw new ArgumentException("PERIOD_TOO_LARGE");
    }

    private static InsightEnvelope<T> Envelope<T>(T data, DateTime? asOf) => new(data, new InsightMeta("BRL", Timezone, DateTimeOffset.UtcNow, asOf, false));
}
