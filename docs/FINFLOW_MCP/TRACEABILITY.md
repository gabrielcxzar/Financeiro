# Matriz de rastreabilidade — 001-finflow-mcp

| Requisito | SPEC | PLAN | TASK | Código atual | Teste/evidência | Status |
|---|---|---|---|---|---|---|
| OAuth Authorization Code + PKCE | RF-MCP-014/US4 | Fundação OAuth | T017, T023 | OpenIddict em `Program.cs` e `/oauth/authorize`/`/oauth/token`, PKCE S256 obrigatório | Wire local passou com authorization code, PKCE S256 e resource; HTTPS Render ainda pendente | Parcial |
| Refresh rotation e revogação | RF-MCP-014 | OAuth | T017–T018 | Reference tokens, leeway zero e `/oauth/revoke` | Wire local: refresh 200, replay 400, revoke 200 e chamada posterior 401 | Validado local |
| Discovery + Protected Resource Metadata | US4 | Fundação segura | T017, T022 | OpenIddict discovery + `McpMetadataController` | Wire local: discovery e metadata 200; HTTPS Render ainda pendente | Parcial |
| Scope `finflow.read` | RF-MCP-015 | Política MCP | T019 | `McpRead` exige claim de scope | Wire HTTP local com scope/resource válidos; testes negativos dedicados permanecem | Parcial |
| Audience/resource | RF-MCP-014 | OAuth | T017 | Validação e principal usam resource canônica `/mcp` | Wire HTTP local enviou `resource` canônico e chamada MCP 200; audiência inválida ainda não automatizada | Parcial |
| Isolamento por usuário | RF-MCP-002 | FinancialInsightsService | T031, T043, T050 | `UserId` é derivado do subject e aplicado em todas as queries | Prova A/B executada em PostgreSQL Neon schema-only isolado; passou | Parcial |
| Read-only financeiro | RF-MCP-001/US4 | Hardening | T042, T057 | Allowlist fixa, adapters sem `SaveChanges`, projeções minimizadas | Fingerprint antes/depois executado em PostgreSQL isolado; estado inalterado | Parcial |
| P1 summary | RF-MCP-003 | Fase 4 | T037–T043 | `get_financial_summary` agregado no banco e registrado | Wire MCP local 200; contrato HTTP 27/27 | Validado local |
| P1 compare | RF-MCP-004 | Fase 4 | T038–T043 | `compare_periods` com períodos civis normalizados | Wire MCP local 200; contrato HTTP 27/27 | Validado local |
| P1 account balances | RF-MCP-007 | Fase 5 | T044–T050 | `get_account_balances` usa `FinancialSnapshotService` e filtra usuário | Wire MCP local 200 e conjunto A/B PostgreSQL isolado | Validado local |
| Reporting policy | RF-MCP-009 | Fase 3 | T024, T029 | `ReportingPolicy.OperationalPredicate` compartilhada pelo módulo MCP | Cobertura financeira MCP PostgreSQL pendente | Parcial |
| Limites de período/payload | RF-MCP-011 | Limites | T021, T025, T035 | 24/12 meses, 92 dias, 50/100 itens, corpo 256 KiB | Wire P1/P2 máximo observado 1.337 bytes; cobertura dedicada de limites permanece | Parcial |
| Rate limit e observabilidade | RF-MCP-016/017 | Fundação | T021–T023 | 60/min por sujeito, 20/min detalhes, correlation ID e logs redigidos | Sem inspeção de logs | Parcial |
| PostgreSQL e performance | RF-MCP-012/013 | Fase 3 | T034–T036 | Resumo/categorias usam GroupBy SQL; sem índices novos | EXPLAIN isolado usou `ix_transactions_user_id`; execução observada <0,1 ms; p50/p95 de carga pendente | Parcial |
| Allowlist exata de 8 tools | RF-MCP-001 | Superfície MCP | T020 | Oito adapters registrados em `FinflowMcpTools` com hints read-only/structured | Reflexão e `tools/list` real confirmaram exatamente 8 | Validado local |

## Checkpoint

O código agora contém a fundação OAuth/MCP e as oito tools read-only. A validação PostgreSQL isolada passou (3 provas), migration foi exercitada, contratos HTTP `27/27` e wire OAuth/MCP local passaram. Permanecem pendentes a prova HTTPS/deploy no Render, o fluxo real com cliente ChatGPT e a cobertura dedicada de alguns cenários; as cinco falhas de importação de `Finflow.Api.LogicTests` são preexistentes ao módulo MCP.
