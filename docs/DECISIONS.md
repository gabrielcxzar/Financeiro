# Finflow - Registro Histórico de Decisões Arquiteturais (ADRs)

Este documento registra o histórico imutável das Decisões Arquiteturais (Architectural Decision Records - ADRs) tomadas na evolução do projeto **Finflow**.

---

## ADR-001: Separação de Arquitetura em SPA (React) e Web API Stateless (.NET 8)

- **Data**: 2026-06-15
- **Contexto**: A aplicação precisava de uma interface rica, responsiva e dinâmica para dispositivos móveis e desktop, separada de forma independente do processamento de regras financeiras.
- **Decisão**: Adotar a arquitetura desacoplada onde o front-end React é um projeto SPA estático hospedado em PaaS (Render/Vercel) e o back-end é uma Web API stateless em ASP.NET Core 8.
- **Motivação**: Permitir deploy independente de front-end e back-end, facilitar testes isolados de contrato e permitir futura reutilização da API para um app mobile nativo.
- **Alternativas Consideradas**:
  1. Aplicação Monolítica ASP.NET Core Razor Pages / MVC.
  2. Next.js Full-stack com Server Actions.
- **Impacto**: Exigiu o desenvolvimento de mecanismos de autenticação via Token JWT em [Program.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Program.cs) e um cliente Axios centralizado com interceptors em [api.js](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/services/api.js).

---

## ADR-002: Encapsulamento de Saldos e Projeções no `FinancialSnapshotService`

- **Data**: 2026-07-01
- **Contexto**: Os cálculos de saldo real, saldo pendente, saldo projetado, faturas de cartão e projeção de fluxo de caixa estavam pulverizados e duplicados entre diversos controllers (`AccountsController`, `TransactionsController`, `DashboardSummaryController`).
- **Decisão**: Criar o serviço [FinancialSnapshotService.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Services/FinancialSnapshotService.cs) para atuar como a única fonte da verdade para o cálculo de snapshots e projeções financeiras.
- **Motivação**: Centralizar a complexidade de regras de cartões de crédito (janela de fatura), compras parceladas, transferências internas e transações recorrentes.
- **Alternativas Consideradas**:
  1. Manter a lógica diretamente nas rotas das Controllers.
  2. Implementar Stored Procedures / Views no PostgreSQL no Supabase.
- **Impacto**: Maior testabilidade de lógica desacoplada da infraestrutura Web API via [FinancialCoreLogicTests.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.LogicTests/FinancialCoreLogicTests.cs).

---

## ADR-003: Modelagem de Cartões de Crédito na Tabela Única `accounts`

- **Data**: 2026-07-02
- **Contexto**: A inclusão de suporte a cartões de crédito exigia decidir se criaríamos uma nova tabela `credit_cards` ou reaproveitaríamos a estrutura existente de `accounts`.
- **Decisão**: Utilizar a flag `is_credit_card = true` na tabela `accounts`, estendendo os campos `closing_day`, `due_day` e `credit_limit`.
- **Motivação**: Simplificar os relacionamentos com transações e transferências sem duplicar chaves estrangeiras em `transactions`.
- **Alternativas Consideradas**:
  1. Criar uma entidade separada `CreditCard` com tabela dedicada.
- **Impacto**: Necessidade de tratar nos cálculos de saldo que contas do tipo cartão de crédito representam um passivo (dívida) em vez de um saldo positivo disponível.

---

## Confidence

### Alta
- Todas as ADRs registram decisões históricas reais comprovadas pelas estruturas de código e commits do projeto.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
