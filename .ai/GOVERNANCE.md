# Governança e Diretrizes do Projeto Finflow

Este documento estabelece as políticas permanentes, regras de versionamento, convenções de código e diretrizes de manutenção do projeto **Finflow**.

---

## 1. Política Documental

- **Estrutura Corporativa Obrigatoria**:
  - **Raiz (`/`)**: `README.md`, `CHANGELOG.md`, `CHANGELOG_AI.md`.
  - **Diretório `docs/`**: `ARCHITECTURE.md`, `SPEC.md`, `ROADMAP.md`, `DECISIONS.md`, `PATTERNS.md`, `EXAMPLES.md`.
  - **Diretório `.ai/`**: `BOOTSTRAP_PROJECT.md`, `AGENTS.md`, `AI_CONVENTIONS.md`, `GOVERNANCE.md`, `CONTEXT.md`, `MEMORY.md`.
  - **Diretório `.meta/`**: `AUDIT.md`, `INVENTORY.md`, `FILE_INDEX.md`, `DEPENDENCY_GRAPH.md`, `METRICS.md`, `WORKSPACE_ANALYSIS.md`.
- **Fonte Primária da Verdade**: O código-fonte compilável e executável é a fonte máxima da verdade.
- **Rastreabilidade e Links**: Toda menção a um arquivo ou módulo deve obrigatoriamente criar um link Markdown utilizando o esquema `file:///...` apontando para o caminho no repositório.

---

## 2. Política Arquitetural e de Organização

- **Separação de Responsabilidades**: Manter a separação estrita entre o Front-end React SPA ([MyFinance.Web/](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/)) e a Web API ASP.NET Core ([MyFinance.API/](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/)).
- **Política para Novos Diretórios**: A criação de novos diretórios na raiz exige justificativa técnica formal e deve ser registrada em [docs/DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md).

---

## 3. Política de Memória e Preservação

- O arquivo [.ai/MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md) é restrito a fatos de longa duração do projeto. É proibido registrar tarefas temporárias ou logs de bugs pontuais.
- NUNCA remova informações úteis, regras de negócio passadas ou contexto de decisões sem justificativa técnica explícita.

---

## 4. Responsabilidades Humanas e das IAs

### Responsabilidades dos Agentes de IA:
- Inspecionar a auditoria técnica em [.meta/AUDIT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/AUDIT.md) antes de propor alterações.
- Manter o isolamento multi-tenant por `UserId` em todas as consultas EF Core.
- Registrar obrigatoriamente toda alteração documental ou de código em [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md).
- Respeitar o manual operacional em [.ai/AI_CONVENTIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AI_CONVENTIONS.md).

### Responsabilidades dos Desenvolvedores Humanos:
- Manter contribuições em conformidade com as suítes de teste de contrato e lógica.
- Atualizar [CHANGELOG.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG.md) ao lançar novas funcionalidades para produção.

---

## 5. Critérios para Reorganização e Auditorias Futuras

- **Critérios para Reorganização**: Qualquer movimentação de arquivos deve atualizar automaticamente 100% dos links de referência no esquema `file:///...`.
- **Critérios para Auditorias Futuras**: Os relatórios analíticos gerados por ferramentas ou IAs devem ser armazenados exclusivamente no diretório `.meta/`.

---

## Confidence

### Alta
- Políticas de governança corporativa e regras permanentes do projeto verificadas e alinhadas.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
