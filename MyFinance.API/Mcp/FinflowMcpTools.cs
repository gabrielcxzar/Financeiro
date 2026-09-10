using System.ComponentModel;
using System.Security.Claims;
using ModelContextProtocol.Server;

namespace MyFinance.API.Mcp;

[McpServerToolType]
public sealed class FinflowMcpTools(IFinancialInsightsService insights, IHttpContextAccessor http)
{
    [McpServerTool(Name = "get_financial_summary"), Description("Retorna resumo financeiro agregado do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<FinancialSummary>> GetFinancialSummary(DateTime from, DateTime to, CancellationToken cancellationToken)
        => insights.GetSummaryAsync(UserId(), from, to, cancellationToken);

    [McpServerTool(Name = "compare_periods"), Description("Compara dois períodos financeiros do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<PeriodComparison>> ComparePeriods(PeriodInput current, PeriodInput previous, CancellationToken cancellationToken)
        => insights.ComparePeriodsAsync(UserId(), current, previous, cancellationToken);

    [McpServerTool(Name = "get_account_balances"), Description("Retorna saldos conhecidos das contas do usuário autenticado."), McpMeta("readOnlyHint", true), McpMeta("destructiveHint", false), McpMeta("idempotentHint", true), McpMeta("openWorldHint", false)]
    public Task<InsightEnvelope<IReadOnlyList<AccountBalance>>> GetAccountBalances(CancellationToken cancellationToken)
        => insights.GetAccountBalancesAsync(UserId(), cancellationToken);

    private int UserId()
    {
        var value = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.HttpContext?.User.FindFirstValue("sub");
        return int.TryParse(value, out var id) && id > 0 ? id : throw new UnauthorizedAccessException();
    }
}
