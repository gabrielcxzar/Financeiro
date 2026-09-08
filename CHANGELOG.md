# Finflow - Histórico de Lançamentos do Projeto (CHANGELOG)

Todas as alterações notáveis, novas funcionalidades e correções importantes do projeto **Finflow** serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/) e este projeto adere ao [Versioning Semântico](https://semver.org/lang/pt-BR/).

## [1.1.0] - 2026-09-08

### Modificado
- Redesenho completo do front-end (`MyFinance.Web`) adotando o **Finflow Minimalist Core** (Swiss Minimalist) via **Google Stitch MCP**:
  - Nova paleta neutra e moderna (`#0F172A`, `#F8FAFC`, `#E2E8F0`, `#10B981`, `#F43F5E`) substituindo o tema gradiente laranja antigo.
  - Tipografia de alta precisão (`Plus Jakarta Sans` para títulos e `Inter` com `tnum` para valores monetários e tabelas).
  - Sidebar unificada com novo logotipo tipográfico, navegação limpa e card de perfil/logout integrado no rodapé.
  - Dashboard modernizado com novos cards de saldo/receitas/despesas/disponível e gráficos minimalistas (`DashboardCharts.jsx`).
  - Extrato de Transações (`Transactions.jsx`) enriquecido com mini-cards de resumo do período e badges limpos de categoria.
  - Carteiras e Contas (`Accounts.jsx`) com novos cartões de crédito em dark slate elegante e cards de saldo com micro-sombras.
  - Faturas (`Invoices.jsx`), Investimentos (`Investments.jsx`), Orçamentos (`Budgets.jsx`) e Metas (`Goals.jsx`) padronizados.

---

## [1.0.0] - 2026-07-21

### Adicionado
- Estruturação completa da documentação corporativa para desenvolvimento assistido por IA nas pastas `docs/`, `.ai/` e `.meta/`.
- Suíte de testes automatizados de lógica de negócio em [Finflow.Api.LogicTests.csproj](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.LogicTests/Finflow.Api.LogicTests.csproj).
- Suíte de testes de contrato HTTP em [Finflow.Api.ContractTests.csproj](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.ContractTests/Finflow.Api.ContractTests.csproj).
- Suíte de testes E2E com Playwright em [login-dashboard.spec.js](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/frontend/e2e/login-dashboard.spec.js).
- Módulo de Metas Financeiras (`/api/goals`) com acompanhamento de progresso e sugestão de aporte mensal.
- Serviço encapsulado `FinancialSnapshotService` para cálculo de saldos reais, pendentes, projetados e passivos de faturas.

### Modificado
- Reorganização de toda a rede de documentação para eliminar poluição visual na raiz do repositório.
- Unificação das referências de links para o esquema `file:///...`.

---

## Confidence

### Alta
- Lançamentos e marcos técnicos validados diretamente na suíte de testes e na árvore de commits.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
