# Auditoria de Estabilidade do Projeto

## Escopo

Varredura estatica do front-end React e do back-end ASP.NET Core sem alterar codigo de producao.

## Achados principais

### 1. Riscos de loops ou chamadas repetidas no React

- `MyFinance.Web/src/pages/Home.jsx`
  - O carregamento do Dashboard depende de multiplas chamadas concorrentes e remount por mudanca de periodo. Hoje existe cancelamento, o que reduz risco de corrida.
  - Risco residual: o componente ainda carrega quatro fontes distintas e qualquer latencia elevada gera tela de loading prolongada.

- `MyFinance.Web/src/pages/Transactions.jsx`
  - `useEffect` depende de `month` e `year`, o que esta correto.
  - Risco residual baixo: `loadTransactions` nao e memoizado, mas nao participa das dependencias do `useEffect`.

- `MyFinance.Web/src/pages/Reports.jsx`
  - Mesmo padrao de recarga por periodo.
  - Sem indicio de loop infinito, mas o request e repetido a cada alteracao do periodo.

- `MyFinance.Web/src/pages/Profile.jsx`
  - `useEffect` chama `/users/me` sem `catch`.
  - Nao ha loop infinito, mas ha risco de erro nao tratado em runtime.

- `MyFinance.Web/src/pages/Invoices.jsx`
  - Ha mais de um `useEffect`, com dependencia em conta selecionada e mes atual.
  - Recomendacao futura: revisar acoplamento para evitar requests encadeadas sem cancelamento.

- `MyFinance.Web/src/components/AddTransactionModal.jsx`
  - Carrega categorias e contas com `Promise.all`.
  - Risco moderado de request concorrente em reaberturas muito rapidas do modal.

Conclusao sobre loops:

- Nao encontrei loop infinito obvio no estado atual.
- O risco principal nao e loop, e sim recarga concorrente sem estrategia centralizada de cache ou deduplicacao.

### 2. Riscos de falha de conexao

- `MyFinance.Web/src/services/api.js`
  - Ha timeout global de 20s e tratamento de timeout.
  - Ponto positivo: a app nao fica indefinidamente aguardando.
  - Ponto de melhoria: nao existe retry, fallback offline ou classificacao de erro por status HTTP.

- `MyFinance.Web/src/pages/Profile.jsx`
  - Chamada inicial a `/users/me` sem `try/catch` nem `.catch`.
  - Falha possivel: rejeicao nao tratada quando token expira ou API cai.

- `MyFinance.Web/src/pages/Invoices.jsx`
  - Ha mistura de `.then` e `try/catch`.
  - Falha possivel: falta de tratamento unificado pode produzir UX inconsistente.

- `MyFinance.API/Controllers/TesouroController.cs`
  - Consome arquivo CSV externo com `GetStreamAsync`.
  - Falta timeout explicito por client configurado, politicas de retry e circuit breaker.
  - Se a origem externa estiver lenta ou fora do ar, o endpoint falha.

- `MyFinance.API/Controllers/ImportController.cs`
  - O processamento percorre linha a linha do CSV no request HTTP.
  - Em arquivos grandes, pode elevar tempo de resposta e uso de memoria.

- `MyFinance.API/Program.cs`
  - Usa `EnableRetryOnFailure` no EF Core, o que ajuda na resiliencia com PostgreSQL.
  - Ponto de melhoria: faltam health checks para banco e dependencias externas.

- Supabase
  - Nao ha tratamento visivel para indisponibilidade alem da excecao natural do EF Core.
  - Nao ha camada de observabilidade para distinguir timeout, autenticacao, pool ou indisponibilidade do host.

### 3. Riscos de seguranca e escalabilidade

- `MyFinance.API/appsettings.json`
  - Ha segredo JWT e string de conexao reais versionados.
  - Esse e o achado mais critico da auditoria. Recomendacao imediata futura: rotacionar os segredos e mover tudo para variaveis de ambiente.

- Controllers concentram regra de negocio
  - Isso reduz testabilidade e dificulta crescimento do dominio.

- Falta de camada de servicos
  - Regras de saldo, transferencia, projecao e importacao estao distribuídas em controllers.

- Falta de testes automatizados nativos
  - Ate esta auditoria, nao havia suite de teste dedicada no repositiorio.

- Falta de health checks e telemetria
  - Sem endpoint de saude, sem tracing, sem metricas de latencia.

## Recomendacoes futuras

### Prioridade alta

- Remover segredos versionados do repositiorio e rotacionar credenciais.
- Adicionar observabilidade minima:
  - health checks
  - logs estruturados
  - identificacao de correlation/request id
- Criar pipeline CI para lint, build e testes.
- Padronizar tratamento de erros no front-end.

### Prioridade media

- Extrair logica de negocio dos controllers para servicos.
- Introduzir testes de integracao do back-end em ambiente isolado.
- Adicionar testes E2E do front-end para login e dashboard.
- Adicionar politicas resilientes para chamadas externas do Tesouro.

### Prioridade baixa

- Revisar encoding de arquivos com acentos inconsistentes.
- Padronizar respostas de erro da API.
- Introduzir cache seletivo no front-end para consultas de leitura frequente.

## Sumario executivo

- Nao foram identificados loops infinitos evidentes no React.
- O maior risco operacional atual e seguranca de credenciais expostas.
- O maior risco tecnico de medio prazo e acoplamento de regra de negocio nos controllers.
- O maior risco de disponibilidade esta em dependencias externas sem politica resiliente e em fluxos do front sem tratamento uniforme de erro.
