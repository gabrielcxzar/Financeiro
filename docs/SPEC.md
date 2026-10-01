# Finflow - Especificação de Requisitos

Este documento formaliza requisitos já confirmados pela implementação e os critérios verificáveis da evolução da plataforma **Finflow**.

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
- **RF-21**: Compras em cartão devem ser despesas operacionais; a liquidação da fatura deve movimentar os saldos da conta pagadora e do cartão sem criar nova receita, despesa ou categoria de gasto. A importação só deve escolher automaticamente o cartão quando houver uma correspondência inequívoca; ausência ou ambiguidade exige revisão manual.

### 1.7. Carteira de Investimentos e MCP
- **RF-22**: O sistema deve persistir renda fixa em entidade própria, separada de `accounts` e de `transactions`, incluindo o último saldo confirmado, sua data e origem. Características não comprovadas, como vencimento, liquidez, principal e imposto, devem aceitar `null`.
- **RF-23**: O sistema deve preservar `fii_holdings` e permitir persistir cotação manual com data e origem. Custo de aquisição (`shares × avg_price`) e valor de mercado (`shares × current_price`) devem permanecer conceitos distintos; ganho e retorno só podem ser calculados quando os dados necessários existirem.
- **RF-24**: A API autenticada deve permitir consultar e atualizar manualmente dados de renda fixa e de FIIs, sempre limitando operações ao usuário autenticado. Atualizações de valor não devem criar contas, transações ou movimentações operacionais.
- **RF-25**: `get_investment_positions` deve continuar somente leitura, isolar dados por usuário e informar renda fixa, FIIs, custos conhecidos, valores conhecidos, valores de mercado disponíveis, alocação e metadados de origem/data.
- **RF-26**: A alocação deve usar somente valuations persistidos em BRL e identificar que a base é formada por saldos confirmados de renda fixa e cotações de FIIs. Posições sem valuation utilizável ficam fora dos percentuais e são contabilizadas como não avaliadas.
- **RF-27**: Nenhum saldo de investimento deve ser calculado como estimativa sem dados suficientes; o saldo confirmado original deve permanecer preservado.
- **RF-28**: Os valores iniciais da carteira de um usuário devem ser cadastrados como dados do usuário em produção, nunca em migration, seed global ou código versionado.

## 2. Especificação verificável: carteira consultável pelo MCP

### Problema

O MCP atual não possui posições cadastradas, e [FiiHolding.cs](../MyFinance.API/Models/FiiHolding.cs) armazena apenas ticker, quantidade e preço médio. Não há entidade para o último saldo confirmado de renda fixa. `Account.Type = Investment`, definido em [Account.cs](../MyFinance.API/Models/Account.cs), participa dos cálculos de saldo de contas e do patrimônio do dashboard; usá-la para representar RDB/CDB poderia misturar investimentos com caixa e duplicar patrimônio.

### Comportamento esperado

- Manter `fii_holdings` por compatibilidade e acrescentar apenas os metadados necessários à cotação persistida.
- Criar uma entidade própria para renda fixa, com campos opcionais quando a característica não for conhecida.
- Tratar os FIIs e a renda fixa como posições do domínio de investimentos, sem relacioná-los a contas ou transações operacionais.
- Permitir atualizar manualmente saldo de renda fixa e cotação de FII, preservando data e origem; quando `BRAPI_API_KEY` estiver configurada, também permitir buscar cotações atuais por provider documentado.
- Não implementar rendimento estimado. Implementar uma abstração opcional de cotação para FIIs sobre a API documentada da brapi, com credencial configurada por ambiente, timeout, cache e `as_of`. A atualização é explícita pela API/UI; não há job em background. Se a credencial estiver ausente ou o provider falhar, preservar a última cotação válida e permitir cotação manual com origem/data. A API não deve apresentar snapshots antigos como atuais. A fonte oficial B3 exige contrato/licença para distribuição de Market Data.
- Devolver ao MCP valores e bases separados: custo conhecido; saldo confirmado; valor de mercado calculado a partir de cotação; ganho/perda; alocação sobre os últimos valuations persistidos; e origem/data por posição.
- Manter [FinancialSnapshotService.cs](../MyFinance.API/Services/FinancialSnapshotService.cs) e o dashboard operacional sem mudanças nesta entrega. O domínio de investimentos não passa a compor o patrimônio global nesta versão.

### Critérios de aceitação

1. A migration é aditiva, mantém as posições FII existentes, usa precisão monetária adequada, índice por `user_id`, unicidade de usuário + ticker e uma FK segura para a nova tabela.
2. Para cada FII, custo é `quantidade × preço médio`; valor de mercado é `quantidade × cotação` quando a cotação existe; ganho é mercado menos custo; retorno percentual é ganho dividido pelo custo quando o custo é maior que zero.
3. Cotação ausente não quebra a consulta e deixa valor de mercado, ganho e retorno nulos.
4. Renda fixa retorna saldo, data e origem confirmados; vencimento, liquidez e principal podem ser nulos; não há estimativa quando faltam dados.
5. O MCP retorna duas posições de renda fixa e quatro FIIs do usuário 1, agrega os valores a partir das posições individuais e marca a cobertura incompleta do custo de aquisição quando o principal da renda fixa é desconhecido.
6. Percentuais de alocação são derivados dos valores conhecidos do mesmo snapshot e expõem a base/data; posições sem valuation são excluídas com indicação explícita.
7. Testes demonstram isolamento por usuário, somente leitura MCP, ausência de gravações em transações, preservação do saldo Nubank e invariância do cálculo do cartão/resumo financeiro.
8. O provider opcional de cotação usa timeout e cache, registra fonte/data, e uma falha ou ausência de credencial não apaga o último preço válido; a cotação manual permanece disponível.
9. A página de investimentos apresenta resumo, renda fixa editável, FIIs com custo/mercado/resultado e datas/origens; deixa claro quando cotação manual ou provider está sendo usada.
10. Os dados pessoais iniciais são inseridos apenas em produção após validação do schema, fora de migration, seed e código-fonte.

### Tarefas ordenadas

1. Mapear `FiiHolding`, `Account`, `FinancialSnapshotService`, dashboard, `get_account_balances` e contratos MCP; confirmar onde o domínio atual inclui contas de investimento.
2. Implementar o modelo aditivo, migration, endpoints autenticados e cálculos do read model MCP; manter qualquer dado de carteira fora do ledger e dos snapshots globais.
3. Atualizar a página de investimentos para editar saldo conhecido de renda fixa e dados/cotação manual de FIIs.
4. Implementar provider brapi opcional com timeout/cache e fallback preservando a cotação atual; adicionar testes de lógica e regressão cobrindo cálculos, valores ausentes, isolamento, read-only e integridade operacional; executar build e suítes aplicáveis.
5. Validar migration e estado de dados numa branch Neon isolada, revisar SQL/schema e conferir que produção permanece inalterada antes do deploy.
6. Fazer staging seletivo, commit e push; acompanhar o auto-deploy, aplicar/confirmar migration em produção e inserir apenas as seis posições do usuário 1.
7. Validar MCP em produção, posições, origem/data e integridade de caixa, transações, faturas e dados de setembro.

### Riscos e controles

- **Valuation desatualizado**: cada saldo/cotação expõe data e origem; UI/MCP chamam os valores de últimos valores conhecidos e não os apresentam como cotações atuais.
- **Custo de renda fixa desconhecido**: `principal_amount` permanece nulo e o MCP marca o total de custo como parcial.
- **Divergência do agregador externo**: totais são calculados pelas posições individuais, sem ajuste contábil para coincidir com agregados do Investidor10.
- **Dupla contagem de patrimônio**: investimentos não são cadastrados como `Account`, não geram `Transaction` e não entram no `FinancialSnapshotService` ou dashboard nesta versão.
- **Dados de outro usuário**: endpoints e consultas filtram por `UserId`; a migration cria somente estrutura, e o cadastro inicial é operação de dados separada.
- **Deploy sem schema compatível**: validar a migration em branch Neon antes do push/deploy; em produção, a migration segue o bootstrap EF Core já existente.

---

## 3. Requisitos Não-Funcionais (RNF)

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
