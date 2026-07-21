# Finflow - Gestão Financeira Pessoal

Finflow é uma aplicação full-stack moderna para gestão financeira pessoal. O sistema permite o controle de contas bancárias, cartões de crédito, transações parceladas, transferências entre carteiras, orçamentos mensais, metas de economia, investimentos (Fundos Imobiliários e Tesouro Direto) e importação automática de extratos bancários.

---

## Sumário
1. [Visão Geral](#visão-geral)
2. [Arquitetura & Tecnologias](#arquitetura--tecnologias)
3. [Estrutura do Projeto](#estrutura-do-projeto)
4. [Organização do Projeto](#organização-do-projeto)
5. [Módulos Principais](#módulos-principais)
6. [Instalação e Execução Local](#instalação-e-execução-local)
7. [Execução de Testes](#execução-de-testes)
8. [Documentação Completa](#documentação)
9. [Confidence](#confidence)

---

## Visão Geral

- **Front-end**: Single Page Application (SPA) responsiva em React 18, Vite e Ant Design.
- **Back-end**: Web API RESTful stateless em ASP.NET Core 8 (.NET 8) com autenticação JWT.
- **Banco de Dados**: PostgreSQL relacional (hospedado no Supabase) manipulado via EF Core 8.
- **Integrações**: Cotações atualizadas do Tesouro Direto via dataset público do Tesouro Transparente.

---

## Arquitetura & Tecnologias

### Back-end
- [.NET 8 SDK](https://dotnet.microsoft.com/) / ASP.NET Core Web API em [MyFinance.API/MyFinance.API.csproj](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/MyFinance.API.csproj)
- Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL` v8.0.0) em [AppDbContext.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Data/AppDbContext.cs)
- Autenticação JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` v8.0.0) em [Program.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Program.cs)
- Criptografia de senhas com BCrypt (`BCrypt.Net-Next` v4.0.3) em [AuthController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/AuthController.cs)

### Front-end
- React 18.3.1 + Vite 7.2.4 em [MyFinance.Web/package.json](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/package.json)
- Interface rica com Ant Design (`antd` v6.0.0, `@ant-design/icons`, `antd-mobile`) e `styled-components` v6.1.19
- Gráficos interativos com Chart.js v4.5.1 (`react-chartjs-2`) em [DashboardCharts.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/components/DashboardCharts.jsx)
- Cliente HTTP Axios v1.13.2 com interceptors em [api.js](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/services/api.js)

---

## Organização do Projeto

O repositório adota a **Estrutura Corporativa Padronizada** para projetos assistidos por Inteligência Artificial:

```text
Financeiro/
├── README.md                           # Ponto de entrada principal para humanos
├── CHANGELOG.md                        # Histórico de lançamentos do projeto
├── CHANGELOG_AI.md                     # Registro auditável de modificações por IAs
├── MyFinance.API/                      # Código-fonte da Web API ASP.NET Core 8
├── MyFinance.Web/                      # Código-fonte do Front-end React 18 + Vite
├── tests/                              # Suítes de testes automatizados (.NET e Playwright)
│
├── docs/                               # Documentação técnica do sistema
│   ├── ARCHITECTURE.md                 # Arquitetura técnica e diagramas Mermaid/ERD
│   ├── SPEC.md                         # Requisitos funcionais e não-funcionais
│   ├── ROADMAP.md                      # Backlog técnico e débitos de código
│   ├── DECISIONS.md                    # Registro Histórico de Decisões (ADRs)
│   ├── PATTERNS.md                     # Padrões de código C# e React
│   └── EXAMPLES.md                     # Exemplos de chamadas HTTP e snippets
│
├── .ai/                                # Diretrizes e contexto para Agentes de IA
│   ├── BOOTSTRAP_PROJECT.md            # Onboarding agnóstico e ordem de leitura
│   ├── AGENTS.md                       # Regras de comportamento e compatibilidade
│   ├── AI_CONVENTIONS.md               # Manual operacional padrão para IAs
│   ├── GOVERNANCE.md                   # Políticas permanentes e governança
│   ├── CONTEXT.md                      # Contexto funcional completo do sistema
│   └── MEMORY.md                       # Memória permanente consolidada
│
└── .meta/                              # Artefatos de análise técnica automatizada
    ├── AUDIT.md                        # Relatório de auditoria técnica completa
    ├── INVENTORY.md                    # Inventário analítico de componentes
    ├── FILE_INDEX.md                   # Índice completo de arquivos
    ├── DEPENDENCY_GRAPH.md             # Grafo de dependências do projeto
    ├── METRICS.md                      # Métricas e contagem de código
    └── WORKSPACE_ANALYSIS.md           # Análise técnica do workspace
```

---

## Documentação

Abaixo estão os links diretos para **TODOS** os documentos da rede de conhecimento do repositório:

- **Instruções de Entrada & Operação de IA (`.ai/`)**:
  - [BOOTSTRAP_PROJECT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/BOOTSTRAP_PROJECT.md)
  - [AGENTS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AGENTS.md)
  - [AI_CONVENTIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AI_CONVENTIONS.md)
  - [GOVERNANCE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/GOVERNANCE.md)
  - [CONTEXT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/CONTEXT.md)
  - [MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md)
- **Documentação Técnica (`docs/`)**:
  - [ARCHITECTURE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/ARCHITECTURE.md)
  - [SPEC.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/SPEC.md)
  - [ROADMAP.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/ROADMAP.md)
  - [DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md)
  - [PATTERNS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/PATTERNS.md)
  - [EXAMPLES.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/EXAMPLES.md)
- **Artefatos de Análise e Auditoria (`.meta/`)**:
  - [AUDIT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/AUDIT.md)
  - [INVENTORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/INVENTORY.md)
  - [FILE_INDEX.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/FILE_INDEX.md)
  - [DEPENDENCY_GRAPH.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/DEPENDENCY_GRAPH.md)
  - [METRICS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/METRICS.md)
  - [WORKSPACE_ANALYSIS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/WORKSPACE_ANALYSIS.md)
- **Logs de Histórico (Raiz)**:
  - [CHANGELOG.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG.md)
  - [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md)

---

## Módulos Principais

1. **Dashboard (`/api/dashboard/summary`)**: Apresenta patrimônio líquido, saldos reais/projetados e despesas por categoria.
2. **Contas e Carteiras (`/api/accounts`)**: Gestão de contas correntes, investimentos e cartões de crédito.
3. **Transações (`/api/transactions`)**: Lançamento de despesas, receitas, compras parceladas e transferências.
4. **Faturas de Cartão (`/api/transactions/invoice`)**: Cálculo automatizado das janelas de fatura de cartão.
5. **Recorrências (`/api/recurring`)**: Automação de contas fixas e projeção de fluxo de caixa de até 36 meses.
6. **Metas Financeiras (`/api/goals`)**: Acompanhamento de metas com cálculo de aporte mensal sugerido.
7. **Orçamentos (`/api/budgets`)**: Teto de gastos por categoria.
8. **Investimentos (`/api/fiiholdings` & `/api/tesouro/latest`)**: Fundos Imobiliários e cotações do Tesouro Direto.
9. **Importação (`/api/import/upload`)**: Upload de extratos Nubank CSV/XLSX com auto-categorização.

---

## Instalação e Execução Local

### Executar a API (.NET)
```powershell
cd MyFinance.API
dotnet run
```
A API ficará acessível em `http://localhost:10000`. Swagger em `http://localhost:10000/swagger`.

### Executar o Web App (React)
```powershell
cd MyFinance.Web
npm install
npm run dev
```
O front-end iniciará em `http://localhost:5173`.

---

## Execução de Testes

- **Testes de Lógica (.NET InMemory)**: `dotnet test .\tests\backend\Finflow.Api.LogicTests\Finflow.Api.LogicTests.csproj`
- **Testes de Contrato HTTP (.NET)**: `.\tests\run-backend-tests.ps1`
- **Testes E2E (Playwright)**: `cd tests/frontend && npm test`

---

## Confidence

### Alta
- Tecnologias, pacotes, estrutura corporativa de arquivos e rotas validados diretamente contra a base de código.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
