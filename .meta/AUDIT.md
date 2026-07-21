# Auditoria Completa do Projeto Finflow

Este documento apresenta o diagnóstico e a auditoria técnica de ponta a ponta do projeto **Finflow** (aplicação full-stack de Gestão Financeira Pessoal).

---

## 1. Arquitetura

- **Topologia Geral**:
  - **Front-end**: Single Page Application (SPA) em React 18 / Vite. Hospedado em plataforma PaaS (Render / Vercel).
  - **Back-end**: Web API RESTful em ASP.NET Core 8 (.NET 8). Hospedado no Render (porta dinâmica via `PORT` ou fallback na porta `10000`).
  - **Banco de Dados**: PostgreSQL relacional hospedado no Supabase, gerenciado via Entity Framework Core 8 (Code-First com Migrations).
  - **Integração Externa**: Consumo resiliente do dataset público em CSV do Tesouro Direto (`TesouroTransparente.gov.br`) via `IHttpClientFactory` e `IMemoryCache`.
- **Estrutura de Comunicação**:
  - Comunicação estritamente via requisições HTTP RESTful com payloads JSON.
  - Autenticação stateless via tokens **JWT (JSON Web Token)** com validade de 30 dias.
  - Front-end intercepta requisições via Axios, anexando o cabeçalho `Authorization: Bearer <token>`.
- **Organização Arquitetural**:
  - **Back-end**: Modelo monolítico orientado a Controllers ("Controller-Centric"). Possui um serviço encapsulado (`FinancialSnapshotService`) para cálculos consolidados de saldo, faturas, snapshot e projeção, mas grande parte das regras de validação, agrupamento de parcelas, transferências e importação ainda reside diretamente nos Controllers.
  - **Front-end**: Arquitetura orientada a componentes React. Navegação baseada em estado local (`activeKey`) centralizada em [App.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/App.jsx) sem biblioteca de roteamento de URL (React Router).

---

## 2. Módulos e Áreas Funcionais

| Módulo | Responsabilidade / Funcionalidades | Endpoints / Componentes Principais |
| :--- | :--- | :--- |
| **Autenticação (Auth)** | Cadastro de usuários, login, geração de JWT, hash de senha BCrypt e provisionamento inicial de categorias padrão. | `POST /api/auth/register`<br>`POST /api/auth/login`<br>`Login.jsx` |
| **Contas e Carteiras (Accounts)** | Gestão de contas bancárias, investimentos e cartões de crédito (com limite, dia de fechamento e vencimento). Reajuste manual de saldo. | `GET/POST/PUT/DELETE /api/accounts`<br>`POST /api/accounts/adjust-balance`<br>`Accounts.jsx` |
| **Transações (Transactions)** | CRUD de receitas e despesas, compras parceladas (com edição de série ou item individual), transferências entre contas com auto-pareamento. | `GET/POST/PUT/DELETE /api/transactions`<br>`POST /api/transactions/transfer`<br>`Transactions.jsx` |
| **Faturas de Cartão (Invoices)** | Cálculo automático do ciclo da fatura (data início, fechamento e vencimento), listagem de lançamentos do período e total devido. | `GET /api/transactions/invoice`<br>`Invoices.jsx` |
| **Recorrências (Recurring)** | Definição de regras de despesas/receitas fixas mensais, geração automática de transações para o mês alvo e projeção de fluxo de caixa (até 36 meses). | `GET/POST/DELETE /api/recurring`<br>`POST /api/recurring/generate`<br>`GET /api/recurring/projection`<br>`Recurring.jsx` |
| **Categorias (Categories)** | Categorização com ícone, cor e tipo (Income/Expense). Proteção contra exclusão de categorias em uso. | `GET/POST/DELETE /api/categories`<br>`Categories.jsx` |
| **Metas Financeiras (Goals)** | Definição de metas de economia ou quitação de dívidas, cálculo de progresso %, data alvo e sugestão de contribuição mensal. | `GET/POST/PUT/DELETE /api/goals`<br>`Goals.jsx` |
| **Orçamentos (Budgets)** | Definição de limites de gastos mensais por categoria, marcação de despesas essenciais e rollover. | `GET/POST/DELETE /api/budgets`<br>`Budgets.jsx` |
| **Investimentos (Investments)** | Posições em Fundos Imobiliários (FII Holdings) e cotações atualizadas das taxas do Tesouro Direto. | `GET/POST/DELETE /api/fiiholdings`<br>`GET /api/tesouro/latest`<br>`Investimentos.jsx` |
| **Importação (Import)** | Upload de arquivos CSV/XLSX (ex: extratos/faturas Nubank), detecção automática de layout, pareamento de pagamentos de fatura e categorização. | `POST /api/import/upload`<br>`ImportModal.jsx` |
| **Dashboard / Home** | Visão consolidada de patrimônio líquido, saldos, receitas/despesas do mês, gastos por categoria (gráficos), meta de gastos livres ("Free to Spend") e projeções. | `GET /api/dashboard/summary`<br>`Home.jsx`, `DashboardCharts.jsx` |
| **Perfil / Usuário (Users)** | Consulta de dados do perfil logado e funcionalidade de reset total de dados transacionais (`wipe-data`). | `GET /api/users/me`<br>`POST /api/users/wipe-data`<br>`Profile.jsx` |

---

## 3. Tecnologias

### Back-end & Persistência
- **Linguagem & Framework**: C# 12 / .NET 8 / ASP.NET Core Web API
- **Banco de Dados**: PostgreSQL (hospedado no Supabase)
- **ORM**: Entity Framework Core 8 (`Npgsql.EntityFrameworkCore.PostgreSQL` v8.0.0)
- **Segurança**: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` v8.0.0), `BCrypt.Net-Next` (v4.0.3)
- **Documentação API**: Swagger / OpenAPI (`Swashbuckle.AspNetCore` v6.6.2)

### Front-end
- **Linguagem & Ambiente**: JavaScript (ES Modules) / React 18.3.1 / Vite 7.2.4
- **Interface & Design System**: Ant Design (`antd` v6.0.0, `@ant-design/icons` v6.1.0, `antd-mobile` v5.41.1)
- **Estilização**: Styled Components (`styled-components` v6.1.19), React Icons (`react-icons` v5.5.0)
- **Gráficos & Visualização**: Chart.js v4.5.1, `react-chartjs-2` v5.3.1
- **Cliente HTTP**: Axios v1.13.2
- **Manipulação de Datas**: Day.js v1.11.19 (com locale `pt-br`)

### Testes & Qualidade
- **Testes Unitários/Lógica Back-end**: xUnit v2.9.2, EF Core InMemory v8.0.0
- **Testes de Contrato Back-end**: xUnit v2.9.2 exercitando requisições HTTP reais
- **Testes E2E Front-end**: Playwright (`@playwright/test` v1.54.2)
- **Linter**: ESLint v9.39.1 com plugins de React Hooks e React Refresh

---

## 4. Inconsistências Detectadas

1. **Encoding da Chave JWT**: `Program.cs` usa `Encoding.ASCII` enquanto `AuthController.cs` usa `Encoding.UTF8`.
2. **Encoding de Arquivos**: O arquivo `UsersController.cs` possui caracteres corrompidos em ANSI.
3. **Projetos de Teste fora da Solução**: `MyFinance.sln` contém apenas a API, deixando os testes desvinculados da solução principal.

---

## Confidence

### Alta
- Diagnósticos e inconsistências validados diretamente contra o código-fonte.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
