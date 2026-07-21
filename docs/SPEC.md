# Finflow - Especificação de Requisitos

Este documento formaliza os Requisitos Funcionais (RF) e Não-Funcionais (RNF) estritamente confirmados pela implementação atual da plataforma **Finflow**.

---

## 1. Requisitos Funcionais (RF)

### 1.1. Autenticação e Usuários
- **RF-01**: O sistema deve permitir o cadastro de novos usuários com nome, e-mail único e senha.
- **RF-02**: A senha do usuário deve ser criptografada via BCrypt antes de ser salva no banco de dados.
- **RF-03**: Ao cadastrar um usuário, o sistema deve provisionar automaticamente 18 categorias padrão via [DefaultCategories.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Data/DefaultCategories.cs).
- **RF-04**: O login deve autenticar o e-mail e senha e retornar um token JWT válido por 30 dias.
- **RF-05**: O sistema deve oferecer funcionalidade de "Reset de Dados" (`POST /api/users/wipe-data`), apagando todas as transações, contas e orçamentos do usuário e recriando as categorias padrão.

### 1.2. Contas e Cartões de Crédito
- **RF-06**: O sistema deve permitir o cadastramento de contas bancárias dos tipos `Checking` e `Investment`.
- **RF-07**: O sistema deve permitir marcar uma conta como cartão de crédito (`IsCreditCard = true`), configurando limite de crédito, dia de fechamento e dia de vencimento.
- **RF-08**: O sistema deve permitir o reajuste manual de saldo (`POST /api/accounts/adjust-balance`), gerando uma transação automática de ajuste.

### 1.3. Transações e Transferências
- **RF-09**: O sistema deve permitir o lançamento de receitas e despesas com descrição, valor, data, conta, categoria e status de pago/não pago.
- **RF-10**: O sistema deve suportar compras parceladas em até N vezes, criando lançamentos individuais vinculados por um `InstallmentId`.
- **RF-11**: O sistema deve permitir atualizar ou remover uma parcela individual ou toda a série de parcelas simultaneamente.
- **RF-12**: O sistema deve permitir a transferência entre duas contas do usuário (`POST /api/transactions/transfer`), criando automaticamente um par de transações vinculadas por `TransferGroupId`.

### 1.4. Faturas, Recorrências e Orçamentos
- **RF-13**: O sistema deve calcular o ciclo mensal da fatura de cartão de crédito e retornar o valor total acumulado e a lista de lançamentos (`GET /api/transactions/invoice`).
- **RF-14**: O sistema deve permitir cadastrar regras de receitas/despesas recorrentes mensais e gerar em lote os lançamentos para um mês específico (`POST /api/recurring/generate`).
- **RF-15**: O sistema deve calcular a projeção de fluxo de caixa para até 36 meses (`GET /api/recurring/projection`).
- **RF-16**: O sistema deve permitir definir teto de orçamento mensal por categoria (`POST /api/budgets`).

### 1.5. Metas Financeiras e Investimentos
- **RF-17**: O sistema deve permitir criar metas financeiras de poupança ou quitação de dívidas, exibindo percentual de progresso e cálculo da contribuição mensal sugerida.
- **RF-18**: O sistema deve permitir gerenciar carteira de Fundos Imobiliários (`FiiHolding`).
- **RF-19**: O sistema deve consultar e exibir as taxas e preços atualizados do Tesouro Direto obtidos do dataset público do Tesouro Transparente.

### 1.6. Importação de Extratos
- **RF-20**: O sistema deve permitir o upload de extratos bancários nos formatos CSV e XLSX, com suporte a detecção de layout do Nubank, auto-categorização e pareamento de pagamentos de fatura.

---

## 2. Requisitos Não-Funcionais (RNF)

- **RNF-01 (Segurança)**: Toda a comunicação deve ser realizada sobre protocolo seguro HTTP/HTTPS com autenticação stateless por JWT Bearer em todas as rotas protegidas por `[Authorize]`.
- **RNF-02 (Isolamento Multi-tenant)**: A API deve obrigatoriamente garantir que um usuário nunca acesse ou modifique dados pertencentes a outro usuário (`UserId == GetUserId()`).
- **RNF-03 (Performance)**: As consultas de leitura intensiva do back-end devem utilizar `.AsNoTracking()` do Entity Framework Core.
- **RNF-04 (Resiliência)**: O consumo da API externa do Tesouro Direto deve utilizar `IMemoryCache` com tempo de expiração de 60 minutos para evitar falhas por indisponibilidade da origem.
- **RNF-05 (Responsividade)**: O front-end React deve adaptar sua interface para telas mobile e desktop utilizando os breakpoints do Ant Design.

---

## Confidence

### Alta
- Todos os requisitos funcionais e não-funcionais foram validados diretamente na suíte de testes de código e nos controllers da API.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
