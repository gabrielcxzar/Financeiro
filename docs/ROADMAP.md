# Finflow - Roteiro Técnico e Débitos (Roadmap)

Este documento centraliza a lista de pendências técnicas, melhorias prioritárias, débitos de código e refatorações identificadas durante a auditoria técnica do repositório **Finflow**.

---

## 1. Prioridade Alta (Segurança e Estabilidade)

- [ ] **Padronização do Encoding do Token JWT**:
  - *Problema*: Em [Program.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Program.cs#L74), o JWT usa `Encoding.ASCII`, enquanto em [AuthController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/AuthController.cs#L87) usa `Encoding.UTF8`.
  - *Ação*: Unificar ambas as declarações para `Encoding.UTF8`.
- [ ] **Rotação e Remoção de Segredos Versionados**:
  - *Problema*: [appsettings.json](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/appsettings.json) contém valores default de chaves.
  - *Ação*: Garantir que segredos sejam injetados exclusivamente por variáveis de ambiente no Render (`AppSettings__Token` e `ConnectionStrings__DefaultConnection`).
- [ ] **Correção de Character Encoding de Arquivos**:
  - *Problema*: Arquivos como [UsersController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/UsersController.cs) possuem caracteres acentuados corrompidos em ANSI.
  - *Ação*: Re-salvar todos os arquivos `.cs` e `.jsx` em codificação **UTF-8 sem BOM**.
- [ ] **Consolidação dos Arquivos de Solução .NET**:
  - *Problema*: Existência redundante de `MyFinance.sln` na raiz e `MyFinance.API/MyFinance.API.sln`.
  - *Ação*: Remover a solução interna em `MyFinance.API` e incluir os projetos de teste ([Finflow.Api.LogicTests.csproj](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.LogicTests/Finflow.Api.LogicTests.csproj) e [Finflow.Api.ContractTests.csproj](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.ContractTests/Finflow.Api.ContractTests.csproj)) dentro da solução principal `MyFinance.sln`.

---

## 2. Prioridade Média (Refatoração e Qualidade)

- [ ] **Desacoplamento de Regras de Negócio dos Controllers**:
  - *Problema*: Regras de validação de compras parceladas, transferências e importação estão diretamente em [TransactionsController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/TransactionsController.cs) e [ImportController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/ImportController.cs).
  - *Ação*: Extrair serviços dedicados (`ITransactionService`, `IImportService`).
- [ ] **Padronização dos DTOs de Resposta de Erro**:
  - *Problema*: Retornos mistos de strings brutas e objetos JSON em retornos `BadRequest`.
  - *Ação*: Criar um middleware global de tratamento de exceções ou padronizar respostas de erro no formato ProblemDetails (`rfc7807`).
- [ ] **Inclusão de Pipeline de CI/CD**:
  - *Problema*: Não há pipeline no GitHub Actions para automatizar a compilação, linting e execução de testes a cada PR.
  - *Ação*: Criar o workflow `.github/workflows/ci.yml`.

---

## 3. Prioridade Baixa (Melhorias de UX e Funcionalidades)

- [ ] **Adição de Client-Side Routing (React Router)**:
  - *Problema*: Troca de tela baseada apenas em estado interno em [App.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/App.jsx).
  - *Ação*: Avaliar a introdução de React Router para permitir URLs amigáveis (`/dashboard`, `/transacoes`, `/carteiras`).
- [ ] **Mapeamento Unificado de Colunas no Banco (Snake Case)**:
  - *Problema*: Mistura de `categoryid` e `user_id` nos atributos `[Column]`.
  - *Ação*: Padronizar os mapeamentos de coluna em EF Core utilizando a convenção `snake_case` global via `EFCore.NamingConventions`.

---

## Confidence

### Alta
- Todos os débitos técnicos listados foram identificados diretamente durante a auditoria do código-fonte.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
