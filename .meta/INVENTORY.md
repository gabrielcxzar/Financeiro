# Finflow - Inventário Analítico do Repositório

Este artefato analítico cataloga todos os componentes, controllers, modelos, serviços, páginas e suítes de teste do projeto **Finflow**.

---

## 1. Back-end (`MyFinance.API`)

### 1.1. Controllers RESTful (`MyFinance.API/Controllers/`)
1. [AccountsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/AccountsController.cs) - Gestão de contas, limites de cartão e reajuste manual de saldo.
2. [AuthController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/AuthController.cs) - Autenticação, login, registro e criação de categorias padrão.
3. [BudgetsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/BudgetsController.cs) - Teto de orçamento mensal por categoria.
4. [CategoriesController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/CategoriesController.cs) - CRUD de categorias.
5. [DashboardSummaryController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/DashboardSummaryController.cs) - Agregação do dashboard, net worth e Free to Spend.
6. [FiiHoldingsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/FiiHoldingsController.cs) - Posições em Fundos Imobiliários.
7. [GoalsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/GoalsController.cs) - Metas financeiras e sugestão de aporte.
8. [ImportController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/ImportController.cs) - Importação de extratos Nubank CSV/XLSX.
9. [RecurringController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/RecurringController.cs) - Regras de contas fixas e projeções.
10. [TesouroController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/TesouroController.cs) - Cotações do Tesouro Direto.
11. [TransactionsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/TransactionsController.cs) - Transações, parcelamentos e transferências.
12. [UsersController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/UsersController.cs) - Consulta de perfil e reset de dados (`wipe-data`).

### 1.2. Entidades de Domínio (`MyFinance.API/Models/`)
1. [Account.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/Account.cs) - Modelo de Conta/Cartão.
2. [Budgets.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/Budgets.cs) - Modelo de Orçamento.
3. [Category.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/Category.cs) - Modelo de Categoria.
4. [FiiHolding.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/FiiHolding.cs) - Modelo de FII Holding.
5. [FinancialGoal.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/FinancialGoal.cs) - Modelo de Meta Financeira.
6. [RecurringTransaction.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/RecurringTransaction.cs) - Modelo de Recorrência.
7. [Transaction.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/Transaction.cs) - Modelo de Transação.
8. [Users.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Models/Users.cs) - Modelo de Usuário.

### 1.3. Serviços e Dados (`MyFinance.API/Services/` & `Data/`)
1. [FinancialSnapshotService.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Services/FinancialSnapshotService.cs) - Serviço centralizador de saldos e projeções.
2. [AppDbContext.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Data/AppDbContext.cs) - DbContext do EF Core.
3. [DefaultCategories.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Data/DefaultCategories.cs) - Gerador de categorias padrão.

---

## 2. Front-end (`MyFinance.Web`)

### 2.1. Páginas (`MyFinance.Web/src/pages/`)
1. `Accounts.jsx`, `Budgets.jsx`, `Categories.jsx`, `Goals.jsx`, `Home.jsx`, `Investments.jsx`, `Invoices.jsx`, `Login.jsx`, `Profile.jsx`, `Recurring.jsx`, `Reports.jsx`, `Transactions.jsx`.

### 2.2. Componentes e Modais (`MyFinance.Web/src/components/`)
1. `AddAccountModal.jsx`, `AddTransactionModal.jsx`, `AdjustBalanceModal.jsx`, `BrandLoading.jsx`, `DashboardCharts.jsx`, `HistoryChart.jsx`, `ImportModal.jsx`, `InputMoney.jsx`, `TransferModal.jsx`.

---

## Confidence

### Alta
- Inventário analítico mapeado diretamente da árvore de arquivos do repositório.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
