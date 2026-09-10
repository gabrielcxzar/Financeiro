using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;

namespace MyFinance.API.Mcp;

[McpServerToolType]
public sealed class FinflowMcpTools(IFinancialInsightsService insights, IHttpContextAccessor http)
{
    [McpServerTool(Name = "get_financial_summary"), Description("Retorna resumo financeiro agregado do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<FinancialSummary>> GetFinancialSummary(DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
        => insights.GetSummaryAsync(UserId(), startDate, endDate, cancellationToken);

    [McpServerTool(Name = "compare_periods"), Description("Compara dois períodos financeiros do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<PeriodComparison>> ComparePeriods(PeriodInput current, PeriodInput previous, CancellationToken cancellationToken)
        => insights.ComparePeriodsAsync(UserId(), current, previous, cancellationToken);

    [McpServerTool(Name = "get_account_balances"), Description("Retorna saldos conhecidos das contas do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<IReadOnlyList<AccountBalance>>> GetAccountBalances(CancellationToken cancellationToken)
        => insights.GetAccountBalancesAsync(UserId(), cancellationToken);

    [McpServerTool(Name = "get_spending_by_category"), Description("Agrega despesas por categoria do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<IReadOnlyList<CategorySpend>>> GetSpendingByCategory(DateTime startDate, DateTime endDate, int limit = 10, CancellationToken cancellationToken = default)
        => insights.GetSpendingByCategoryAsync(UserId(), startDate, endDate, limit, cancellationToken);

    [McpServerTool(Name = "get_transactions"), Description("Lista transações do período informado com paginação limitada."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<TransactionPage>> GetTransactions(DateTime startDate, DateTime endDate, int limit = 50, string? cursor = null, CancellationToken cancellationToken = default)
        => insights.GetTransactionsAsync(UserId(), startDate, endDate, limit, cursor, cancellationToken);

    [McpServerTool(Name = "get_recurring_expenses"), Description("Retorna despesas recorrentes ativas."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<IReadOnlyList<RecurringExpense>>> GetRecurringExpenses(CancellationToken cancellationToken)
        => insights.GetRecurringExpensesAsync(UserId(), cancellationToken);

    [McpServerTool(Name = "get_financial_goals"), Description("Retorna metas financeiras do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<IReadOnlyList<FinancialGoalItem>>> GetFinancialGoals(CancellationToken cancellationToken)
        => insights.GetFinancialGoalsAsync(UserId(), cancellationToken);

    [McpServerTool(Name = "get_investment_positions"), Description("Retorna posições de investimento conhecidas sem inventar cotação de mercado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<IReadOnlyList<InvestmentPosition>>> GetInvestmentPositions(CancellationToken cancellationToken)
        => insights.GetInvestmentPositionsAsync(UserId(), cancellationToken);

    private int UserId()
    {
        var value = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.HttpContext?.User.FindFirstValue("sub");
        return int.TryParse(value, out var id) && id > 0 ? id : throw new UnauthorizedAccessException();
    }
}
