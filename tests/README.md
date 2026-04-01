# Base de Testes

Esta pasta contem apenas novos artefatos de teste e validacao. Nenhum arquivo de producao foi alterado para suportar esta base.

## Estrutura

- `backend/Finflow.Api.ContractTests`
  - testes xUnit que exercitam a API via HTTP real
- `frontend`
  - testes E2E com Playwright para login e dashboard
- `run-backend-tests.ps1`
- `run-frontend-tests.ps1`

## Premissas

Os testes do back-end foram desenhados como contract tests e precisam de uma API executando em ambiente local ou remoto.

Variaveis esperadas:

- `FINFLOW_TEST_BASE_URL`
- `FINFLOW_TEST_EMAIL`
- `FINFLOW_TEST_PASSWORD`
- `FINFLOW_TEST_NAME`

Exemplo:

```powershell
$env:FINFLOW_TEST_BASE_URL = "https://seu-backend.onrender.com"
$env:FINFLOW_TEST_EMAIL = "qa.finflow@example.com"
$env:FINFLOW_TEST_PASSWORD = "SenhaForte123!"
$env:FINFLOW_TEST_NAME = "QA Finflow"
```

Importante:

- Use um usuario dedicado para testes.
- Alguns testes criam e apagam contas, categorias, transacoes, recorrencias e holdings.
- O endpoint `users/wipe-data` existe, mas esta suite nao o executa automaticamente antes de cada rodada para nao destruir dados por engano.

## Execucao do back-end

```powershell
.\tests\run-backend-tests.ps1
```

Ou diretamente:

```powershell
dotnet test .\tests\backend\Finflow.Api.ContractTests\Finflow.Api.ContractTests.csproj
```

## Execucao do front-end

Entre na pasta `tests/frontend`, instale as dependencias e rode:

```powershell
npm install
npx playwright install
npm test
```

Variaveis esperadas:

- `FINFLOW_E2E_BASE_URL`
- `FINFLOW_E2E_EMAIL`
- `FINFLOW_E2E_PASSWORD`

## Cobertura alvo

Back-end:

- Auth
- Accounts
- Categories
- Budgets
- Transactions
- Recurring
- Users
- Import
- FII Holdings
- Tesouro

Front-end:

- fluxo de login
- carregamento do dashboard
- ausencia de erro de timeout logo apos autenticacao

