using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GoalsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GoalsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<FinancialGoalDto>>> GetGoals(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var goals = await _context.FinancialGoals
                .AsNoTracking()
                .Include(g => g.LinkedAccount)
                .Where(g => g.UserId == userId)
                .OrderBy(g => g.Status == "Active" ? 0 : g.Status == "Paused" ? 1 : 2)
                .ThenBy(g => g.TargetDate ?? DateTime.MaxValue)
                .ThenBy(g => g.Name)
                .ToListAsync(cancellationToken);

            return goals.Select(MapGoal).ToList();
        }

        [HttpPost]
        public async Task<ActionResult<FinancialGoalDto>> PostGoal(UpsertFinancialGoalDto request, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var validation = await ValidateGoalRequestAsync(request, userId, cancellationToken);
            if (validation != null)
            {
                return validation;
            }

            var goal = new FinancialGoal
            {
                Name = request.Name.Trim(),
                GoalType = NormalizeGoalType(request.GoalType),
                TargetAmount = request.TargetAmount,
                CurrentAmount = request.CurrentAmount,
                TargetDate = request.TargetDate?.ToUniversalTime(),
                MonthlyContribution = request.MonthlyContribution,
                Status = NormalizeGoalStatus(request.Status, request.CurrentAmount, request.TargetAmount),
                LinkedAccountId = request.LinkedAccountId,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                UserId = userId
            };

            _context.FinancialGoals.Add(goal);
            await _context.SaveChangesAsync(cancellationToken);
            await _context.Entry(goal).Reference(g => g.LinkedAccount).LoadAsync(cancellationToken);

            return CreatedAtAction(nameof(GetGoals), new { id = goal.Id }, MapGoal(goal));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutGoal(int id, UpsertFinancialGoalDto request, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var validation = await ValidateGoalRequestAsync(request, userId, cancellationToken);
            if (validation != null)
            {
                return validation;
            }

            var goal = await _context.FinancialGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);

            if (goal == null)
            {
                return NotFound();
            }

            goal.Name = request.Name.Trim();
            goal.GoalType = NormalizeGoalType(request.GoalType);
            goal.TargetAmount = request.TargetAmount;
            goal.CurrentAmount = request.CurrentAmount;
            goal.TargetDate = request.TargetDate?.ToUniversalTime();
            goal.MonthlyContribution = request.MonthlyContribution;
            goal.Status = NormalizeGoalStatus(request.Status, request.CurrentAmount, request.TargetAmount);
            goal.LinkedAccountId = request.LinkedAccountId;
            goal.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var goal = await _context.FinancialGoals
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId, cancellationToken);

            if (goal == null)
            {
                return NotFound();
            }

            _context.FinancialGoals.Remove(goal);
            await _context.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private async Task<ActionResult?> ValidateGoalRequestAsync(UpsertFinancialGoalDto request, int userId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Nome obrigatorio.");
            }

            if (request.TargetAmount <= 0)
            {
                return BadRequest("Valor alvo deve ser maior que zero.");
            }

            if (request.CurrentAmount < 0)
            {
                return BadRequest("Valor atual nao pode ser negativo.");
            }

            if (request.CurrentAmount > request.TargetAmount)
            {
                return BadRequest("Valor atual nao pode ser maior que o valor alvo.");
            }

            if (request.MonthlyContribution < 0)
            {
                return BadRequest("Contribuicao mensal nao pode ser negativa.");
            }

            if (!IsAllowedGoalType(request.GoalType))
            {
                return BadRequest("Tipo de meta invalido.");
            }

            if (!IsAllowedGoalStatus(request.Status))
            {
                return BadRequest("Status invalido.");
            }

            if (request.TargetDate.HasValue && request.TargetDate.Value.Year is < 2000 or > 2100)
            {
                return BadRequest("Prazo final invalido.");
            }

            if (request.LinkedAccountId.HasValue)
            {
                var accountExists = await _context.Accounts
                    .AnyAsync(a => a.Id == request.LinkedAccountId.Value && a.UserId == userId, cancellationToken);

                if (!accountExists)
                {
                    return BadRequest("Conta vinculada invalida.");
                }
            }

            return null;
        }

        private static FinancialGoalDto MapGoal(FinancialGoal goal)
        {
            var progress = goal.TargetAmount <= 0 ? 0m : Math.Round((goal.CurrentAmount / goal.TargetAmount) * 100m, 2);
            var remainingAmount = Math.Max(goal.TargetAmount - goal.CurrentAmount, 0m);
            var suggestedContribution = CalculateSuggestedContribution(goal.TargetDate, remainingAmount);

            return new FinancialGoalDto(
                goal.Id,
                goal.Name,
                goal.GoalType,
                goal.TargetAmount,
                goal.CurrentAmount,
                remainingAmount,
                Math.Min(progress, 100m),
                goal.TargetDate,
                goal.MonthlyContribution,
                suggestedContribution,
                goal.Status,
                goal.LinkedAccountId,
                goal.LinkedAccount == null ? null : new LinkedAccountDto(goal.LinkedAccount.Id, goal.LinkedAccount.Name, goal.LinkedAccount.IsCreditCard),
                goal.Notes
            );
        }

        private static decimal? CalculateSuggestedContribution(DateTime? targetDate, decimal remainingAmount)
        {
            if (!targetDate.HasValue || remainingAmount <= 0)
            {
                return null;
            }

            var today = DateTime.UtcNow.Date;
            var target = targetDate.Value.Date;
            var months = ((target.Year - today.Year) * 12) + target.Month - today.Month + 1;
            if (months <= 0)
            {
                return remainingAmount;
            }

            return Math.Round(remainingAmount / months, 2, MidpointRounding.AwayFromZero);
        }

        private static string NormalizeGoalType(string? goalType)
        {
            return string.Equals(goalType, "DebtPaydown", StringComparison.OrdinalIgnoreCase)
                ? "DebtPaydown"
                : "Saving";
        }

        private static string NormalizeGoalStatus(string? status, decimal currentAmount, decimal targetAmount)
        {
            if (currentAmount >= targetAmount)
            {
                return "Completed";
            }

            if (string.Equals(status, "Paused", StringComparison.OrdinalIgnoreCase))
            {
                return "Paused";
            }

            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return "Completed";
            }

            return "Active";
        }

        private static bool IsAllowedGoalType(string? goalType)
        {
            return string.Equals(goalType, "Saving", StringComparison.OrdinalIgnoreCase)
                || string.Equals(goalType, "DebtPaydown", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedGoalStatus(string? status)
        {
            return string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Paused", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
        }

        public sealed record UpsertFinancialGoalDto(
            string Name,
            string GoalType,
            decimal TargetAmount,
            decimal CurrentAmount,
            DateTime? TargetDate,
            decimal MonthlyContribution,
            string Status,
            int? LinkedAccountId,
            string? Notes);

        public sealed record FinancialGoalDto(
            int Id,
            string Name,
            string GoalType,
            decimal TargetAmount,
            decimal CurrentAmount,
            decimal RemainingAmount,
            decimal ProgressPercent,
            DateTime? TargetDate,
            decimal MonthlyContribution,
            decimal? SuggestedMonthlyContribution,
            string Status,
            int? LinkedAccountId,
            LinkedAccountDto? LinkedAccount,
            string? Notes);

        public sealed record LinkedAccountDto(int Id, string Name, bool IsCreditCard);
    }
}
