# FinFlow MCP — Quickstart

O servidor MCP é exposto em `/mcp` via Streamable HTTP stateless. O acesso exige OAuth 2.1 Authorization Code com PKCE e o escopo `finflow.read`.

## Configuração

Defina no ambiente (Render Secret/Environment Groups):

- `ConnectionStrings__DefaultConnection`
- `AppSettings__Token` (segredo JWT legado da API; nunca versionar)
- `McpOAuth__Issuer` (URL HTTPS pública terminada em `/`)
- `McpOAuth__ClientId` (client ID pré-registrado; para ChatGPT use a identidade/callback exibidos na tela de criação do app)
- `McpOAuth__RedirectUris` (lista de callbacks exatos, sem curingas)
- `McpOAuth__EncryptionCertificatePath` e `McpOAuth__SigningCertificatePath` (produção)
- `McpOAuth__EncryptionCertificateBase64` e `McpOAuth__SigningCertificateBase64` (alternativa para secrets do Render sem arquivo)
- `McpOAuth__CertificatePassword` (produção; secret store)
- `Mcp__RateLimitPerMinute` (padrão 60)
- `Mcp__DetailRateLimitPerMinute` (padrão 20; reservado para detalhes)
- `Mcp__MaxRequestBytes` (padrão 262144)
- `Mcp__AllowedHosts__0` (host público do issuer; aliases opcionais)
- `Mcp__AllowedOrigins__0` (opcional; por padrão requisições com `Origin` são rejeitadas)

Configure a retenção do agregador de logs do Render para 30 dias. O middleware MCP registra apenas metadados técnicos redigidos (correlation ID, tool/status, duração, sujeito pseudonimizado e tamanho aproximado), sem prompts, descrições, valores, respostas ou tokens.

As migrations são aplicadas automaticamente apenas quando `RunSchemaBootstrap=true` (desenvolvimento por padrão). Em produção, execute a migration revisada no pipeline de deploy.

## Endpoints OAuth

- `/.well-known/openid-configuration` (discovery OpenID Connect gerado pelo OpenIddict)
- `/.well-known/oauth-protected-resource` (Protected Resource Metadata)
- `/oauth/authorize`
- `/oauth/token`
- `/oauth/revoke`

O cliente deve usar PKCE (S256), solicitar apenas `finflow.read` e enviar o access token Bearer para `/mcp`. O servidor usa pré-registro explícito de cliente; não existe `AcceptAnonymousClients`, API key MCP nem reutilização do JWT de sessão de 30 dias. A URL e callback fornecidos pelo ChatGPT devem ser cadastrados exatamente nas variáveis acima.

## Tools v1

`get_financial_summary`, `compare_periods`, `get_spending_by_category`, `get_transactions`, `get_account_balances`, `get_recurring_expenses`, `get_financial_goals` e `get_investment_positions` são somente leitura e filtradas pelo sujeito OAuth autenticado.
