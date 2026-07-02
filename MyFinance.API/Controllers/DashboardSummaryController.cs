using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using MyFinance.API.Services;
using System.Diagnostics;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/dashboard")]
    [ApiController]
    [Authorize]
    public class DashboardSummaryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IFinancialSnapshotService _financialSnapshotService;
        private readonly ILogger<DashboardSummaryController> _logger;

        public DashboardSummaryController(
            AppDbContext context,
            IFinancialSnapshotService financialSnapshotService,
            ILogger<DashboardSummaryController> logger)
        {
            _context = context;
            _financialSnapshotService = financialSnapshotService;
            _logger = logger;
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
            var totalStopwatch = Stopwatch.StartNew();
            var stepStopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "Dashboard summary started. UserId: {UserId}, Month: {Month}, Year: {Year}",
                userId,
                month,
                year);

            var snapshot = await _financialSnapshotService.BuildUserSnapshotAsync(userId, DateTime.UtcNow, cancellationToken);
            var accounts = snapshot.Accounts;
            _logger.LogInformation(
                "Dashboard summary accounts query completed in {ElapsedMs} ms. UserId: {UserId}, Count: {Count}",
                stepStopwatch.ElapsedMilliseconds,
                userId,
                accounts.Count);

            stepStopwatch.Restart();
            var transactions = snapshot.Transactions
                .Where(t => t.Date >= startDate && t.Date < endDate)
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
                    t.IsTransfer,
                    t.Category == null
                        ? null
                        : new CategoryDto(t.Category.Id, t.Category.Name, t.Category.Type, t.Category.Icon, t.Category.Color)))
                .ToList();
            _logger.LogInformation(
                "Dashboard summary transactions query completed in {ElapsedMs} ms. UserId: {UserId}, Count: {Count}",
                stepStopwatch.ElapsedMilliseconds,
                userId,
                transactions.Count);

            stepStopwatch.Restart();
            var recurringRules = snapshot.RecurringRules
                .Select(r => new RecurringRuleDto(r.Id, r.Description, r.Amount, r.Type, r.DayOfMonth, r.AccountId))
                .ToList();
            _logger.LogInformation(
                "Dashboard summary recurring query completed in {ElapsedMs} ms. UserId: {UserId}, Count: {Count}",
                stepStopwatch.ElapsedMilliseconds,
                userId,
                recurringRules.Count);

            var accountSnapshots = snapshot.AccountSnapshots.ToDictionary(s => s.AccountId);
            var totalBalance = accountSnapshots.Values
                .Where(a => !a.IsCreditCard)
                .Sum(a => a.RealBalance);
            var pendingTotal = accountSnapshots.Values
                .Where(a => !a.IsCreditCard)
                .Sum(a => a.PendingBalance);
            var projectedTotal = accountSnapshots.Values
                .Where(a => !a.IsCreditCard)
                .Sum(a => a.ProjectedBalance);
            var cardLiability = accountSnapshots.Values
                .Where(a => a.IsCreditCard)
                .Sum(a => a.OutstandingLiability);
            var pendingCardLiability = accountSnapshots.Values
                .Where(a => a.IsCreditCard)
                .Sum(a => a.PendingLiability);
            var projectedCardLiability = accountSnapshots.Values
                .Where(a => a.IsCreditCard)
                .Sum(a => a.ProjectedLiability);

            var normalizedCardLiability = Math.Max(cardLiability, 0m);
            var normalizedPendingCardLiability = Math.Max(pendingCardLiability, 0m);
            var normalizedProjectedCardLiability = Math.Max(projectedCardLiability, 0m);
            var netWorth = totalBalance - normalizedCardLiability;
            var pendingNetWorth = pendingTotal - normalizedPendingCardLiability;
            var projectedNetWorth = projectedTotal - normalizedProjectedCardLiability;

            var totalIncome = transactions
                .Where(t => t.Type == "Income" && !t.IsTransfer && t.Paid)
                .Sum(t => t.Amount);

            var totalExpense = transactions
                .Where(t => t.Type == "Expense" && !t.IsTransfer && t.Paid)
                .Sum(t => t.Amount);

            var predictedFixed = recurringRules
                .Where(r => r.Type == "Expense" && (!r.AccountId.HasValue || accounts.First(a => a.Id == r.AccountId.Value).IsCreditCard == false))
                .Sum(r => r.Amount);
            var categorySummary = transactions
                .Where(t => t.Type == "Expense" && !t.IsTransfer)
                .GroupBy(t => new { Name = t.Category?.Name ?? "Outros", Color = t.Category?.Color ?? "#8c8c8c" })
                .Select(g => new CategorySummaryDto(g.Key.Name, g.Key.Color, g.Sum(t => t.Amount)))
                .OrderByDescending(g => g.Total)
                .ToList();

            stepStopwatch.Restart();
            var essentialBudgets = await _context.Budgets
                .AsNoTracking()
                .Where(b => b.UserId == userId && b.Month == month && b.Year == year && b.IsEssential)
                .ToListAsync(cancellationToken);
            var goals = await _context.FinancialGoals
                .AsNoTracking()
                .Where(g => g.UserId == userId)
                .ToListAsync(cancellationToken);
            var activeGoals = goals
                .Where(g => g.Status == "Active")
                .ToList();
            _logger.LogInformation(
                "Dashboard summary planning data completed in {ElapsedMs} ms. UserId: {UserId}, Budgets: {BudgetCount}, Goals: {GoalCount}",
                stepStopwatch.ElapsedMilliseconds,
                userId,
                essentialBudgets.Count,
                activeGoals.Count);

            var freeToSpend = BuildFreeToSpendSummary(
                snapshot,
                essentialBudgets,
                activeGoals,
                month,
                year);

            var nextMonth = month == 12 ? 1 : month + 1;
            var nextYear = month == 12 ? year + 1 : year;
            var projection = _financialSnapshotService.BuildProjection(
                snapshot.Accounts,
                snapshot.Transactions,
                snapshot.RecurringRules,
                DateTime.UtcNow,
                nextMonth,
                nextYear,
                6);

            var payload = new DashboardSummaryResponse(
                month,
                year,
                new DashboardSummaryDto(
                    totalBalance,
                    totalIncome,
                    totalExpense,
                    predictedFixed,
                    pendingTotal,
                    projectedTotal,
                    normalizedCardLiability,
                    normalizedPendingCardLiability,
                    normalizedProjectedCardLiability,
                    netWorth,
                    pendingNetWorth,
                    projectedNetWorth,
                    freeToSpend.FreeToSpendAmount),
                transactions,
                transactions.Take(5).ToList(),
                snapshot.Accounts
                    .Where(a => a.IsCreditCard)
                    .Select(a => new AccountSnapshotDto(
                        a.Id,
                        a.Name,
                        a.InitialBalance,
                        accountSnapshots[a.Id].RealBalance,
                        _financialSnapshotService.CalculateInvoiceAmount(a, snapshot.Transactions, DateTime.UtcNow.Month, DateTime.UtcNow.Year),
                        a.Type,
                        a.IsCreditCard,
                        a.CreditLimit,
                        a.ClosingDay,
                        a.DueDay,
                        accountSnapshots[a.Id].PendingBalance,
                        accountSnapshots[a.Id].ProjectedBalance,
                        accountSnapshots[a.Id].OutstandingLiability,
                        accountSnapshots[a.Id].PendingLiability,
                        accountSnapshots[a.Id].ProjectedLiability))
                    .ToList(),
                categorySummary,
                new ProjectionDto(
                    projection.StartBalance,
                    projection.Items.Select(item => new ProjectionItemDto(
                        item.Year,
                        item.Month,
                        item.Income,
                        item.Expense,
                        item.TransferImpact,
                        item.Net,
                        item.ProjectedBalance)).ToList()),
                freeToSpend,
                DateTime.UtcNow
            );

            totalStopwatch.Stop();
            _logger.LogInformation(
                "Dashboard summary finished in {ElapsedMs} ms. UserId: {UserId}, Month: {Month}, Year: {Year}",
                totalStopwatch.ElapsedMilliseconds,
                userId,
                month,
                year);

            return Ok(payload);
        }

        public sealed record DashboardSummaryResponse(
            int Month,
            int Year,
            DashboardSummaryDto Summary,
            List<TransactionSummaryDto> Transactions,
            List<TransactionSummaryDto> RecentTransactions,
            List<AccountSnapshotDto> Cards,
            List<CategorySummaryDto> CategorySummary,
            ProjectionDto Projection,
            FreeToSpendDto FreeToSpend,
            DateTime GeneratedAtUtc
        );

        public sealed record DashboardSummaryDto(
            decimal Total,
            decimal Income,
            decimal Expense,
            decimal PredictedFixed,
            decimal PendingTotal,
            decimal ProjectedTotal,
            decimal CardLiability,
            decimal PendingCardLiability,
            decimal ProjectedCardLiability,
            decimal NetWorth,
            decimal PendingNetWorth,
            decimal ProjectedNetWorth,
            decimal FreeToSpend);

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
            int? DueDay,
            decimal PendingBalance,
            decimal ProjectedBalance,
            decimal OutstandingLiability,
            decimal PendingLiability,
            decimal ProjectedLiability
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
            bool IsTransfer,
            CategoryDto? Category
        );

        public sealed record CategoryDto(int Id, string Name, string Type, string Icon, string Color);

        public sealed record CategorySummaryDto(string Name, string Color, decimal Total);

        public sealed record RecurringRuleDto(int Id, string Description, decimal Amount, string Type, int DayOfMonth, int? AccountId);

        public sealed record ProjectionDto(decimal StartBalance, List<ProjectionItemDto> Items);

        public sealed record ProjectionItemDto(
            int Year,
            int Month,
            decimal Income,
            decimal Expense,
            decimal TransferImpact,
            decimal Net,
            decimal ProjectedBalance
        );

        [HttpGet("free-to-spend")]
        public async Task<IActionResult> GetFreeToSpend([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
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
            var snapshot = await _financialSnapshotService.BuildUserSnapshotAsync(userId, DateTime.UtcNow, cancellationToken);
            var essentialBudgets = await _context.Budgets
                .AsNoTracking()
                .Where(b => b.UserId == userId && b.Month == month && b.Year == year && b.IsEssential)
                .ToListAsync(cancellationToken);
            var activeGoals = await _context.FinancialGoals
                .AsNoTracking()
                .Where(g => g.UserId == userId && g.Status == "Active")
                .ToListAsync(cancellationToken);

            return Ok(BuildFreeToSpendSummary(snapshot, essentialBudgets, activeGoals, month, year));
        }

        private FreeToSpendDto BuildFreeToSpendSummary(
            UserFinancialSnapshot snapshot,
            IReadOnlyCollection<Budget> essentialBudgets,
            IReadOnlyCollection<FinancialGoal> activeGoals,
            int month,
            int year)
        {
            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);
            var cashAccounts = snapshot.Accounts
                .Where(a => !a.IsCreditCard)
                .ToDictionary(a => a.Id);

            var confirmedIncome = snapshot.Transactions
                .Where(t =>
                    t.Date >= monthStart &&
                    t.Date < monthEnd &&
                    t.Type == "Income" &&
                    t.Paid &&
                    !t.IsTransfer &&
                    cashAccounts.ContainsKey(t.AccountId))
                .Sum(t => t.Amount);

            var predictedIncomeTransactions = snapshot.Transactions
                .Where(t =>
                    t.Date >= monthStart &&
                    t.Date < monthEnd &&
                    t.Type == "Income" &&
                    !t.Paid &&
                    !t.IsTransfer &&
                    t.RecurringRuleId.HasValue &&
                    cashAccounts.ContainsKey(t.AccountId))
                .Sum(t => t.Amount);

            var predictedExpenseTransactions = snapshot.Transactions
                .Where(t =>
                    t.Date >= monthStart &&
                    t.Date < monthEnd &&
                    t.Type == "Expense" &&
                    !t.Paid &&
                    !t.IsTransfer &&
                    t.RecurringRuleId.HasValue &&
                    cashAccounts.ContainsKey(t.AccountId))
                .Sum(t => t.Amount);

            var projectedRecurringIncome = snapshot.RecurringRules
                .Where(r =>
                    r.Type == "Income" &&
                    r.AccountId.HasValue &&
                    cashAccounts.ContainsKey(r.AccountId.Value) &&
                    !RecurringTransactionExists(snapshot.Transactions, r, year, month))
                .Sum(r => r.Amount);

            var projectedRecurringExpense = snapshot.RecurringRules
                .Where(r =>
                    r.Type == "Expense" &&
                    r.AccountId.HasValue &&
                    cashAccounts.ContainsKey(r.AccountId.Value) &&
                    !RecurringTransactionExists(snapshot.Transactions, r, year, month))
                .Sum(r => r.Amount);

            var goalsContribution = activeGoals.Sum(g => g.MonthlyContribution);
            var essentialBudgetAmount = essentialBudgets.Sum(b => b.Amount);
            var cardInvoices = snapshot.Accounts
                .Where(a => a.IsCreditCard)
                .Sum(a => _financialSnapshotService.CalculateInvoiceAmount(a, snapshot.Transactions, month, year));

            var consideredIncome = confirmedIncome + predictedIncomeTransactions + projectedRecurringIncome;
            var recurringExpenses = predictedExpenseTransactions + projectedRecurringExpense;
            var consideredOutflows = recurringExpenses + essentialBudgetAmount + goalsContribution + cardInvoices;
            var freeToSpendAmount = consideredIncome - consideredOutflows;

            return new FreeToSpendDto(
                freeToSpendAmount,
                confirmedIncome,
                predictedIncomeTransactions + projectedRecurringIncome,
                recurringExpenses,
                essentialBudgetAmount,
                goalsContribution,
                cardInvoices,
                freeToSpendAmount < 0,
                BuildFreeToSpendExplanation(
                    consideredIncome,
                    recurringExpenses,
                    essentialBudgetAmount,
                    goalsContribution,
                    cardInvoices,
                    freeToSpendAmount));
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

        private static string BuildFreeToSpendExplanation(
            decimal consideredIncome,
            decimal recurringExpenses,
            decimal essentialBudgetAmount,
            decimal goalsContribution,
            decimal cardInvoices,
            decimal freeToSpendAmount)
        {
            return
                $"Receitas consideradas: {consideredIncome:N2}. " +
                $"Despesas recorrentes previstas: {recurringExpenses:N2}. " +
                $"Orcamentos essenciais: {essentialBudgetAmount:N2}. " +
                $"Metas: {goalsContribution:N2}. " +
                $"Faturas/cartoes: {cardInvoices:N2}. " +
                $"Resultado livre para gastar: {freeToSpendAmount:N2}.";
        }

        public sealed record FreeToSpendDto(
            decimal FreeToSpendAmount,
            decimal ConfirmedIncome,
            decimal PredictedIncome,
            decimal RecurringExpenses,
            decimal EssentialBudgets,
            decimal GoalsContribution,
            decimal CardInvoices,
            bool IsNegative,
            string Explanation);
    }
}
