# Convenções e Manual Operacional de Inteligência Artificial

Este documento estabelece o manual de comportamento operacional padrão para qualquer Agente de Inteligência Artificial atuando no projeto **Finflow**.

---

## 1. Convenções de Documentação

- **Localização Estrita**: Os documentos DEVEM respeitar a Estrutura Corporativa:
  - Raiz: `README.md`, `CHANGELOG.md`, `CHANGELOG_AI.md`.
  - Pasta `docs/`: Documentação técnica permanente (`ARCHITECTURE.md`, `SPEC.md`, `ROADMAP.md`, `DECISIONS.md`, `PATTERNS.md`, `EXAMPLES.md`).
  - Pasta `.ai/`: Diretrizes de IA e contexto (`BOOTSTRAP_PROJECT.md`, `AGENTS.md`, `AI_CONVENTIONS.md`, `GOVERNANCE.md`, `CONTEXT.md`, `MEMORY.md`).
  - Pasta `.meta/`: Artefatos analíticos (`AUDIT.md`, `INVENTORY.md`, `FILE_INDEX.md`, `DEPENDENCY_GRAPH.md`, `METRICS.md`, `WORKSPACE_ANALYSIS.md`).
- **Seções Obrigatórias de Confiança**: Todo arquivo de documentação alterado ou criado DEVE conter no final:
  ```markdown
  ## Confidence

  ### Alta
  [Itens validados pelo código]

  ### Média
  [Itens de docs existentes]

  ### Baixa
  [Inferências]

  ## Validação Humana Necessária
  [Ações manuais pendentes]
  ```

---

## 2. Convenções de Nomenclatura e Arquivos

- **Arquivos de Documentação**: Sempre em MAIÚSCULAS com extensão `.md` (ex: `ARCHITECTURE.md`, `SPEC.md`).
- **Arquivos de Código C#**: PascalCase no nome da classe e do arquivo (ex: `TransactionsController.cs`, `FinancialSnapshotService.cs`).
- **Arquivos de Código React**: PascalCase para componentes/páginas `.jsx` (ex: `AddTransactionModal.jsx`, `Transactions.jsx`) e camelCase para utilitários (ex: `api.js`).

---

## 3. Regras para Edição e Criação de Arquivos

### Quando criar novos documentos:
- Apenas quando um novo domínio ou módulo técnico for introduzido e não se enquadrar em nenhum dos documentos existentes.
- O novo documento deve ser alocado na subpasta apropriada (`docs/`, `.ai/` ou `.meta/`).

### Quando NÃO criar novos documentos:
- Nunca crie documentos redundantes (ex: `README_2.md`, `NOTES.md`, `TEMP.md`).
- Se a informação se refere à arquitetura, atualize [docs/ARCHITECTURE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/ARCHITECTURE.md).
- Se a informação se refere a regras funcionais, atualize [docs/SPEC.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/SPEC.md).

---

## 4. Gatilhos de Atualização

- **Quando atualizar MEMORY ([.ai/MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md))**: Sempre que uma premissa arquitetural estável ou regra de banco permanente for confirmada ou alterada.
- **Quando atualizar DECISIONS ([docs/DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md))**: Sempre que uma decisão arquitetural relevante for tomada (tecnologias, refatoração de serviços, modelos de dados).
- **Quando atualizar CHANGELOG_AI ([CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md))**: Obrigatoriamente ao final de TODA sessão em que a IA realizar alterações na documentação ou no código-fonte.

---

## 5. Políticas Permanentes

- **Política de Prevenção de Duplicações**: Antes de escrever novos parágrafos, consulte os documentos existentes. Faça referência por link Markdown em vez de copiar texto.
- **Política para Informações Inferidas**: Toda informação não verificada diretamente no código deve conter o rótulo **Inferência**.
- **Política de Rastreabilidade**: Todo nome de arquivo ou classe mencionado em uma documentação DEVE ser acompanhado pelo seu link formatado no esquema `file:///...`.
- **Política de Referências Cruzadas**: Documentos devem citar uns aos outros formando uma rede navegável.
- **Política de Reorganização Futura**: Se no futuro novos arquivos precisarem ser movidos, a IA responsável deve atualizar automaticamente 100% dos links de referência no repositório.

---

## Confidence

### Alta
- Convenções e regras operacionais de IA alinhadas com as diretrizes corporativas do projeto.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
