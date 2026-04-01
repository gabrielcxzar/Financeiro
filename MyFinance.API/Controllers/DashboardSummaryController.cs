using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize]
    public class DashboardSummaryController : ControllerBase
    {
        private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

        public DashboardSummaryController(IDbContextFactory<AppDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
        {
            if (month < 1 || month > 12)
            {
                return BadRequest("Mes invalido.");
            }

            if (year < 2000 || year > 2100)
            {
                return BadRequest("Ano invalido.");
            }

            var userId = GetUserId();
            var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddMonths(1);

            var accountsTask = GetAccountsSnapshotAsync(userId, cancellationToken);
            var transactionsTask = GetMonthlyTransactionsAsync(userId, startDate, endDate, cancellationToken);
            var recurringTask = GetRecurringExpensesAsync(userId, cancellationToken);

            await Task.WhenAll(accountsTask, transactionsTask, recurringTask);

            var accounts = accountsTask.Result;
            var transactions = transactionsTask.Result;
            var recurringRules = recurringTask.Result;

            var totalBalance = accounts
                .Where(a => !a.IsCreditCard)
                .Sum(a => a.CurrentBalance);

            var totalIncome = transactions
                .Where(t => t.Type == "Income")
                .Sum(t => t.Amount);

            var totalExpense = transactions
                .Where(t => t.Type == "Expense")
                .Sum(t => t.Amount);

            var predictedFixed = recurringRules
                .Where(r => r.Type == "Expense")
                .Sum(r => r.Amount);
            var categorySummary = transactions
                .Where(t => t.Type == "Expense")
                .GroupBy(t => new { Name = t.Category?.Name ?? "Outros", Color = t.Category?.Color ?? "#8c8c8c" })
                .Select(g => new CategorySummaryDto(g.Key.Name, g.Key.Color, g.Sum(t => t.Amount)))
                .OrderByDescending(g => g.Total)
                .ToList();

            var nextMonth = month == 12 ? 1 : month + 1;
            var nextYear = month == 12 ? year + 1 : year;
            var projection = BuildProjection(accounts, recurringRules, nextMonth, nextYear, 6);

            var payload = new DashboardSummaryResponse(
                month,
                year,
                new DashboardSummaryDto(totalBalance, totalIncome, totalExpense, predictedFixed),
                transactions,
                transactions.Take(5).ToList(),
                accounts.Where(a => a.IsCreditCard).ToList(),
                categorySummary,
                projection,
                DateTime.UtcNow
            );

            return Ok(payload);
        }

        private async Task<List<AccountSnapshotDto>> GetAccountsSnapshotAsync(int userId, CancellationToken cancellationToken)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var accounts = await db.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);

            var creditCardAccounts = accounts.Where(a => a.IsCreditCard).ToList();
            var invoiceTotals = new Dictionary<int, decimal>();

            if (creditCardAccounts.Count > 0)
            {
                var today = DateTime.UtcNow;
                var ranges = new List<CardInvoiceRange>();

                foreach (var account in creditCardAccounts)
                {
                    var closingDay = account.ClosingDay ?? 1;
                    var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
                    var safeClosingDay = Math.Min(closingDay, daysInMonth);

                    var closeDate = new DateTime(today.Year, today.Month, safeClosingDay, 23, 59, 59, DateTimeKind.Utc);
                    if (today.Day >= safeClosingDay)
                    {
                        closeDate = closeDate.AddMonths(1);
                    }

                    var startDate = closeDate.AddMonths(-1).AddDays(1);
                    ranges.Add(new CardInvoiceRange(account.Id, startDate, closeDate));
                }

                var minStartDate = ranges.Min(r => r.StartDate);
                var maxCloseDate = ranges.Max(r => r.CloseDate);
                var cardIds = ranges.Select(r => r.AccountId).ToList();

                var transactions = await db.Transactions
                    .AsNoTracking()
                    .Where(t =>
                        t.UserId == userId &&
                        cardIds.Contains(t.AccountId) &&
                        t.Date >= minStartDate &&
                        t.Date <= maxCloseDate)
                    .Select(t => new
                    {
                        t.AccountId,
                        t.Date,
                        t.Type,
                        t.Amount
                    })
                    .ToListAsync(cancellationToken);

                invoiceTotals = ranges.ToDictionary(
                    range => range.AccountId,
                    range => transactions
                        .Where(t => t.AccountId == range.AccountId && t.Date >= range.StartDate && t.Date <= range.CloseDate)
                        .Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount)
                );
            }

            return accounts
                .Select(acc => new AccountSnapshotDto(
                    acc.Id,
                    acc.Name,
                    acc.InitialBalance,
                    acc.CurrentBalance,
                    invoiceTotals.GetValueOrDefault(acc.Id, 0m),
                    acc.Type,
                    acc.IsCreditCard,
                    acc.CreditLimit,
                    acc.ClosingDay,
                    acc.DueDay
                ))
                .ToList();
        }

        private async Task<List<TransactionSummaryDto>> GetMonthlyTransactionsAsync(
            int userId,
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await db.Transactions
                .AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= startDate && t.Date < endDate)
                .OrderByDescending(t => t.Date)
                .Select(t => new TransactionSummaryDto(
                    t.Id,
                    t.Description,
                    t.Amount,
                    t.Date,
                    t.Type,
                    t.Paid,
                    t.CategoryId,
                    t.AccountId,
                    t.InstallmentId,
                    t.Category == null
                        ? null
                        : new CategoryDto(t.Category.Id, t.Category.Name, t.Category.Type, t.Category.Icon, t.Category.Color)
                ))
                .ToListAsync(cancellationToken);
        }

        private async Task<List<RecurringRuleDto>> GetRecurringExpensesAsync(int userId, CancellationToken cancellationToken)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            return await db.RecurringTransactions
                .AsNoTracking()
                .Where(r => r.Active && r.UserId == userId)
                .Select(r => new RecurringRuleDto(r.Id, r.Description, r.Amount, r.Type, r.DayOfMonth))
                .ToListAsync(cancellationToken);
        }

        private static ProjectionDto BuildProjection(
            List<AccountSnapshotDto> accounts,
            List<RecurringRuleDto> recurringRules,
            int startMonth,
            int startYear,
            int months)
        {
            var startingBalance = accounts
                .Where(a => !a.IsCreditCard)
                .Sum(a => a.CurrentBalance);

            var monthlyExpense = recurringRules.Where(r => r.Type == "Expense").Sum(r => r.Amount);
            var monthlyIncome = recurringRules.Where(r => r.Type == "Income").Sum(r => r.Amount);
            var runningBalance = startingBalance;
            var items = new List<ProjectionItemDto>();
            var start = new DateTime(startYear, startMonth, 1);

            for (var i = 0; i < months; i++)
            {
                var date = start.AddMonths(i);
                var net = monthlyIncome - monthlyExpense;
                runningBalance += net;

                items.Add(new ProjectionItemDto(
                    date.Year,
                    date.Month,
                    monthlyIncome,
                    monthlyExpense,
                    net,
                    runningBalance
                ));
            }

            return new ProjectionDto(startingBalance, items);
        }

        private sealed record CardInvoiceRange(int AccountId, DateTime StartDate, DateTime CloseDate);

        public sealed record DashboardSummaryResponse(
            int Month,
            int Year,
            DashboardSummaryDto Summary,
            List<TransactionSummaryDto> Transactions,
            List<TransactionSummaryDto> RecentTransactions,
            List<AccountSnapshotDto> Cards,
            List<CategorySummaryDto> CategorySummary,
            ProjectionDto Projection,
            DateTime GeneratedAtUtc
        );

        public sealed record DashboardSummaryDto(decimal Total, decimal Income, decimal Expense, decimal PredictedFixed);

        public sealed record AccountSnapshotDto(
            int Id,
            string Name,
            decimal InitialBalance,
            decimal CurrentBalance,
            decimal InvoiceAmount,
            string Type,
            bool IsCreditCard,
            decimal? CreditLimit,
            int? ClosingDay,
            int? DueDay
        );

        public sealed record TransactionSummaryDto(
            int Id,
            string Description,
            decimal Amount,
            DateTime Date,
            string Type,
            bool Paid,
            int? CategoryId,
            int AccountId,
            string? InstallmentId,
            CategoryDto? Category
        );

        public sealed record CategoryDto(int Id, string Name, string Type, string Icon, string Color);

        public sealed record CategorySummaryDto(string Name, string Color, decimal Total);

        public sealed record RecurringRuleDto(int Id, string Description, decimal Amount, string Type, int DayOfMonth);

        public sealed record ProjectionDto(decimal StartBalance, List<ProjectionItemDto> Items);

        public sealed record ProjectionItemDto(
            int Year,
            int Month,
            decimal Income,
            decimal Expense,
            decimal Net,
            decimal ProjectedBalance
        );
    }
}
