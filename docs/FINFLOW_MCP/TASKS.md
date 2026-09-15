# Tarefas: FinFlow MCP somente leitura

**Entrada**: [SPEC.md](SPEC.md), [PLAN.md](PLAN.md), [RESEARCH.md](RESEARCH.md) e [CONTRACTS.md](CONTRACTS.md)  
**Status**: Backend implementado e validado localmente; migration, contratos HTTP e wire OAuth/MCP foram exercitados em PostgreSQL isolado. HTTPS/Render/ChatGPT e a suíte histórica completa ainda são gates externos/pendentes
**Formato**: `[ID] [P?] [US?] descrição com caminho`

## Fase 0 — Gate humano

- [x] T001 Aprovar em `docs/FINFLOW_MCP/PLAN.md` a arquitetura integrada, o provedor OAuth, a exposição do endpoint, a interpretação de datas, o recorte P1/P2, os limites e a retenção de logs.
- [x] T002 Criar a branch `codex/001-finflow-mcp` somente após T001 e confirmar worktree limpa.
- [x] T003 Registrar como ADR proposto/aprovado em `docs/DECISIONS.md` a integração no backend e o modelo de autorização escolhido.

**Checkpoint**: não iniciar dependências ou código antes da aprovação.

## Fase 1 — Spikes e contratos executáveis

- [ ] T004 [P] Criar teste de compatibilidade mínimo do pacote `ModelContextProtocol.AspNetCore` em `tests/backend/Finflow.Api.McpIntegrationTests/McpTransportCompatibilityTests.cs`, fixando uma versão compatível com .NET 8.
- [ ] T005 [P] Criar spike OAuth com o provedor aprovado em `tests/backend/Finflow.Api.McpIntegrationTests/OAuthCompatibilityTests.cs`, cobrindo discovery, PKCE, resource/audience e revogação.
- [ ] T006 Validar T004/T005 com um endpoint temporário local e o “Scan Tools” do ChatGPT; registrar versões e limitações confirmadas em `docs/FINFLOW_MCP/RESEARCH.md`.
- [ ] T007 Revisar e congelar os schemas de `docs/FINFLOW_MCP/CONTRACTS.md` após o teste real do cliente.
- [ ] T008 Definir fixtures financeiras de dois usuários e datas-limite em `tests/backend/Finflow.Api.McpIntegrationTests/Fixtures/FinancialScenario.cs`.
- [ ] T009 Confirmar a semântica histórica de `transactions.date` com amostra controlada e registrar a decisão em `docs/FINFLOW_MCP/PLAN.md`; abrir feature de migração separada se necessário.

**Checkpoint**: transporte, autenticação e datas comprovados antes do desenho definitivo.

## Fase 2 — Fundação segura (bloqueia todas as stories)

### Testes primeiro

- [ ] T010 [P] Criar testes de configuração sem secrets versionados em `tests/backend/Finflow.Api.McpIntegrationTests/ConfigurationSecurityTests.cs`.
- [ ] T011 [P] Criar testes de discovery, token inválido/expirado/revogado, issuer, audience/resource e scope em `tests/backend/Finflow.Api.McpIntegrationTests/McpAuthenticationTests.cs`.
- [ ] T012 [P] Criar testes de rate limit, Origin/Host e tamanho de request em `tests/backend/Finflow.Api.McpIntegrationTests/McpTransportSecurityTests.cs`.
- [ ] T013 [P] Criar testes de redaction e correlação em `tests/backend/Finflow.Api.McpIntegrationTests/McpObservabilityTests.cs`.
- [ ] T014 Executar T010–T013 e confirmar que falham pelas capacidades ainda ausentes.

### Implementação

- [x] T015 Remover valores default sensíveis de `MyFinance.API/appsettings.json`, adicionar opções seguras e documentar apenas nomes de variáveis no `.env.example` se sua criação for aprovada.
- [x] T016 Adicionar e fixar dependências aprovadas em `MyFinance.API/MyFinance.API.csproj` e nos testes existentes, sem pacotes redundantes.
- [x] T017 Implementar configuração OAuth/resource server em `MyFinance.API/Program.cs` e `MyFinance.API/Controllers/McpOAuthController.cs` com discovery, PKCE, audience/resource, `finflow.read`, rotação e revogação.
- [x] T018 Criar migrations OAuth necessárias em `MyFinance.API/Migrations/`, sem armazenar tokens em texto claro.
- [x] T019 Configurar autenticação/política `finflow.read` e separar políticas REST/MCP em `MyFinance.API/Program.cs`.
- [x] T020 Configurar Streamable HTTP stateless em `POST /mcp` e allowlist de tools em `MyFinance.API/Program.cs` e `MyFinance.API/Mcp/`.
- [x] T021 Implementar rate limiting por sujeito/IP, limite de corpo e validação de Origin/Host em `MyFinance.API/Program.cs` e `MyFinance.API/Mcp/McpRateLimitException.cs`.
- [x] T021a Ajustar o rate limiter MCP para média configurável com burst configurável e documentar os defaults aprovados.
- [x] T021b Processar `X-Forwarded-Proto` de forma configurável antes do redirecionamento HTTPS no proxy do Render.
- [x] T022 Implementar envelope de erro, correlação e redaction em `MyFinance.API/Program.cs`.
- [ ] T023 Executar T010–T013 até passarem e confirmar que as rotas REST existentes continuam autenticando normalmente.

**Checkpoint**: nenhum dado financeiro é consultável sem OAuth válido e escopo mínimo.

## Fase 3 — Módulo de consultas financeiras

### Testes primeiro

- [ ] T024 [P] Criar testes da política operacional para consumo, transferência, fatura, repasse, ajuste e estorno em `tests/backend/Finflow.Api.LogicTests/ReportingPolicyTests.cs`.
- [ ] T025 [P] Criar testes de período civil, fim exclusivo e virada de mês em `tests/backend/Finflow.Api.LogicTests/FinancialPeriodTests.cs`.
- [ ] T026 [P] Criar testes de taxa de poupança, comparação e renda zero em `tests/backend/Finflow.Api.LogicTests/FinancialInsightsCalculationTests.cs`.
- [ ] T027 [P] Criar testes de cursor adulterado, datas iguais e limites em `tests/backend/Finflow.Api.LogicTests/TransactionCursorTests.cs`.
- [ ] T028 Executar T024–T027 e confirmar que falham antes da implementação.

### Implementação

- [x] T029 Transformar a semântica operacional em expressão EF reutilizável sem alterar seu resultado em `MyFinance.API/Services/ReportingPolicy.cs`.
- [ ] T030 Criar contratos internos imutáveis e tipos de período/dinheiro em `MyFinance.API/Services/FinancialInsightsContracts.cs`.
- [x] T031 Criar `IFinancialInsightsService` e implementação agregada SQL em `MyFinance.API/Services/FinancialInsightsService.cs`, aplicando `UserId` na raiz de toda query.
- [x] T032 Implementar paginação keyset e cursor protegido em `MyFinance.API/Services/FinancialInsightsService.cs` e `MyFinance.API/Services/TransactionCursorCodec.cs`.
- [x] T033 Integrar saldos/passivos por `IFinancialSnapshotService` sem usar o carregamento completo para agregações de período.
- [ ] T034 Adicionar índices candidatos em `MyFinance.API/Data/AppDbContext.cs` e migration em `MyFinance.API/Migrations/`; manter apenas os justificados por planos de consulta.
- [ ] T035 Medir consultas com dados sintéticos e registrar p50/p95, payload e `EXPLAIN` redigido em `docs/FINFLOW_MCP/RESEARCH_CURRENT.md` (EXPLAIN/payload já registrados; p50/p95 de carga sustentada permanecem pendentes).
- [ ] T036 Executar T024–T027 e a suíte `Finflow.Api.LogicTests` completa.

**Checkpoint**: domínio consultável por uma interface única, sem MCP e sem controllers nos testes de regra.

## Fase 4 — US1: resumo e comparação (P1/MVP)

### Testes primeiro

- [ ] T037 [P] [US1] Criar testes de contrato input/output para `get_financial_summary` em `tests/backend/Finflow.Api.McpIntegrationTests/FinancialSummaryToolTests.cs`.
- [ ] T038 [P] [US1] Criar testes de contrato input/output para `compare_periods` em `tests/backend/Finflow.Api.McpIntegrationTests/ComparePeriodsToolTests.cs`.
- [ ] T039 [US1] Criar teste que reproduza o risco de agregação fora do período e prove o resultado correto no MCP.

### Implementação

- [x] T040 [P] [US1] Implementar adaptador `get_financial_summary` em `MyFinance.API/Mcp/FinflowMcpTools.cs`.
- [x] T041 [P] [US1] Implementar adaptador `compare_periods` em `MyFinance.API/Mcp/FinflowMcpTools.cs`.
- [x] T042 [US1] Validar output schemas, hints read-only e ausência de `SaveChanges`/dependências de escrita por reflexão, wire test e fingerprint PostgreSQL.
- [ ] T043 [US1] Executar testes US1, autenticação, isolamento e suíte de regressão REST.

**Checkpoint MVP**: ChatGPT obtém contexto agregado confiável sem registros brutos.

## Fase 5 — US3: saldos e compromissos (P1/P2)

### Testes primeiro

- [ ] T044 [P] [US3] Criar testes de `get_account_balances` com conta, cartão, estorno e zero contas em `tests/backend/Finflow.Api.McpIntegrationTests/AccountBalancesToolTests.cs`.
- [ ] T045 [P] [US3] Criar testes de recorrências/metas/posições e omissão de notas em `tests/backend/Finflow.Api.McpIntegrationTests/PlanningContextToolTests.cs`.

### Implementação

- [x] T046 [P] [US3] Implementar `get_account_balances` em `MyFinance.API/Mcp/FinflowMcpTools.cs`.
- [x] T047 [P] [US3] Implementar `get_recurring_expenses` em `MyFinance.API/Mcp/FinflowMcpTools.cs`.
- [x] T048 [P] [US3] Implementar `get_financial_goals` em `MyFinance.API/Mcp/FinflowMcpTools.cs`.
- [x] T049 [P] [US3] Implementar `get_investment_positions` em `MyFinance.API/Mcp/FinflowMcpTools.cs`, mantendo `marketValue=null` quando não conhecido.
- [ ] T050 [US3] Executar testes US3 e reconciliar saldos com fixtures do `FinancialSnapshotService`.

## Fase 6 — US2: categorias e detalhes minimizados (P2)

### Testes primeiro

- [ ] T051 [P] [US2] Criar testes de agregação/top N para `get_spending_by_category` em `tests/backend/Finflow.Api.McpIntegrationTests/SpendingByCategoryToolTests.cs`.
- [ ] T052 [P] [US2] Criar testes de filtros, paginação, isolamento e minimização para `get_transactions` em `tests/backend/Finflow.Api.McpIntegrationTests/TransactionsToolTests.cs`.
- [ ] T053 [P] [US2] Criar testes de prompt injection/exfiltração em descrições e categorias em `tests/backend/Finflow.Api.McpIntegrationTests/McpDataSafetyTests.cs`.

### Implementação

- [x] T054 [P] [US2] Implementar `get_spending_by_category` em `MyFinance.API/Mcp/FinflowMcpTools.cs`.
- [x] T055 [P] [US2] Implementar `get_transactions` em `MyFinance.API/Mcp/FinflowMcpTools.cs` com projeção explícita de campos permitidos.
- [ ] T056 [US2] Executar testes US2, confirmar payload máximo e verificar que `RawMemo`, source file, external ID e dados de importação nunca aparecem (wire P2 passou; falta a asserção dedicada de campos proibidos).

## Fase 7 — Hardening, documentação e liberação

- [ ] T057 [P] Executar testes de tool inexistente/de escrita, parâmetros desconhecidos, período/página excessivos e dois usuários em `tests/backend/Finflow.Api.McpIntegrationTests/McpAbuseTests.cs`.
- [ ] T058 [P] Fazer revisão de dependências, threat model e configuração de produção; registrar achados em `docs/FINFLOW_MCP/RESEARCH.md`.
- [ ] T059 Executar `dotnet build MyFinance.sln`, suítes de lógica, contrato e integração MCP; registrar resultados em `CHANGELOG_AI.md`.
- [ ] T060 Validar HTTPS, discovery, consentimento, revogação, reconnect e todas as tools pelo ChatGPT em ambiente de staging.
- [ ] T061 Atualizar `docs/ARCHITECTURE.md`, `docs/SPEC.md`, `docs/DECISIONS.md`, `docs/EXAMPLES.md` e `.ai/MEMORY.md` apenas com fatos implementados e validados.
- [x] T062 Criar `docs/FINFLOW_MCP/QUICKSTART.md` com execução, variáveis, URL, transporte, OAuth, tools, schemas, limites, revogação e teste local, sem secrets reais.
- [x] T063 Atualizar `CHANGELOG_AI.md` e, no lançamento, `CHANGELOG.md`.
- [ ] T064 Liberar primeiro para o titular, observar erros/latência por 7 dias e só então decidir se habilita Resources, Prompts ou novas tools.

## Dependências e ordem

- T001–T003 bloqueiam todo o trabalho.
- T004–T009 bloqueiam a fundação para evitar escolher protocolo/autenticação por suposição.
- T010–T023 bloqueiam acesso a dados.
- T024–T036 bloqueiam todas as tools financeiras.
- US1 é o MVP; US3 e US2 podem avançar em paralelo depois da fundação e do módulo, respeitando arquivos compartilhados.
- T057–T064 dependem das stories escolhidas para a entrega.
- Em cada bloco, testes devem existir e falhar pelo motivo esperado antes do código correspondente.

## Critério de conclusão

A feature só está concluída quando as tools aprovadas passam contratos, isolamento, segurança, read-only, correção financeira e smoke test real no ChatGPT; documentação e revogação são parte do produto, não tarefas opcionais.

## Confidence

### Implementação observada em 2026-09-15

- Os adaptadores foram consolidados em `MyFinance.API/Mcp/FinflowMcpTools.cs` e as consultas em `MyFinance.API/Mcp/FinancialInsightsService.cs`; os caminhos `Mcp/Tools/*` e filtros separados citados no plano não foram criados.
- As provas de serviço que dependem de PostgreSQL foram executadas em banco Neon efêmero, schema-only e isolado da produção, com `FINFLOW_POSTGRES_TEST_ISOLATED=1`: 3 provas PostgreSQL passaram. A suíte completa ficou em 23 aprovados e 5 falhas preexistentes de importação.
- A migration foi aplicada/revertida/reaplicada; contratos HTTP `27/27`, EXPLAIN e wire OAuth/MCP local passaram. O wire usa HTTP apenas em Development, mantendo issuer/resource HTTPS canônicos; HTTPS Render e cliente ChatGPT real permanecem pendentes.

### Alta

- Ordem e arquivos refletem a estrutura atual da solução e o fluxo Spec Kit oficial.

### Média

- A divisão em novo projeto de integração depende do resultado do spike com `WebApplicationFactory` e transporte MCP.

### Baixa

- Caminhos exatos gerados por OpenIddict/SDK podem mudar após a versão ser fixada.

## Validação Humana Necessária

- Executar e aprovar T001 antes de qualquer tarefa de implementação.
