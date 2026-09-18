# FinFlow Web

Frontend React/Vite do FinFlow.

## PWA

O frontend é instalável como PWA em navegadores compatíveis e abre em modo
`standalone`. O `vite-plugin-pwa` gera o manifest e o service worker com
Workbox durante o build.

### Política de cache

- JavaScript, CSS, HTML do shell, fontes, logos e ícones versionados podem ser
  precacheados.
- O `index.html` não recebe cache eterno: a atualização é descoberta pelo
  service worker e apresentada como “Nova versão do FinFlow disponível.”.
- Toda a API financeira é `NetworkOnly`, incluindo `/api/*` e a API Render
  `https://my-finance-api-a51s.onrender.com`.
- `/mcp`, OAuth, tokens e respostas financeiras nunca são armazenados no
  Cache Storage.
- A autenticação continua usando o armazenamento existente do navegador; o
  service worker não acessa nem move tokens.

### Offline e instalação

Sem conexão, o shell pode permanecer disponível, mas o FinFlow informa que é
necessário conectar-se à internet para carregar os dados financeiros. Dados
antigos não são exibidos como se fossem atuais.

No Chrome/Edge, use o aviso discreto “Instalar” quando ele aparecer ou a opção
de instalação do navegador. No Android, o mesmo fluxo pode ser acessado pelo
menu do navegador. No iPhone/iPad, use Safari → Compartilhar → Adicionar à
Tela de Início.

Uma atualização disponível pede confirmação pelo botão “Atualizar”; ela não
força recarregamento enquanto o usuário estiver preenchendo um formulário.

### Diagnóstico de latência

O frontend registra marcas na Performance API para separar o clique, a
requisição de login, a autenticação do app, as chamadas iniciais e a tela de
dashboard utilizável. As chamadas individuais também recebem medidas com
`finflow:api:*`. Consulte esses eventos e a aba Network do DevTools para
comparar o primeiro request com os seguintes, sem alterar timeout ou regras de
autenticação.

## Desenvolvimento

```bash
npm install
npm run dev
```

Para validar o build de produção:

```bash
npm run build
npm run preview
```

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) (or [oxc](https://oxc.rs) when used in [rolldown-vite](https://vite.dev/guide/rolldown)) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and [`typescript-eslint`](https://typescript-eslint.io) in your project.
