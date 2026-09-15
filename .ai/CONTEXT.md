# Finflow - Contexto Funcional e Estrutural do Sistema

Este documento formaliza o contexto funcional completo, a topologia dos ambientes, a arquitetura do sistema e a explicação detalhada da estrutura de diretórios do projeto **Finflow**.

---

## 1. Visão Geral do Sistema

O Finflow é uma plataforma full-stack para gestão financeira pessoal com arquitetura desacoplada:

- **Front-end**: Single Page Application (SPA) em React 18, Vite, Ant Design, styled-components, Axios e Chart.js.
- **Back-end**: Web API RESTful em ASP.NET Core 8 (.NET 8) com C#, utilizando Entity Framework Core 8 para persistência.
- **Banco de Dados**: PostgreSQL relacional hospedado na plataforma Supabase.

O front-end consome exclusivamente a API HTTP do back-end através de chamadas assíncronas JSON. O banco de dados PostgreSQL é inacessível para o front-end.

---

## 2. Estrutura do Projeto

Abaixo está o detalhamento de cada diretório do repositório, seu objetivo, responsabilidade, conteúdo esperado e relacionamento com outros diretórios:

### 2.1. `docs/`
- **Objetivo**: Centralizar a documentação técnica oficial e permanente do sistema.
- **Responsabilidade**: Manter as especificações arquiteturais, requisitos, débitos técnicos, decisões (ADRs), padrões de código e exemplos de uso.
- **Conteúdo Esperado**: `ARCHITECTURE.md`, `SPEC.md`, `ROADMAP.md`, `DECISIONS.md`, `PATTERNS.md`, `EXAMPLES.md`.
- **Relacionamento**: Referenciado por [README.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/README.md), [.ai/BOOTSTRAP_PROJECT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/BOOTSTRAP_PROJECT.md) e [.ai/AGENTS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AGENTS.md).

### 2.2. `.ai/`
- **Objetivo**: Prover diretrizes de comportamento, contexto funcional e instrução operacional para Agentes de Inteligência Artificial.
- **Responsabilidade**: Garantir o onboarding rápido de IAs sem dependência de comandos proprietários, preservando governança e memória permanente.
- **Conteúdo Esperado**: `BOOTSTRAP_PROJECT.md`, `AGENTS.md`, `AI_CONVENTIONS.md`, `GOVERNANCE.md`, `CONTEXT.md`, `MEMORY.md`.
- **Relacionamento**: Serve como o guia de entrada para IAs explorando a base de código e a pasta `docs/`.

### 2.3. `.meta/`
- **Objetivo**: Armazenar artefatos analíticos de auditoria técnica e mapeamento estático produzidos por ferramentas automatizadas ou análises da IA.
- **Responsabilidade**: Isolamento de relatórios e métricas que não pertencem à documentação permanente de negócio.
- **Conteúdo Esperado**: `AUDIT.md`, `INVENTORY.md`, `FILE_INDEX.md`, `DEPENDENCY_GRAPH.md`, `METRICS.md`, `WORKSPACE_ANALYSIS.md`.
- **Relacionamento**: Consultado por IAs durante auditorias antes da modificação do código.

### 2.4. `MyFinance.API/`
- **Objetivo**: Manter a aplicação Web API em ASP.NET Core 8.
- **Responsabilidade**: Expor endpoints RESTful, validar autenticação JWT, executar regras de negócio e realizar a persistência no PostgreSQL.
- **Conteúdo Esperado**: Controllers (`Controllers/`), Models (`Models/`), Contexto EF Core (`Data/`), Serviço de Snapshot (`Services/`), Migrations (`Migrations/`) e arquivo de projeto (`MyFinance.API.csproj`).
- **Relacionamento**: Consumido via HTTP pelo `MyFinance.Web/` e testado pelas suítes em `tests/backend/`.

### 2.5. `MyFinance.Web/`
- **Objetivo**: Manter a aplicação Web Front-end em React 18 e Vite.
- **Responsabilidade**: Renderizar a interface rica para o usuário, gerenciar login/sessão e interagir com a API RESTful.
- **Conteúdo Esperado**: Componentes (`src/components/`), Páginas (`src/pages/`), Cliente Axios (`src/services/api.js`), Shell (`src/App.jsx`) e `package.json`.
- **Relacionamento**: Consome a `MyFinance.API/` e é testado pela suíte E2E em `tests/frontend/`.

### 2.6. `tests/`
- **Objetivo**: Manter as suítes de testes automatizados do sistema.
- **Responsabilidade**: Garantir a regressão zero da lógica do back-end, contratos HTTP da API e navegação E2E do front-end.
- **Conteúdo Esperado**: `backend/Finflow.Api.LogicTests` (InMemory), `backend/Finflow.Api.ContractTests` (HTTP real), `frontend/e2e` (Playwright) e scripts PowerShell de execução.
- **Relacionamento**: Executa testes contra `MyFinance.API/` e `MyFinance.Web/`.

---

## 3. Arquitetura Resumida e Ciclo do Projeto

1. **Topologia**: O usuário acessa o front-end React SPA. O front-end envia requisições HTTP para a API ASP.NET Core no Render. A API autentica o JWT e realiza consultas filtradas por `UserId` no PostgreSQL do Supabase.
2. **Tecnologias**: .NET 8, C#, EF Core 8, PostgreSQL, JWT, BCrypt, React 18, Vite, Ant Design, Axios, Chart.js, xUnit, Playwright.
3. **Módulos**: Auth, Contas/Carteiras, Transações/Parcelamentos, Faturas, Recorrências, Metas, Orçamentos, Investimentos (FIIs + Tesouro Direto), Importação de Extratos, Dashboard e Perfil.

---

## 4. Glossário de Consultas Financeiras

- **Movimentação operacional**: transação que representa consumo ou renda econômica nos relatórios. No modelo atual, possui `ReportingKind = normal`, não é transferência e não está excluída de relatórios.
- **Liquidação**: movimentação que quita ou transfere uma obrigação já reconhecida, como pagamento de fatura; não deve ser contada novamente como consumo.
- **Saldo conhecido**: valor contábil derivável do saldo inicial e das transações persistidas no FinFlow. Não implica disponibilidade bancária em tempo real.
- **Patrimônio contábil conhecido**: soma dos saldos conhecidos menos passivos de cartão conhecidos. Não equivale a patrimônio de mercado quando faltam cotações atuais.
- **Data financeira**: data civil usada para atribuir uma movimentação a um período de relatório. Para a feature MCP proposta, o timezone canônico é `America/Sao_Paulo`, sujeito à validação dos registros legados.
- **Acesso MCP somente leitura** *(implementado parcialmente em 2026-09-14)*: autorização com escopo mínimo para consultar agregados e detalhes limitados, sem capacidade de criar, alterar, importar, transferir ou excluir dados.

O acesso MCP somente leitura está implementado na API em `/mcp`, com OAuth 2.1/PKCE via OpenIddict, escopo `finflow.read`, pré-registro explícito de cliente e as oito tools descritas em [docs/FINFLOW_MCP/CONTRACTS.md](file:///C:/Users/Gabriel/Documents/Projetos%20dev/Financeiro/docs/FINFLOW_MCP/CONTRACTS.md). A validação PostgreSQL, de produção e do cliente ChatGPT permanece dependente da configuração do ambiente.

---

## Confidence

### Alta
- Estrutura de diretórios, responsabilidades, semântica de movimentação operacional e conceitos de saldo foram verificados diretamente no repositório.

### Média
- A semântica de datas legadas e a validação de produção/cliente ainda requerem evidência operacional.

### Baixa
- N/A.

## Validação Humana Necessária
- Confirmar a interpretação de datas financeiras legadas antes da implementação do MCP.
