# FinFlow MCP — Quickstart

O servidor MCP é exposto em `/mcp` via Streamable HTTP stateless. O acesso exige OAuth 2.1 Authorization Code com PKCE e o escopo `finflow.read`.

## Configuração

Defina no ambiente (Render Secret/Environment Groups):

- `ConnectionStrings__DefaultConnection`
- `AppSettings__Token` (segredo JWT legado da API; nunca versionar)
- `McpOAuth__Issuer` (URL HTTPS pública terminada em `/`)
- `McpOAuth__AllowInsecureDevelopmentTransport` (opcional, somente Development/testes locais; nunca habilitar em produção)
- `ForwardedHeaders__Enabled` (produção atrás do proxy TLS do Render; padrão `true` em Production)
- `McpOAuth__ClientId` (client ID pré-registrado; para ChatGPT use a identidade/callback exibidos na tela de criação do app)
- `McpOAuth__RedirectUris` (lista de callbacks exatos, sem curingas)
- `McpOAuth__EncryptionCertificatePath` e `McpOAuth__SigningCertificatePath` (produção)
- `McpOAuth__EncryptionCertificateBase64` e `McpOAuth__SigningCertificateBase64` (alternativa para secrets do Render sem arquivo)
- `McpOAuth__CertificatePassword` (produção; secret store)
- `Mcp__RateLimitPerMinute` (padrão 60)
- `Mcp__RateLimitBurst` (padrão 10)
- `Mcp__DetailRateLimitPerMinute` (padrão 20; reservado para detalhes)
- `Mcp__MaxRequestBytes` (padrão 262144)
- `Mcp__AllowedHosts__0` (host público do issuer; aliases opcionais)
- `Mcp__AllowedOrigins__0` (opcional; por padrão requisições com `Origin` são rejeitadas)

Configure a retenção do agregador de logs do Render para 30 dias. O middleware MCP registra apenas metadados técnicos redigidos (correlation ID, tool/status, duração, sujeito pseudonimizado e tamanho aproximado), sem prompts, descrições, valores, respostas ou tokens.

Em produção, mantenha `Logging__LogLevel__OpenIddict=Warning` e `Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning` (os mesmos defaults já estão versionados). Isso conserva erros e warnings relevantes sem registrar payloads OAuth ou comandos SQL rotineiros em `Information`; a categoria própria `McpRequest` continua em `Information` e redigida.

As migrations são aplicadas automaticamente apenas quando `RunSchemaBootstrap=true` (desenvolvimento por padrão). Em produção, execute a migration revisada no pipeline de deploy.

## Endpoints OAuth

- `/.well-known/openid-configuration` (discovery OpenID Connect gerado pelo OpenIddict)
- `/.well-known/oauth-protected-resource` (Protected Resource Metadata)
- `/oauth/authorize`
- `/oauth/token`
- `/oauth/revoke`

O cliente deve usar PKCE (S256), solicitar `finflow.read` e, se precisar renovar a sessão, também o escopo OAuth padrão `offline_access`; somente `finflow.read` concede acesso a dados do FinFlow. Depois, deve enviar o access token Bearer para `/mcp`. O servidor usa pré-registro explícito de cliente; não existe `AcceptAnonymousClients`, API key MCP nem reutilização do JWT de sessão de 30 dias. A URL e callback fornecidos pelo ChatGPT devem ser cadastrados exatamente nas variáveis acima.

## Tools v1

`get_financial_summary`, `compare_periods`, `get_spending_by_category`, `get_transactions`, `get_account_balances`, `get_recurring_expenses`, `get_financial_goals` e `get_investment_positions` são somente leitura e filtradas pelo sujeito OAuth autenticado.

## Manutenção de lançamentos legados

O script `MyFinance.API/Scripts/Maintenance/RepairLegacyNonOperationalTransactions.sql` é um runbook auditável, específico para os IDs históricos 3214, 3240 e 3276. Ele é idempotente, valida as identidades e invariantes antes de qualquer alteração e executa dry-run por padrão. Para aplicar, use uma conexão administrativa isolada e defina explicitamente `finflow.apply_legacy_reporting='1'`; não reutilize o script para outros lançamentos sem uma nova revisão.
