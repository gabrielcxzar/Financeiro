# Finflow Contexto de Arquitetura

## Visao geral

Finflow e uma aplicacao full stack para gestao financeira pessoal, com:

- Front-end React hospedado no Render.
- Back-end ASP.NET Core 8 hospedado no Render.
- Banco PostgreSQL hospedado no Supabase.

O front-end consome exclusivamente a API HTTP do back-end. O banco nao e acessado diretamente pelo front-end.

## Topologia dos ambientes

### 1. Front-end no Render

- Stack: React 18, Vite, Ant Design, styled-components, axios, Chart.js.
- Ponto de entrada: `MyFinance.Web/src/main.jsx`.
- Shell da aplicacao: `MyFinance.Web/src/App.jsx`.
- Cliente HTTP: `MyFinance.Web/src/services/api.js`.
- Variavel principal esperada:
  - `VITE_API_URL`: URL base da API publicada no Render, com sufixo `/api`.

Responsabilidades:

- Login e cadastro de usuarios.
- Persistencia local do token JWT em `localStorage`.
- Navegacao entre Dashboard, Transacoes, Contas, Recorrencias, Categorias, Orcamentos, Faturas, Perfil e Investimentos.
- Renderizacao de graficos e tabelas.

### 2. Back-end no Render

- Stack: ASP.NET Core 8, Entity Framework Core 8, JWT Bearer, Swagger, Npgsql.
- Bootstrap principal: `MyFinance.API/Program.cs`.
- Controladores: `MyFinance.API/Controllers`.
- Persistencia: `AppDbContext` em `MyFinance.API/Data/AppDbContext.cs`.

Responsabilidades:

- Autenticacao via JWT.
- Regras de negocio do dominio financeiro.
- Exposicao dos endpoints REST consumidos pelo front-end.
- Conexao com PostgreSQL do Supabase.
- Consumo de fonte externa do Tesouro Direto via `IHttpClientFactory`.

### 3. Banco de Dados no Supabase

- Banco: PostgreSQL.
- A API usa `ConnectionStrings:DefaultConnection`.
- O projeto trabalha com tabelas como `users`, `accounts`, `transactions`, `categories`, `budgets`, `recurring_transactions` e `fii_holdings`.
- O boot da API contem rotina de schema bootstrap para algumas estruturas auxiliares.

## Fluxo de comunicacao

1. O usuario acessa o front-end no Render.
2. O front-end chama a API no Render via `axios`.
3. No login, a API gera um JWT e devolve `token` e `name`.
4. O front-end salva o token em `localStorage`.
5. Nas chamadas autenticadas seguintes, o interceptor envia `Authorization: Bearer <token>`.
6. A API valida o JWT, resolve o `userId` a partir de `ClaimTypes.NameIdentifier` e filtra os dados por usuario.
7. A API persiste e consulta dados no Supabase PostgreSQL.

## Fluxo de autenticacao

### Cadastro

- Endpoint: `POST /api/auth/register`
- Entrada: `name`, `email`, `password`
- Efeitos:
  - cria o usuario
  - gera hash da senha com BCrypt
  - cria categorias padrao para o usuario

### Login

- Endpoint: `POST /api/auth/login`
- Entrada: `email`, `password`
- Saida:
  - `token`
  - `name`

### Consumo autenticado

- O arquivo `MyFinance.Web/src/services/api.js` aplica o token em todas as requests.
- Quase todos os controllers usam `[Authorize]`.

## Fluxo de dados por modulo

### Dashboard

O Dashboard do front-end e montado a partir de:

- `GET /api/recurring`
- `GET /api/accounts`
- `GET /api/transactions?month={m}&year={y}`
- `GET /api/recurring/projection?months=6&startMonth={m}&startYear={y}`

Essas chamadas alimentam:

- saldo atual consolidado
- receitas e despesas do mes
- despesas fixas previstas
- projecao dos proximos meses
- tabela de transacoes e graficos

### Transacoes

- `GET /api/transactions`
- `POST /api/transactions`
- `PUT /api/transactions/{id}`
- `DELETE /api/transactions/{id}`
- `POST /api/transactions/transfer`
- `GET /api/transactions/invoice`

### Contas

- `GET /api/accounts`
- `POST /api/accounts`
- `PUT /api/accounts/{id}`
- `DELETE /api/accounts/{id}`
- `POST /api/accounts/adjust-balance`

### Categorias

- `GET /api/categories`
- `POST /api/categories`
- `DELETE /api/categories/{id}`

### Recorrencias

- `GET /api/recurring`
- `POST /api/recurring`
- `DELETE /api/recurring/{id}`
- `POST /api/recurring/generate`
- `GET /api/recurring/projection`

### Orcamentos

- `GET /api/budgets`
- `POST /api/budgets`
- `DELETE /api/budgets/{id}`

### Perfil

- `GET /api/users/me`
- `POST /api/users/wipe-data`

### Investimentos

- `GET /api/tesouro/latest`
- `GET /api/fiiholdings`
- `POST /api/fiiholdings`
- `DELETE /api/fiiholdings/{id}`

### Importacao

- `POST /api/import/upload?accountId={id}`
- Upload de CSV com processamento no back-end.

## Variaveis de ambiente necessarias

### Front-end

- `VITE_API_URL`
  - exemplo esperado: `https://<api-render>/api`

### Back-end

- `PORT`
  - usada pelo Render para bind da aplicacao.
- `ConnectionStrings__DefaultConnection`
  - string de conexao do PostgreSQL no Supabase.
- `AppSettings__Token`
  - segredo usado para assinatura do JWT.
- `RunSchemaBootstrap`
  - opcional, controla bootstrap de schema.

## CORS

Configurado globalmente em `MyFinance.API/Program.cs` com politica aplicada por `app.UseCors("AllowAll")`.

Origem atualmente permitida:

- `https://financeiro-02r7.onrender.com`

## Dependencias externas relevantes

- Supabase PostgreSQL para persistencia.
- Dataset do Tesouro Transparente para taxas de investimento.
- Render para hospedagem do front-end.
- Render para hospedagem da API.

## Riscos operacionais atuais observados

- Ha segredos sensiveis em `appsettings.json` do repositiorio. Isso deve ser migrado integralmente para variaveis de ambiente.
- O front-end depende de `localStorage` para sessao, sem refresh token.
- O endpoint de investimentos depende de um CSV externo e pode falhar por indisponibilidade da origem.
- Nao existe, no estado atual do repositiorio, uma suite de testes automatizados integrada ao pipeline.
