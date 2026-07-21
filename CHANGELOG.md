# Finflow - Histórico de Lançamentos do Projeto (CHANGELOG)

Todas as alterações notáveis, novas funcionalidades e correções importantes do projeto **Finflow** serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/) e este projeto adere ao [Versioning Semântico](https://semver.org/lang/pt-BR/).

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
