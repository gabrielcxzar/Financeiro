# Matriz de rastreabilidade — 001-finflow-mcp

| Requisito | SPEC | PLAN | TASK | Código atual | Teste/evidência | Status |
|---|---|---|---|---|---|---|
| OAuth Authorization Code + PKCE | RF-MCP-014/US4 | Fundação OAuth | T017, T023 | OpenIddict em `Program.cs` e `/oauth/authorize`/`/oauth/token` | Fluxo end-to-end ainda pendente | Parcial |
| Refresh rotation e revogação | RF-MCP-014 | OAuth | T017–T018 | Reference tokens, lifetime e `/oauth/revoke` | Sem teste de revogação | Parcial |
| Discovery + Protected Resource Metadata | US4 | Fundação segura | T017, T022 | OpenIddict discovery + `McpMetadataController` | Sem teste HTTP automatizado | Parcial |
| Scope `finflow.read` | RF-MCP-015 | Política MCP | T019 | `McpRead` exige claim de scope | Sem teste HTTP | Parcial |
| Audience/resource | RF-MCP-014 | OAuth | T017 | Não configurado na validação JWT/MCP | Sem teste | Pendente |
| Isolamento por usuário | RF-MCP-002 | FinancialInsightsService | T031, T043, T050 | Filtro `UserId` no serviço inicial | Sem cenário A/B | Parcial |
| Read-only financeiro | RF-MCP-001/US4 | Hardening | T042, T057 | Adapters não chamam `SaveChanges` | Sem fingerprint antes/depois | Parcial |
| P1 summary | RF-MCP-003 | Fase 4 | T037–T043 | `get_financial_summary` registrado | Sem contrato/integração | Parcial |
| P1 compare | RF-MCP-004 | Fase 4 | T038–T043 | `compare_periods` registrado | Sem contrato/integração | Parcial |
| P1 account balances | RF-MCP-007 | Fase 5 | T044–T050 | `get_account_balances` registrado | Sem PostgreSQL/A-B | Parcial |
| Reporting policy | RF-MCP-009 | Fase 3 | T024, T029 | Reusa `ReportingPolicy.IsOperational` | Cobertura existente não é MCP | Parcial |
| Limites de período/payload | RF-MCP-011 | Limites | T021, T025, T035 | Validação mínima no serviço | Sem testes de limites completos | Parcial |
| Rate limit e observabilidade | RF-MCP-016/017 | Fundação | T021–T023 | Rate limiter baseline; logs MCP dedicados ausentes | Sem inspeção de logs | Parcial |
| PostgreSQL e performance | RF-MCP-012/013 | Fase 3 | T034–T036 | Sem queries agregadas finais/índices medidos | Sem EXPLAIN/p95 | Pendente |
| Allowlist exata de 8 tools | RF-MCP-001 | Superfície MCP | T020 | Oito adapters registrados em `FinflowMcpTools` | `tools/list` não validado | Parcial |

## Checkpoint

O código agora contém a fundação OAuth/MCP e as oito tools read-only. A validação PostgreSQL, contratos estritos e o fluxo real com cliente ChatGPT continuam pendentes; as cinco falhas de `Finflow.Api.LogicTests` permanecem preexistentes.
