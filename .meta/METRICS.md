# Finflow - Métricas e Análise de Código

Este artefato analítico apresenta as métricas de volume de código, quantidade de arquivos e cobertura de testes do projeto **Finflow**.

---

## 1. Métricas Gerais de Código

| Módulo | Tipo de Projeto | Arquivos Principais | Linhas Aproximadas | Tecnologias Principais |
| :--- | :--- | :--- | :--- | :--- |
| **MyFinance.API** | Web API (.NET 8) | 12 Controllers, 8 Models, 1 Service, 1 DbContext | ~3.500 LOC | C#, EF Core, JWT, Npgsql |
| **MyFinance.Web** | SPA (React 18) | 13 Páginas, 10 Modais, Shell App.jsx, api.js | ~4.200 LOC | React, Vite, Ant Design, Axios |
| **Finflow.Api.LogicTests** | Testes Unitários/Lógica | `FinancialCoreLogicTests.cs` | ~1.200 LOC | xUnit, EF Core InMemory |
| **Finflow.Api.ContractTests** | Testes de Contrato HTTP | 4 arquivos de teste + `FinflowApiClient` | ~500 LOC | xUnit, System.Net.Http |
| **tests/frontend** | Testes E2E | `login-dashboard.spec.js` | ~100 LOC | Playwright |
| **Documentação (`docs/`, `.ai/`, `.meta/`)** | Markdown Corporativo | 21 arquivos `.md` | ~2.800 LOC | GitHub Flavored Markdown |

---

## 2. Pontos Críticos de Volume de Código (Hotspots)

1. [ImportController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/ImportController.cs): ~824 linhas - Requer futura extração para `IImportService`.
2. [TransactionsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/TransactionsController.cs): ~656 linhas - Requer futura extração para `ITransactionService`.
3. [DashboardSummaryController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/DashboardSummaryController.cs): ~548 linhas.
4. [Home.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/pages/Home.jsx): ~600 linhas.

---

## Confidence

### Alta
- Métricas e linhas de código estimadas a partir da leitura direta dos arquivos do repositório.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
