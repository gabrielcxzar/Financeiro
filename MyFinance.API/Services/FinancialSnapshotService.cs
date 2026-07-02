using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;

namespace MyFinance.API.Services;

public interface IFinancialSnapshotService
{
    Task RecalculateAccountBalancesAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserFinancialSnapshot> BuildUserSnapshotAsync(int userId, DateTime asOfUtc, CancellationToken cancellationToken = default);
    ProjectionSnapshot BuildProjection(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Transaction> transactions,
        IReadOnlyCollection<RecurringTransaction> recurringRules,
        DateTime asOfUtc,
        int startMonth,
        int startYear,
        int months);
    InvoiceWindow GetInvoiceWindow(Account account, int month, int year);
    decimal CalculateInvoiceAmount(Account account, IEnumerable<Transaction> transactions, int month, int year);
}

public sealed class FinancialSnapshotService(AppDbContext context) : IFinancialSnapshotService
{
    private readonly AppDbContext _context = context;

    public async Task RecalculateAccountBalancesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _context.Accounts
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
        {
            return;
        }

        var transactions = await _context.Transactions
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        var snapshots = BuildAccountSnapshots(accounts, transactions, DateTime.UtcNow);
        var changed = false;

        foreach (var account in accounts)
        {
            var snapshot = snapshots.Single(s => s.AccountId == account.Id);
            var expectedBalance = account.IsCreditCard ? 0m : snapshot.RealBalance;
            if (account.CurrentBalance != expectedBalance)
            {
                account.CurrentBalance = expectedBalance;
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserFinancialSnapshot> BuildUserSnapshotAsync(int userId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync(cancellationToken);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        var recurringRules = await _context.RecurringTransactions
            .AsNoTracking()
            .Where(r => r.Active && r.UserId == userId)
            .ToListAsync(cancellationToken);

        var snapshots = BuildAccountSnapshots(accounts, transactions, asOfUtc);
        return new UserFinancialSnapshot(accounts, transactions, recurringRules, snapshots);
    }

    public ProjectionSnapshot BuildProjection(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Transaction> transactions,
        IReadOnlyCollection<RecurringTransaction> recurringRules,
        DateTime asOfUtc,
        int startMonth,
        int startYear,
        int months)
    {
        if (months < 1)
        {
            months = 1;
        }

        var accountSnapshots = BuildAccountSnapshots(accounts, transactions, asOfUtc);
        var startBalance = accountSnapshots
            .Where(a => !a.IsCreditCard)
            .Sum(a => a.RealBalance);

        var runningBalance = startBalance;
        var start = new DateTime(startYear, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new List<ProjectionMonthSnapshot>();

        for (var offset = 0; offset < months; offset++)
        {
            var monthStart = start.AddMonths(offset);
            var monthEnd = monthStart.AddMonths(1);
            var explicitStart = monthStart.Year == asOfUtc.Year && monthStart.Month == asOfUtc.Month
                ? asOfUtc
                : monthStart;

            decimal income = 0;
            decimal expense = 0;
            decimal transferImpact = 0;
            var monthTransactions = transactions
                .Where(t => t.Date >= explicitStart && t.Date < monthEnd)
                .ToList();

            foreach (var transaction in monthTransactions.Where(t => !IsCreditCardAccount(accounts, t.AccountId)))
            {
                if (transaction.IsTransfer)
                {
                    if (ShouldExcludeTransferFromCashFlow(accounts, monthTransactions, transaction))
                    {
                        continue;
                    }

                    transferImpact += CashSignedAmount(transaction);
                    continue;
                }

                if (transaction.Type == "Income")
                {
                    income += transaction.Amount;
                }
                else
                {
                    expense += transaction.Amount;
                }
            }

            foreach (var rule in recurringRules.Where(r => r.AccountId.HasValue && !IsCreditCardAccount(accounts, r.AccountId.Value)))
            {
                var scheduledDay = Math.Min(rule.DayOfMonth, DateTime.DaysInMonth(monthStart.Year, monthStart.Month));
                var scheduledDate = new DateTime(monthStart.Year, monthStart.Month, scheduledDay, 12, 0, 0, DateTimeKind.Utc);
                if (scheduledDate < explicitStart)
                {
                    continue;
                }

                if (RecurringTransactionExists(transactions, rule, monthStart.Year, monthStart.Month))
                {
                    continue;
                }

                if (rule.Type == "Income")
                {
                    income += rule.Amount;
                }
                else
                {
                    expense += rule.Amount;
                }
            }

            var net = income - expense + transferImpact;
            runningBalance += net;
            items.Add(new ProjectionMonthSnapshot(monthStart.Year, monthStart.Month, income, expense, transferImpact, net, runningBalance));
        }

        return new ProjectionSnapshot(startBalance, items);
    }

    public InvoiceWindow GetInvoiceWindow(Account account, int month, int year)
    {
        var closingDay = account.ClosingDay ?? 1;
        var dueDay = account.DueDay ?? 10;
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var safeClosingDay = Math.Min(closingDay, daysInMonth);
        var closeDate = new DateTime(year, month, safeClosingDay, 23, 59, 59, DateTimeKind.Utc);
        var startDate = closeDate.AddMonths(-1).AddDays(1);

        DateTime dueDate;
        if (dueDay < closingDay)
        {
            var nextMonthDate = closeDate.AddMonths(1);
            var daysInNextMonth = DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month);
            var safeDueDay = Math.Min(dueDay, daysInNextMonth);
            dueDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, safeDueDay, 12, 0, 0, DateTimeKind.Utc);
        }
        else
        {
            var safeDueDay = Math.Min(dueDay, daysInMonth);
            dueDate = new DateTime(year, month, safeDueDay, 12, 0, 0, DateTimeKind.Utc);
        }

        return new InvoiceWindow(startDate, closeDate, dueDate);
    }

    public decimal CalculateInvoiceAmount(Account account, IEnumerable<Transaction> transactions, int month, int year)
    {
        var window = GetInvoiceWindow(account, month, year);
        return transactions
            .Where(t =>
                t.AccountId == account.Id &&
                t.Date >= window.StartDate &&
                t.Date <= window.CloseDate &&
                !t.IsTransfer)
            .Sum(CardSignedAmount);
    }

    private static bool IsCreditCardAccount(IReadOnlyCollection<Account> accounts, int accountId)
    {
        return accounts.FirstOrDefault(a => a.Id == accountId)?.IsCreditCard == true;
    }

    private static bool RecurringTransactionExists(IEnumerable<Transaction> transactions, RecurringTransaction rule, int year, int month)
    {
        return transactions.Any(t =>
                   t.UserId == rule.UserId &&
                   t.Date.Year == year &&
                   t.Date.Month == month &&
                   (t.RecurringRuleId == rule.Id ||
                    (t.AccountId == rule.AccountId &&
                     t.CategoryId == rule.CategoryId &&
                     t.Type == rule.Type &&
                     t.Amount == rule.Amount &&
                     t.Description == rule.Description)));
    }

    private static bool ShouldExcludeTransferFromCashFlow(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Transaction> transactions,
        Transaction transaction)
    {
        if (!transaction.IsTransfer || string.IsNullOrWhiteSpace(transaction.TransferGroupId))
        {
            return false;
        }

        var group = transactions
            .Where(t => t.TransferGroupId == transaction.TransferGroupId)
            .ToList();

        if (group.Count != 2)
        {
            return false;
        }

        return group.All(t => !IsCreditCardAccount(accounts, t.AccountId));
    }

    private static decimal CashSignedAmount(Transaction transaction)
    {
        return transaction.Type == "Income" ? transaction.Amount : -transaction.Amount;
    }

    private static decimal CardSignedAmount(Transaction transaction)
    {
        return transaction.Type == "Expense" ? transaction.Amount : -transaction.Amount;
    }

    private static List<AccountFinancialSnapshot> BuildAccountSnapshots(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Transaction> transactions,
        DateTime asOfUtc)
    {
        var snapshots = new List<AccountFinancialSnapshot>(accounts.Count);

        foreach (var account in accounts)
        {
            var accountTransactions = transactions
                .Where(t => t.AccountId == account.Id)
                .ToList();

            if (account.IsCreditCard)
            {
                var realLiability = accountTransactions
                    .Where(t => t.Paid && t.Date <= asOfUtc)
                    .Sum(CardSignedAmount);
                var pendingLiability = realLiability + accountTransactions
                    .Where(t => !t.Paid && t.Date <= asOfUtc)
                    .Sum(CardSignedAmount);
                var projectedLiability = pendingLiability + accountTransactions
                    .Where(t => t.Date > asOfUtc)
                    .Sum(CardSignedAmount);

                snapshots.Add(new AccountFinancialSnapshot(
                    account.Id,
                    account.IsCreditCard,
                    0m,
                    0m,
                    0m,
                    realLiability,
                    pendingLiability,
                    projectedLiability));
                continue;
            }

            var realBalance = account.InitialBalance + accountTransactions
                .Where(t => t.Paid && t.Date <= asOfUtc)
                .Sum(CashSignedAmount);
            var pendingBalance = realBalance + accountTransactions
                .Where(t => !t.Paid && t.Date <= asOfUtc)
                .Sum(CashSignedAmount);
            var projectedBalance = pendingBalance + accountTransactions
                .Where(t => t.Date > asOfUtc)
                .Sum(CashSignedAmount);

            snapshots.Add(new AccountFinancialSnapshot(
                account.Id,
                account.IsCreditCard,
                realBalance,
                pendingBalance,
                projectedBalance,
                0m,
                0m,
                0m));
        }

        return snapshots;
    }
}

public sealed record UserFinancialSnapshot(
    IReadOnlyCollection<Account> Accounts,
    IReadOnlyCollection<Transaction> Transactions,
    IReadOnlyCollection<RecurringTransaction> RecurringRules,
    IReadOnlyCollection<AccountFinancialSnapshot> AccountSnapshots);

public sealed record AccountFinancialSnapshot(
    int AccountId,
    bool IsCreditCard,
    decimal RealBalance,
    decimal PendingBalance,
    decimal ProjectedBalance,
    decimal OutstandingLiability,
    decimal PendingLiability,
    decimal ProjectedLiability);

public sealed record ProjectionSnapshot(decimal StartBalance, IReadOnlyCollection<ProjectionMonthSnapshot> Items);

public sealed record ProjectionMonthSnapshot(
    int Year,
    int Month,
    decimal Income,
    decimal Expense,
    decimal TransferImpact,
    decimal Net,
    decimal ProjectedBalance);

public sealed record InvoiceWindow(DateTime StartDate, DateTime CloseDate, DateTime DueDate);
