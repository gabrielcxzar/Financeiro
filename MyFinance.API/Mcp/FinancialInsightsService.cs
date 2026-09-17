using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text.Json;
using MyFinance.API.Data;
using MyFinance.API.Models;
using MyFinance.API.Services;

namespace MyFinance.API.Mcp;

public interface IFinancialInsightsService
{
    Task<InsightEnvelope<FinancialSummary>> GetSummaryAsync(int userId, DateTime from, DateTime to, bool includeMonthlyBreakdown, int topCategories, CancellationToken cancellationToken);
    Task<InsightEnvelope<PeriodComparison>> ComparePeriodsAsync(int userId, PeriodInput current, PeriodInput previous, CancellationToken cancellationToken);
    Task<InsightEnvelope<AccountBalancesData>> GetAccountBalancesAsync(int userId, bool includeProjected, bool includeCreditCards, CancellationToken cancellationToken);
    Task<InsightEnvelope<CategorySpendingData>> GetSpendingByCategoryAsync(int userId, DateTime from, DateTime to, int limit, bool includeUncategorized, CancellationToken cancellationToken);
    Task<InsightEnvelope<TransactionPage>> GetTransactionsAsync(int userId, DateTime from, DateTime to, int? accountId, int? categoryId, string? type, string? status, decimal? minAmount, decimal? maxAmount, bool includeNonOperational, int limit, string? cursor, CancellationToken cancellationToken);
    Task<InsightEnvelope<RecurringExpensesData>> GetRecurringExpensesAsync(int userId, int? accountId, int? categoryId, CancellationToken cancellationToken);
    Task<InsightEnvelope<FinancialGoalsData>> GetFinancialGoalsAsync(int userId, string? status, CancellationToken cancellationToken);
    Task<InsightEnvelope<InvestmentPositionsData>> GetInvestmentPositionsAsync(int userId, bool includeInvestmentAccounts, bool includeFiiHoldings, CancellationToken cancellationToken);
}

public sealed class FinancialInsightsService(AppDbContext db, IDataProtectionProvider dataProtection, IFinancialSnapshotService snapshots) : IFinancialInsightsService
{
    private const string Timezone = "America/Sao_Paulo";
    private readonly IDataProtector cursorProtector = dataProtection.CreateProtector("FinFlow.Mcp.Transactions.Cursor.v1");

    public async Task<InsightEnvelope<FinancialSummary>> GetSummaryAsync(int userId, DateTime from, DateTime to, bool includeMonthlyBreakdown, int topCategories, CancellationToken cancellationToken)
    {
        ValidatePeriod(from, to, 24);
        if (topCategories is < 0 or > 20) throw new ArgumentException("INVALID_ARGUMENT");
        var aggregate = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.Date >= from && t.Date < to)
            .Where(ReportingPolicy.OperationalPredicate)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Income = g.Where(t => t.Type.ToLower() == "income").Sum(t => t.Amount),
                Expense = g.Where(t => t.Type.ToLower() != "income").Sum(t => Math.Abs(t.Amount)),
                Count = g.Count()
            })
            .SingleOrDefaultAsync(cancellationToken);
        var income = aggregate?.Income ?? 0m;
        var expense = aggregate?.Expense ?? 0m;
        var net = income - expense;
        decimal? savings = income == 0 ? null : decimal.Round(net / income * 100m, 2);
        var monthly = includeMonthlyBreakdown
            ? (await db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= from && t.Date < to).Where(ReportingPolicy.OperationalPredicate)
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Income = g.Where(t => t.Type.ToLower() == "income").Sum(t => t.Amount),
                    Expense = g.Where(t => t.Type.ToLower() != "income").Sum(t => Math.Abs(t.Amount))
                })
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync(cancellationToken))
                .Select(x => new MonthlySummary(x.Year, x.Month, x.Income, x.Expense, x.Income - x.Expense))
                .ToList()
            : [];
        var top = topCategories == 0 ? [] : await db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= from && t.Date < to).Where(ReportingPolicy.OperationalPredicate).Where(t => t.Type.ToLower() != "income")
            .GroupBy(t => new { t.CategoryId, Name = t.Category == null ? "Sem categoria" : t.Category.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Amount = g.Sum(t => Math.Abs(t.Amount)), Count = g.Count() }).OrderByDescending(x => x.Amount).ThenBy(x => x.Name).Take(topCategories).ToListAsync(cancellationToken);
        var topDtos = top.Select(x => new CategorySpend(x.CategoryId, x.Name, x.Amount, expense == 0 ? 0 : decimal.Round(x.Amount / expense * 100m, 2), x.Count)).ToList();
        return Envelope(new FinancialSummary(income, expense, net, savings, aggregate?.Count ?? 0, monthly, topDtos), to.AddTicks(-1));
    }

    public async Task<InsightEnvelope<PeriodComparison>> ComparePeriodsAsync(int userId, PeriodInput current, PeriodInput previous, CancellationToken cancellationToken)
    {
        ValidatePeriod(current.From, current.To, 12);
        ValidatePeriod(previous.From, previous.To, 12);
        var a = await GetSummaryAsync(userId, current.From, current.To, false, 0, cancellationToken);
        var b = await GetSummaryAsync(userId, previous.From, previous.To, false, 0, cancellationToken);
        var data = new PeriodComparison(a.Data, b.Data, a.Data.Income - b.Data.Income, a.Data.Expense - b.Data.Expense, a.Data.Net - b.Data.Net);
        return Envelope(data, current.To.AddTicks(-1));
    }

    public async Task<InsightEnvelope<AccountBalancesData>> GetAccountBalancesAsync(int userId, bool includeProjected, bool includeCreditCards, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.AsNoTracking().Where(a => a.UserId == userId).OrderBy(a => a.Id).ToListAsync(cancellationToken);
        var snapshot = await snapshots.BuildUserSnapshotAsync(userId, DateTime.UtcNow, cancellationToken);
        var byId = snapshot.AccountSnapshots.ToDictionary(x => x.AccountId);
        var data = accounts.Where(a => includeCreditCards || !a.IsCreditCard).Select(a =>
        {
            byId.TryGetValue(a.Id, out var s);
            var pending = a.IsCreditCard ? s?.PendingLiability ?? 0m : s?.PendingBalance ?? 0m;
            var projected = a.IsCreditCard ? s?.ProjectedLiability ?? 0m : s?.ProjectedBalance ?? 0m;
            return new AccountBalance(a.Id, a.Name, a.Type, a.IsCreditCard ? s?.OutstandingLiability ?? 0m : s?.RealBalance ?? a.CurrentBalance, a.IsCreditCard, pending, includeProjected ? projected : 0m, s?.OutstandingLiability ?? 0m, a.CreditLimit);
        }).ToList();
        var cash = data.Where(x => !x.IsCreditCard).ToList();
        var cards = data.Where(x => x.IsCreditCard).ToList();
        var totals = new AccountBalanceTotals(cash.Sum(x => x.Balance), cards.Sum(x => x.OutstandingLiability), cash.Sum(x => x.Balance) - cards.Sum(x => x.OutstandingLiability));
        return Envelope(new AccountBalancesData(CivilToday(), cash, cards, totals), null);
    }

    public async Task<InsightEnvelope<CategorySpendingData>> GetSpendingByCategoryAsync(int userId, DateTime from, DateTime to, int limit, bool includeUncategorized, CancellationToken cancellationToken)
    {
        ValidatePeriod(from, to, 24); limit = Math.Clamp(limit, 1, 50);
        var rows = await db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= from && t.Date < to).Where(ReportingPolicy.OperationalPredicate).Where(t => t.Type.ToLower() != "income" && (includeUncategorized || t.CategoryId != null))
            .GroupBy(t => new { t.CategoryId, Name = t.Category == null ? "Sem categoria" : t.Category.Name })
            .Select(g => new { g.Key.CategoryId, g.Key.Name, Amount = g.Sum(t => Math.Abs(t.Amount)), Count = g.Count() }).OrderByDescending(x => x.Amount).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var total = rows.Sum(x => x.Amount);
        rows = rows.Take(limit).ToList();
        return Envelope(new CategorySpendingData(from.Date, to.AddTicks(-1).Date, total, rows.Select(x => new CategorySpend(x.CategoryId, x.Name, x.Amount, total == 0 ? 0 : decimal.Round(x.Amount / total * 100m, 2), x.Count)).ToList()), to.AddTicks(-1));
    }

    public async Task<InsightEnvelope<TransactionPage>> GetTransactionsAsync(int userId, DateTime from, DateTime to, int? accountId, int? categoryId, string? type, string? status, decimal? minAmount, decimal? maxAmount, bool includeNonOperational, int limit, string? cursor, CancellationToken cancellationToken)
    {
        if (from >= to || (to - from).TotalDays > 92) throw new ArgumentException("PERIOD_TOO_LARGE");
        limit = Math.Clamp(limit, 1, 100);
        if (accountId is <= 0 || categoryId is <= 0 || minAmount is < 0 || maxAmount is < 0 || minAmount > maxAmount) throw new ArgumentException("INVALID_ARGUMENT");
        if (type is not (null or "income" or "expense" or "all") || status is not (null or "paid" or "pending" or "all")) throw new ArgumentException("INVALID_ARGUMENT");
        var query = db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= from && t.Date < to);
        if (!includeNonOperational) query = query.Where(ReportingPolicy.OperationalPredicate);
        if (accountId.HasValue) query = query.Where(t => t.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(t => t.CategoryId == categoryId.Value);
        if (type == "income") query = query.Where(t => t.Type.ToLower() == "income");
        if (type == "expense") query = query.Where(t => t.Type.ToLower() != "income");
        if (status == "paid") query = query.Where(t => t.Paid);
        if (status == "pending") query = query.Where(t => !t.Paid);
        if (minAmount.HasValue) query = query.Where(t => Math.Abs(t.Amount) >= minAmount.Value);
        if (maxAmount.HasValue) query = query.Where(t => Math.Abs(t.Amount) <= maxAmount.Value);
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            try
            {
                var payload = cursorProtector.Unprotect(Convert.FromBase64String(cursor));
                var state = JsonSerializer.Deserialize<TransactionCursor>(payload) ?? throw new FormatException();
                if (state.UserId != userId || state.Binding != CursorBinding(from, to, accountId, categoryId, type, status, minAmount, maxAmount, includeNonOperational)) throw new FormatException();
                query = query.Where(t => t.Date < state.Date || (t.Date == state.Date && t.Id < state.Id));
            }
            catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException) { throw new ArgumentException("INVALID_CURSOR"); }
        }
        var ordered = query.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id);
        var rows = await ordered.Take(limit + 1).Select(t => new TransactionItem(t.Id, t.Date, t.Description, t.Amount, t.Type, t.Paid, t.CategoryId, t.Category == null ? null : t.Category.Name, t.AccountId, t.Account == null ? "Conta desconhecida" : t.Account.Name, t.Account != null && t.Account.IsCreditCard)).ToListAsync(cancellationToken);
        var more = rows.Count > limit; if (more) rows.RemoveAt(rows.Count - 1);
        var next = more && rows.Count > 0
            ? Convert.ToBase64String(cursorProtector.Protect(JsonSerializer.SerializeToUtf8Bytes(new TransactionCursor(userId, rows[^1].Date, rows[^1].Id, CursorBinding(from, to, accountId, categoryId, type, status, minAmount, maxAmount, includeNonOperational)))))
            : null;
        return Envelope(new TransactionPage(rows, next, more), to.AddTicks(-1));
    }

    public async Task<InsightEnvelope<RecurringExpensesData>> GetRecurringExpensesAsync(int userId, int? accountId, int? categoryId, CancellationToken cancellationToken)
    {
        if (accountId is <= 0 || categoryId is <= 0) throw new ArgumentException("INVALID_ARGUMENT");
        var query = db.RecurringTransactions.AsNoTracking().Where(x => x.UserId == userId && x.Active && x.Type.ToLower() == "expense");
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId.Value);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
        var rows = await query.OrderBy(x => x.DayOfMonth).Select(x => new RecurringExpense(x.Id, x.Description, x.Amount, x.DayOfMonth, x.CategoryId, x.Category == null ? null : x.Category.Name, x.AccountId, x.Account == null ? null : x.Account.Name)).ToListAsync(cancellationToken);
        return Envelope(new RecurringExpensesData(rows.Sum(x => x.Amount), rows), null);
    }

    public async Task<InsightEnvelope<FinancialGoalsData>> GetFinancialGoalsAsync(int userId, string? status, CancellationToken cancellationToken)
    {
        if (status is not (null or "active" or "paused" or "completed" or "all")) throw new ArgumentException("INVALID_ARGUMENT");
        var query = db.FinancialGoals.AsNoTracking().Where(x => x.UserId == userId);
        if (status is not (null or "all")) query = query.Where(x => x.Status.ToLower() == status);
        var rows = await query.OrderBy(x => x.Id).Select(x => new FinancialGoalItem(x.Id, x.Name, x.GoalType, x.TargetAmount, x.CurrentAmount, x.TargetAmount == 0 ? 0 : decimal.Round(x.CurrentAmount / x.TargetAmount * 100m, 2), x.TargetDate, x.MonthlyContribution, x.Status)).ToListAsync(cancellationToken);
        return Envelope(new FinancialGoalsData(rows.Sum(x => x.MonthlyContribution), rows), null);
    }

    public async Task<InsightEnvelope<InvestmentPositionsData>> GetInvestmentPositionsAsync(int userId, bool includeInvestmentAccounts, bool includeFiiHoldings, CancellationToken cancellationToken)
    {
        var accounts = includeInvestmentAccounts
            ? await db.Accounts.AsNoTracking().Where(x => x.UserId == userId && !x.IsCreditCard && x.Type == "Investment").OrderBy(x => x.Id).Select(x => new InvestmentAccountPosition(x.Id, x.Name, x.CurrentBalance)).ToListAsync(cancellationToken)
            : [];
        var holdings = includeFiiHoldings
            ? await db.FiiHoldings.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Ticker).Select(x => new InvestmentPosition(x.Ticker, x.Shares, x.AvgPrice, x.Shares * x.AvgPrice, null)).ToListAsync(cancellationToken)
            : [];
        return Envelope(new InvestmentPositionsData(accounts, holdings, holdings.Sum(x => x.CostBasis), "marketValue unavailable without persisted quotation"), null);
    }

    private static void ValidatePeriod(DateTime from, DateTime to, int maxMonths)
    {
        if (from.Date >= to.Date || from.Date < to.Date.AddMonths(-maxMonths))
            throw new ArgumentException("PERIOD_TOO_LARGE");
    }

    private static InsightEnvelope<T> Envelope<T>(T data, DateTime? asOf) => new(data, new InsightMeta("BRL", Timezone, DateTimeOffset.UtcNow, asOf, false, []));

    private static string CursorBinding(DateTime from, DateTime to, int? accountId, int? categoryId, string? type, string? status, decimal? minAmount, decimal? maxAmount, bool includeNonOperational)
    {
        var value = $"{from:O}|{to:O}|{accountId}|{categoryId}|{type}|{status}|{minAmount}|{maxAmount}|{includeNonOperational}";
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }

    private static DateTime CivilToday()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, Timezone).Date;
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "E. South America Standard Time").Date;
        }
    }

    private sealed record TransactionCursor(int UserId, DateTime Date, int Id, string Binding);
}
