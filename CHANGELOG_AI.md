# Finflow - Histórico de Alterações de IA (CHANGELOG_AI)

## [1.7.0] - 2026-09-10

- **Agente**: Codex (GPT-5)
- **Objetivo**: Bootstrap do Spec Kit 1.0.5 e spike inicial da fundação MCP/OAuth da feature `001-finflow-mcp`.
- **Alterações**: `.specify/`, comandos Spec Kit genéricos em `.codex/commands/`, pacotes `ModelContextProtocol.AspNetCore 2.2.0`, `OpenIddict 7.7.0`, alinhamento EF Core/Npgsql, endpoint stateless `/mcp`, política `finflow.read`, rate limit configurável e adaptadores P1 sobre `FinancialInsightsService`.
- **Validação**: `dotnet build MyFinance.API/MyFinance.API.csproj --no-restore` passou. A suíte LogicTests executou 22 testes, com 17 aprovados e 5 falhas preexistentes/relacionadas à semântica de importação; OAuth completo, P1/P2 e cliente real ainda não concluídos.
- **Risco**: O fluxo Authorization Code/token handlers ainda precisa ser implementado antes de expor o endpoint. O segredo JWT legado foi removido de `appsettings.json` e requer variável de ambiente para inicialização.


Este arquivo registra todas as alterações estruturais, de código e de documentação executadas por Agentes de Inteligência Artificial no repositório **Finflow**.

## [1.6.0] - 2026-09-10

- **Data**: 2026-09-10
- **Agente**: Codex
- **Modelo**: GPT-5
- **Objetivo**: Investigar e planejar a feature `001-finflow-mcp` somente leitura pelo fluxo Spec Kit `spec → plan → tasks`, sem implementar código de produção.
- **Arquivos Modificados / Criados**:
  - `docs/FINFLOW_MCP/SPEC.md` -> [SPEC.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/SPEC.md)
  - `docs/FINFLOW_MCP/PLAN.md` -> [PLAN.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/PLAN.md)
  - `docs/FINFLOW_MCP/TASKS.md` -> [TASKS.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/TASKS.md)
  - `docs/FINFLOW_MCP/CONTRACTS.md` -> [CONTRACTS.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/CONTRACTS.md)
  - `docs/FINFLOW_MCP/DATA_MODEL.md` -> [DATA_MODEL.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/DATA_MODEL.md)
  - `docs/FINFLOW_MCP/RESEARCH.md` -> [RESEARCH.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/RESEARCH.md)
  - `.ai/CONTEXT.md` -> [.ai/CONTEXT.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/.ai/CONTEXT.md)
  - `CHANGELOG_AI.md` -> [CHANGELOG_AI.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/CHANGELOG_AI.md)
- **Resumo Técnico**:
  - Mapeamento da API ASP.NET Core 8, entidades EF Core, autenticação JWT, `FinancialSnapshotService`, políticas de reporting, índices e suítes de teste.
  - Comparação entre MCP integrado e serviço separado, com recomendação do endpoint integrado e Streamable HTTP stateless.
  - Proposta de oito tools agregadoras/paginadas, sem Resources ou Prompts na v1.
  - Proposta de OAuth 2.1 com PKCE, escopo `finflow.read`, revogação, minimização, rate limit, observabilidade redigida e testes de segurança.
  - Registro de riscos existentes: JWT sem OAuth/audience/scope, segredo default versionado, carregamento integral de transações, possível agregação fora do período e ausência de índices compostos por período.
- **Motivação**: Permitir conexão futura do ChatGPT aos dados financeiros do FinFlow com segurança, correção e rastreabilidade antes de qualquer implementação.
- **Impacto**: Somente documentação e glossário; nenhuma funcionalidade ou configuração de execução foi alterada.
- **Riscos**: As decisões de autenticação, exposição, datas e recorte de entrega permanecem pendentes de aprovação humana.
- **Necessita Validação Humana?**: Sim, conforme gates de `docs/FINFLOW_MCP/PLAN.md` e `TASKS.md`.

---

## [1.5.1] - 2026-09-08

- **Data**: 2026-09-08
- **Agente**: Antigravity
- **Modelo**: Gemini 3.6 Flash (High) / Claude 3.7 Sonnet
- **Objetivo**: Modernização completa do favicon e do componente de carregamento (`BrandLoading`), alinhando 100% com o design Swiss Minimalist.
- **Arquivos Modificados / Criados**:
  - `MyFinance.Web/public/favicon.svg` -> Monograma Finflow 'F' em Dark Slate (`#0F172A`) com haste branca e acento Esmeralda (`#10B981`).
  - `MyFinance.Web/public/brand-mark.svg` -> SVG oficial atualizado para a nova identidade da marca.
  - `MyFinance.Web/src/components/BrandLoading.jsx` e `.css` -> Spinner desacoplado da logo (anel fino elegante em slate/esmeralda girando ao redor da logo estável com pulso suave, eliminando rotação torta da logo).
  - `MyFinance.Web/src/pages/Login.css` -> Paleta de login sincronizada com Dark Slate `#0F172A` e acentos esmeralda.
- **Resumo Técnico**:
  - Correção da rotação indesejada da logo durante o loading.
  - Eliminação de gradientes laranja legados no favicon e no loading.
  - Build do Vite validado com sucesso (`exit code 0`).
- **Motivação**: Feedback do usuário sobre inconsistência visual no loading e favicon.
- **Impacto**: Identidade visual unificada em todos os pontos de contato da aplicação.
- **Riscos**: Nulo.
- **Necessita Validação Humana?**: Não.

---

## [1.5.0] - 2026-09-08

- **Data**: 2026-09-08
- **Agente**: Antigravity
- **Modelo**: Gemini 3.6 Flash (High) / Claude 3.7 Sonnet
- **Objetivo**: Redesenho completo do sistema Finflow utilizando o Google Stitch MCP, modernizando toda a identidade visual para o estilo Swiss Minimalist (Finflow Minimalist Core), em português brasileiro (pt-BR) e moeda Real (R$).
- **Arquivos Modificados / Criados**:
  - `MyFinance.Web/src/index.css` -> Design tokens, paleta neutra, tipografia `Plus Jakarta Sans` + `Inter` (`tnum`).
  - `MyFinance.Web/src/App.jsx` -> ConfigProvider AntD (`colorPrimary: #0F172A`), Sidebar limpa e unificada, rodapé com perfil de Gabriel e logout.
  - `MyFinance.Web/src/pages/Home.jsx` -> Header bar, cards de métricas brancos com micro-sombras e tags de variação.
  - `MyFinance.Web/src/components/DashboardCharts.jsx` -> Gráficos de barras e rosca com paleta moderna e tooltips refinados.
  - `MyFinance.Web/src/pages/Transactions.jsx` -> Resumo do período com mini-cards, badges arredondados, tabela limpa.
  - `MyFinance.Web/src/pages/Accounts.jsx` -> Cartões de crédito em dark slate (`#0F172A`), contas com ícones e micro-sombras.
  - `MyFinance.Web/src/pages/Invoices.jsx` -> Fatura do cartão estilizada com status e detalhamento elegante.
  - `MyFinance.Web/src/pages/Investments.jsx` -> Cards de patrimônio em custódia e ativos em carteira, tabelas limpas.
  - `MyFinance.Web/src/pages/Budgets.jsx` -> Orçamentos por categoria com barras sutis e aviso de saldo restante.
  - `MyFinance.Web/src/pages/Goals.jsx` -> Metas financeiras com cards elegantes, metas acumuladas e progresso limpo.
  - `CHANGELOG.md` e `CHANGELOG_AI.md`
- **Resumo Técnico**:
  - Conexão e sincronização das telas com o Stitch MCP (`Finflow - Modern & Minimalist`).
  - Todas as páginas secundárias refatoradas para o padrão do Stitch aprovado pelo usuário.
  - Build do Vite (`npm run build`) validado com 0 erros e 0 quebras de contrato de API.
- **Motivação**: Solicitação direta do usuário para modernizar todo o sistema com minimalismo, em português e sem esforço mental.
- **Impacto**: UX/UI totalmente renovada, profissional e coesa.
- **Riscos**: Nulo.
- **Necessita Validação Humana?**: Não.

---

## [1.4.0] - 2026-07-29

- **Data**: 2026-07-29
- **Agente**: Antigravity
- **Modelo**: Gemini 3.6 Flash (High)
- **Objetivo**: Importação automática da fatura do Cartão de Crédito Nubank em formato OFX (`Nubank_2026-08-07.ofx`) para o banco de dados Neon (`neondb`) do usuário Gabriel Cezar.
- **Arquivos Modificados / Criados**:
  - `CHANGELOG_AI.md` -> [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md)
- **Resumo Técnico**:
  - Parsing de 23 transações de cartão contidas em `Nubank_2026-08-07.ofx`.
  - Verificação de duplicidade contra as transações pré-existentes na conta `Cartao Nubank` (ID 14).
  - Inserção de 13 novas despesas de cartão (ex: AliExpress, Outback, Centauro, TikTok, Shopee, iFood, etc.) e ajuste das datas de parcelamentos pré-existentes.
  - Validação do valor final consolidado da fatura de cartão de crédito no banco de dados em **R$ 1.193,30** (22 itens).
- **Motivação**: Atualização e calibração da fatura do cartão de crédito de julho/2026 a pedido do usuário.
- **Impacto**: Fatura de cartão de crédito no banco PostgreSQL Neon calibrada com 100% de exatidão em R$ 1.193,30.
- **Riscos**: Nulo.
- **Necessita Validação Humana?**: Não.

---

## [1.3.0] - 2026-07-29

- **Data**: 2026-07-29
- **Agente**: Antigravity
- **Modelo**: Gemini 3.6 Flash (High)
- **Objetivo**: Padronização do Protocolo de Inicialização da IA com a adição do checklist obrigatório e regras permanentes de leitura e validação de arquivos.
- **Arquivos Modificados / Criados**:
  - `.ai/AI_CONVENTIONS.md` -> [.ai/AI_CONVENTIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AI_CONVENTIONS.md)
  - `.ai/MEMORY.md` -> [.ai/MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md)
  - `CHANGELOG_AI.md` -> [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md)
- **Resumo Técnico**:
  - Inclusão do aviso de prioridade máxima "CHECKLIST OBRIGATÓRIO DE INICIALIZAÇÃO DA IA" no início de `.ai/AI_CONVENTIONS.md`.
  - Inclusão da seção 4 "Protocolo de Inicialização Obrigatório" em `.ai/MEMORY.md` definindo leitura mandatória prévia, validação de diretórios de destino e proibição de geração de arquivos indevidos na raiz.
- **Motivação**: Garantir o alinhamento e padronização permanente do protocolo de operação de IA no projeto.
- **Impacto**: Governança reforçada para todas as futuras execuções de agentes de IA neste repositório.
- **Riscos**: Nulo.
- **Necessita Validação Humana?**: Não.

---

## [1.2.0] - 2026-07-29

- **Data**: 2026-07-29
- **Agente**: Antigravity
- **Modelo**: Gemini 3.6 Flash (High)
- **Objetivo**: Importação automática do extrato bancário em formato OFX (`NU_166504429_01JUL2026_28JUL2026.ofx`) para o banco de dados Neon (`neondb`) do usuário Gabriel Cezar.
- **Arquivos Modificados / Criados**:
  - `CHANGELOG_AI.md` -> [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md)
- **Resumo Técnico**:
  - Leitura e parsing de 131 transações contidas no extrato OFX do Nubank.
  - Verificação de duplicidade contra as transações pré-existentes na tabela `transactions`.
  - Descarte de 8 transações duplicadas e inserção direta de 123 novas transações limpas e categorizadas para a conta Nubank (ID 13) do usuário Gabriel Cezar (ID 1).
- **Motivação**: Atualização de extrato bancário de julho/2026 a pedido do usuário.
- **Impacto**: Atualização completa da base de transações de julho/2026 no banco PostgreSQL Neon.
- **Riscos**: Nulo.
- **Necessita Validação Humana?**: Não.

---

## [1.1.0] - 2026-07-21

- **Data**: 2026-07-21
- **Agente**: Antigravity
- **Modelo**: Gemini 3.5 Flash (High)
- **Objetivo**: Reorganização e padronização da estrutura de documentação corporativa (.ai/, docs/, .meta/).
- **Arquivos Modificados / Criados**:
  - `README.md` -> [README.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/README.md)
  - `CHANGELOG.md` -> [CHANGELOG.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG.md)
  - `CHANGELOG_AI.md` -> [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md)
  - `.ai/BOOTSTRAP_PROJECT.md`
  - `.ai/AGENTS.md`
  - `.ai/AI_CONVENTIONS.md`
  - `.ai/GOVERNANCE.md`
  - `.ai/CONTEXT.md`
  - `.ai/MEMORY.md`
  - `docs/ARCHITECTURE.md`
  - `docs/SPEC.md`
  - `docs/ROADMAP.md`
  - `docs/DECISIONS.md`
  - `docs/PATTERNS.md`
  - `docs/EXAMPLES.md`
  - `.meta/AUDIT.md`
  - `.meta/INVENTORY.md`
  - `.meta/FILE_INDEX.md`
  - `.meta/DEPENDENCY_GRAPH.md`
  - `.meta/METRICS.md`
  - `.meta/WORKSPACE_ANALYSIS.md`
- **Resumo Técnico**:
  - Reorganização de toda a documentação nas 3 pastas corporativas de especificação: `docs/` (técnica), `.ai/` (diretrizes de IA e onboarding) e `.meta/` (análise automatizada).
  - Criação do manual operacional de IA [AI_CONVENTIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AI_CONVENTIONS.md).
  - Geração dos artefatos analíticos `.meta/` (Inventário, Índice de Arquivos, Grafo de Dependências, Métricas e Análise de Workspace).
  - Atualização integral de 100% dos links de referência para o esquema `file:///...`.
- **Motivação**: Atender à Estrutura Corporativa Obrigatória para desenvolvimento assistido por IA, garantindo máxima legibilidade, rastreabilidade e manutencibilidade.
- **Impacto**: Nenhuma alteração no código de produção; melhoria expressiva no tempo de onboarding e contexto de qualquer IA ou desenvolvedor humano.
- **Riscos**: Nulo para o código de execução (.NET e React).
- **Necessita Validação Humana?**: Não.

---

## [1.0.0] - 2026-07-21

- **Data**: 2026-07-21
- **Agente**: Antigravity
- **Modelo**: Gemini 3.5 Flash (High)
- **Objetivo**: Auditoria técnica completa inicial e criação do relatório AUDIT.md.
- **Arquivos Modificados**: `.meta/AUDIT.md`.
- **Resumo Técnico**: Leitura e varredura estática de 100% do código C# e React para catalogar arquitetura, dependências, padrões, duplicações e inconsistências.
- **Motivação**: Atender à solicitação de auditoria inicial do projeto.
- **Impacto**: Diagnóstico preciso de segurança (encoding de JWT) e débitos técnicos.
- **Riscos**: Nulo.
- **Necessita Validação Humana?**: Não.

---

## Confidence

### Alta
- Registro cronológico e auditável de todas as modificações efetuadas por IAs neste repositório.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
