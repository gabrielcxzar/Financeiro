# FinFlow MCP — Quickstart

O servidor MCP é exposto em `/mcp` via Streamable HTTP stateless. O acesso exige OAuth 2.1 Authorization Code com PKCE e o escopo `finflow.read`.

## Configuração

Defina no ambiente (Render Secret/Environment Groups):

- `ConnectionStrings__DefaultConnection`
- `AppSettings__Token` (segredo JWT legado da API; nunca versionar)
- `McpOAuth__Issuer` (URL HTTPS pública terminada em `/`)
- `Mcp__RateLimitPerMinute` (padrão 60)
- `Mcp__DetailRateLimitPerMinute` (padrão 20; reservado para detalhes)

As migrations são aplicadas automaticamente apenas quando `RunSchemaBootstrap=true` (desenvolvimento por padrão). Em produção, execute a migration revisada no pipeline de deploy.

## Endpoints OAuth

- `/.well-known/openid-configuration` (discovery OpenID Connect gerado pelo OpenIddict)
- `/.well-known/oauth-protected-resource` (Protected Resource Metadata)
- `/oauth/authorize`
- `/oauth/token`
- `/oauth/revoke`

O cliente deve usar PKCE (S256), solicitar apenas `finflow.read` e enviar o access token Bearer para `/mcp`. Não existe API key MCP nem reutilização do JWT de sessão de 30 dias.

## Tools v1

`get_financial_summary`, `compare_periods`, `get_spending_by_category`, `get_transactions`, `get_account_balances`, `get_recurring_expenses`, `get_financial_goals` e `get_investment_positions` são somente leitura e filtradas pelo sujeito OAuth autenticado.
