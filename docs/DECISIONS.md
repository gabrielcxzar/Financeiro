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

## ADR-004: MCP integrado e OAuth com cliente pré-registrado

- **Data**: 2026-09-14
- **Contexto**: O FinFlow precisa oferecer consultas financeiras somente leitura a um cliente MCP, sem criar um segundo serviço nem reutilizar o JWT de sessão REST.
- **Decisão**: Integrar Streamable HTTP stateless em `/mcp` à API ASP.NET Core 8. Usar OpenIddict para Authorization Code + PKCE (S256), escopo `finflow.read`, resource/audience canônica `/mcp`, tokens de referência de curta duração e refresh rotation/revogação. Clientes são pré-registrados com callbacks HTTPS exatos; descoberta de cliente dinâmica não faz parte da v1.
- **Motivação**: Manter uma fronteira de autorização explícita e auditável para um endpoint público, com minimização, isolamento por sujeito e allowlist fixa de oito tools read-only.
- **Alternativas consideradas**:
  1. Serviço MCP separado, que aumentaria superfície operacional e duplicaria autenticação.
  2. API key ou reutilização do JWT REST de 30 dias, rejeitados por não atenderem ao modelo OAuth aprovado.
  3. Dynamic Client Registration/CIMD, reservados para uma revisão futura após validação do cliente.
- **Impacto**: Produção exige issuer HTTPS, certificados persistentes e cadastro das credenciais/callbacks do cliente no secret store do Render. Validação PostgreSQL e conexão real com ChatGPT permanecem gates de liberação.

---

## ADR-006: Posições de investimento fora do ledger operacional

- **Data**: 2026-10-01
- **Contexto**: [FiiHolding.cs](../MyFinance.API/Models/FiiHolding.cs) registra apenas ticker, cotas e preço médio. RDB/CDB não têm entidade própria. Contas não-cartão, inclusive `Account.Type = Investment` em [Account.cs](../MyFinance.API/Models/Account.cs), entram no saldo de caixa e no patrimônio do dashboard por [FinancialSnapshotService.cs](../MyFinance.API/Services/FinancialSnapshotService.cs).
- **Decisão**: Manter `fii_holdings` e acrescentar somente metadados de cotação manual (`current_price`, data e origem); criar uma tabela própria, aditiva e isolada por usuário para posições de renda fixa. Não representar investimentos como `Account` ou `Transaction`, nem incluir o novo domínio no patrimônio consolidado nesta versão. Não implementar rendimento estimado nem cotações automáticas; valores confirmados e cotações manuais preservam tipo, origem e data.
- **Motivação**: Aproveitar o contrato FII existente, reduzir risco de migração e manter saldo operacional, investimento confirmado e cotação de mercado como conceitos separados. O MCP pode compor alocação com os valores mais recentes conhecidos, identificando sua base e data.
- **Alternativas consideradas**:
  1. Migrar FIIs para uma tabela genérica de posições: mais uniforme, porém amplia o backfill, altera contratos existentes e aumenta o risco sem necessidade para renda fixa.
  2. Usar `Account.Type = Investment`: rejeitado porque a conta entra nos cálculos operacionais de saldo e patrimônio e poderia duplicar a posição quando se cadastrasse também o ativo real.
  3. Cotação automática: a [B3 documenta APIs de mercado](https://www.b3.com.br/pt_br/market-data-e-indices/servicos-de-dados/b3-for-developers/) e informa que a distribuição do Market Data exige contrato/licença; a [brapi documenta cotação de FIIs](https://brapi.dev/docs/fiis) e autenticação por token para cobertura além dos tickers de teste. Sem contrato ou credencial ampla disponível e validada para este deploy, a cotação automática não entra nesta versão.
- **Impacto**: Os FIIs mantêm sua identidade e unicidade atuais; custo, valor de mercado, ganho e retorno são calculados a partir das posições. Renda fixa guarda saldo confirmado e características conhecidas, deixando nulos os dados desconhecidos. O principal de renda fixa não informado deixa o total de custo de aquisição explicitamente parcial.
- **Riscos**: Valuations manuais podem ficar desatualizados, então data/origem são retornadas e exibidas. Valores de mercado de FIIs e saldos confirmados de renda fixa são bases diferentes, ainda que a alocação os compare como últimos valores BRL conhecidos. A migration cria estrutura apenas; os registros pessoais iniciais são inseridos separadamente em produção.

## ADR-007: Cotação externa opcional de FIIs

- **Data**: 2026-10-01
- **Contexto**: A ADR-006 manteve as cotações manuais por não haver credencial ampla validada no deploy. A documentação oficial da [brapi](https://brapi.dev/docs/fiis) apresenta endpoint de cotação de FIIs, timestamp de mercado e autenticação por token. A [B3](https://www.b3.com.br/pt_br/market-data-e-indices/servicos-de-dados/b3-for-developers/) exige contrato/licença para distribuição de Market Data.
- **Decisão**: Adicionar provider brapi opcional atrás de uma interface de cotação, ativada apenas com `BRAPI_API_KEY`. A busca será explícita, autenticada e iniciada pela UI/API, com timeout e cache. O provider retorna fonte e horário; em erro ou falta de credencial, a cotação persistida não muda. A entrada manual continua disponível. Esta decisão atualiza a parte de cotações da ADR-006; a separação do ledger e a ausência de rendimento estimado permanecem.
- **Motivação**: Usar uma API documentada com metadados de cotação, sem scraping nem acoplamento do domínio ao fornecedor, mantendo fallback claro quando não houver credencial ou disponibilidade.
- **Alternativas consideradas**:
  1. Manter somente cotações manuais; não atende ao pedido de aproximar FIIs de valores atuais quando existe provider documentado.
  2. Consumir diretamente Market Data B3; exige contrato/licença comercial.
  3. Atualização automática em background; adiada para evitar chamadas/credenciais recorrentes sem infraestrutura de agendamento definida.
- **Impacto**: A aplicação exige configuração opcional da credencial no ambiente do serviço para cotações amplas. Dados manuais funcionam sem ela e permanecem identificados por origem e data.
- **Riscos**: A fonte externa pode falhar, limitar acesso ou retornar dado atrasado; timeout/cache, preservação do último valor e timestamp visível limitam o risco. Cotação não equivale a preço garantido de execução.
## Confidence

### Alta
- Todas as ADRs registram decisões históricas reais comprovadas pelas estruturas de código e commits do projeto.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
