# Finflow - Memória Permanente do Projeto

Este documento armazena o conhecimento permanente, estável e consolidado do projeto **Finflow**. As informações contidas aqui devem ser preservadas entre diferentes sessões de trabalho e consultas por agentes de IA ou desenvolvedores.

---

## 1. Premissas Arquiteturais Consolidadas

1. **Stateless JWT Authentication**: O back-end não armazena sessões de usuário em banco ou memória. Toda a autenticação é stateless via Token JWT assinado contendo o `ClaimTypes.NameIdentifier`.
2. **Isolamento Estrito por Usuário**: Todas as tabelas de domínio (`accounts`, `transactions`, `categories`, `budgets`, `recurring_transactions`, `financial_goals`, `fii_holdings`) contêm obrigatoriamente a coluna `user_id`. Nenhuma consulta de leitura ou gravação pode ser feita sem o filtro por `UserId`.
3. **Cartões de Crédito como Subtipo de Conta**: Cartões de crédito não possuem tabela separada; são registros na tabela `accounts` com a flag `is_credit_card = true` e utilizam os campos `closing_day`, `due_day` e `credit_limit`.
4. **Calculador Único de Snapshot**: O cálculo de saldos consolidados reais, pendentes, projetados e passivos de fatura é realizado exclusivamente pela classe [FinancialSnapshotService.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Services/FinancialSnapshotService.cs).
5. **Navegação no Front-end por Estado**: O front-end React em [App.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/App.jsx) gerencia as telas por chave em estado local (`activeKey`) sem utilizar rotas baseadas em URL.
6. **Liquidação de Fatura Não é Operação**: A compra no cartão é a despesa operacional. O pagamento da fatura é um par de transferência com `ReportingKind = invoice_payment`, `ExcludeFromReports = true` e `TransferGroupId` compartilhado; ele altera caixa/passivo, mas não receita, despesa nem categorias dos relatórios.
7. **MCP Somente Leitura em Produção**: O endpoint `/mcp` usa OAuth 2.1 Authorization Code + PKCE, escopo `finflow.read` e oito tools read-only; o fluxo real com ChatGPT foi validado em produção.

---

## 2. Invariantes do Banco de Dados

- **Timestamp Legacy Behavior**: O PostgreSQL via Npgsql utiliza a flag global `AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);` configurada em [Program.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Program.cs) para tratar datas sem fuso horário.
- **Categorias Padrão por Usuário**: Ao cadastrar um novo usuário, o sistema invoca obrigatoriamente [DefaultCategories.Create(userId)](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Data/DefaultCategories.cs) para preencher 18 categorias pré-definidas.

---

## 3. Integrações Externas Estáveis

- **Tesouro Transparente**: O endpoint `/api/tesouro/latest` realiza o download assíncrono dos arquivos CSV do Tesouro Direto nos domínios oficiais do governo federal e armazena os dados parseados em cache de memória local por 60 minutos.

---

## 4. Protocolo de Inicialização Obrigatório

- **Primeira Ação Mandatória:** Antes de qualquer ação envolvendo arquivos, ler obrigatoriamente [.ai/AI_CONVENTIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AI_CONVENTIONS.md).
- **Validação de Destino:** Nunca criar, mover, copiar ou modificar arquivos sem validar previamente o diretório correto conforme a estrutura catalogada do projeto.
- **Proibição:** Não gerar arquivos na raiz do repositório quando existir um diretório apropriado definido pelas convenções.
- **Persistência:** Este protocolo deve ser considerado uma regra permanente para todas as tarefas futuras realizadas neste projeto.

---

## Confidence

### Alta
- Todas as premissas e invariantes foram confirmadas diretamente inspecionando o código-fonte em C# e React.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
