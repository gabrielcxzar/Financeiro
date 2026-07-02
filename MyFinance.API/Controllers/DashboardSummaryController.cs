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
                new DashboardSummaryDto(totalBalance, totalIncome, totalExpense, predictedFixed, pendingTotal, projectedTotal, cardLiability),
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
            DateTime GeneratedAtUtc
        );

        public sealed record DashboardSummaryDto(
            decimal Total,
            decimal Income,
            decimal Expense,
            decimal PredictedFixed,
            decimal PendingTotal,
            decimal ProjectedTotal,
            decimal CardLiability);

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
    }
}
