# Frontend Agent Guide

## Missao

Este agente trabalha exclusivamente no front-end React hospedado na Vercel. Ele deve preservar a experiencia atual do usuario e respeitar os contratos da API do back-end.

## Stack

- React 18
- Vite
- JavaScript
- Ant Design
- styled-components
- axios
- dayjs
- Chart.js

## Arquivos centrais

- `MyFinance.Web/src/App.jsx`
- `MyFinance.Web/src/services/api.js`
- `MyFinance.Web/src/pages`
- `MyFinance.Web/src/components`

## Regras de negocio do front-end

- O token JWT fica em `localStorage` na chave `token`.
- O nome do usuario fica em `localStorage` na chave `userName`.
- Toda chamada autenticada sai pelo cliente `api` em `src/services/api.js`.
- O Dashboard depende de quatro fontes: recorrencias, contas, transacoes do mes e projecao.
- A troca de periodo e baseada em `month` e `year` derivados do `DatePicker`.
- O logout e apenas local: remove os dados do `localStorage`.

## Contratos implicitos importantes

- A API responde objetos JSON simples, sem envelope padrao global.
- O front espera `token` e `name` no login.
- O front espera `currentBalance`, `invoiceAmount`, `isCreditCard`, `closingDay` e `dueDay` em contas.
- Transacoes retornam `category` populada para exibicao em listas e relatorios.

## Padroes de codigo a seguir

- Reutilizar `api.js` para qualquer integracao HTTP.
- Usar `try/catch/finally` em requests assicronas de UI.
- Em `useEffect`, revisar dependencias para evitar loops ou chamadas duplicadas.
- Em componentes com request longo, prever estado de loading.
- Manter componentes de pagina em `src/pages` e componentes reutilizaveis em `src/components`.
- Nao acessar a API diretamente com `fetch` se `axios` ja cobre o caso.

## Cuidados ao modificar o front-end

- Nao quebrar o formato das props entre `App.jsx`, paginas e modais.
- Nao assumir que a API sempre responde instantaneamente.
- Nao remover tratamento de erro visual com `message.error`.
- Nao introduzir dependencias de roteamento sem necessidade: a app atual usa selecao de tela por estado local, nao React Router.

## Testes recomendados para toda alteracao

- Login com credenciais validas.
- Login com credenciais invalidas.
- Carregamento do Dashboard apos login.
- Navegacao entre periodos.
- Criacao e exclusao de transacao.
- Fluxos de responsividade basica em mobile e desktop.

