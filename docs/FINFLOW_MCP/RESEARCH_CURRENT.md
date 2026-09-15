# Pesquisa atual — autorização MCP, ChatGPT e OpenIddict 7.7.0

> Data de verificação: 2026-09-14  
> Escopo: fluxo OAuth de um servidor MCP remoto do FinFlow consumido pelo ChatGPT.  
> Método: somente fontes primárias oficiais da OpenAI, do projeto MCP e do OpenIddict/NuGet.

## Decisão executiva

Para a integração controlada do FinFlow com o ChatGPT, a decisão recomendada é **pré-cadastrar o cliente ChatGPT no OpenIddict** e fornecer as credenciais estáticas na configuração do app, se essa opção estiver disponível na superfície do ChatGPT usada no rollout. A especificação MCP 2026-07-28 dá prioridade a credenciais pré-registradas já disponíveis antes de CIMD e DCR; a documentação do ChatGPT confirma que credenciais estáticas fornecidas são usadas. Essa escolha evita manter um endpoint DCR e evita que o authorization server faça fetch de URLs arbitrárias de CIMD, reduzindo complexidade e superfície de SSRF. ([MCP — Client Registration](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration), [OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode), [MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))

**CIMD é a alternativa preferida para distribuição ampla/multi-tenant.** O MCP recomenda suporte a CIMD e a OpenAI o chama de mecanismo preferido quando o authorization server o suporta e o criador o escolhe. Porém, o OpenIddict 7.7.0 não oferece evidência oficial de suporte nativo a CIMD; adotá-lo no FinFlow exigiria uma camada própria para resolver e validar o documento do cliente. Para um único cliente conhecido, isso não compensa inicialmente. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization), [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [OpenIddict — repositório oficial](https://github.com/openiddict/openiddict-core))

**DCR não deve ser a primeira escolha.** A revisão MCP atual o mantém apenas por compatibilidade e o classifica como legado/depreciado; a OpenAI alerta que conexões distintas podem gerar muitos registros. Além disso, o suporte a RFC 7591/7592 ainda aparece como trabalho aberto no repositório oficial do OpenIddict, inclusive destinado a uma linha posterior à 7.7.0. ([MCP — Client Registration](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration), [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [OpenIddict issue #2404 — Dynamic Client Registration](https://github.com/openiddict/openiddict-core/issues/2404))

## Fluxo atual do ChatGPT

1. O criador cadastra a URL do servidor MCP remoto no modo desenvolvedor e escolhe OAuth (ou autenticação mista, se aplicável). O cliente documenta suporte a servidores remotos por SSE e streaming HTTP. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode))
2. Ao receber `401`, o ChatGPT descobre o Protected Resource Metadata do MCP e, a partir dele, o issuer do authorization server; em seguida consulta OAuth Authorization Server Metadata ou OIDC Discovery. O servidor MCP precisa publicar RFC 9728, e o authorization server precisa publicar seus endpoints e capacidades. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization Server Discovery](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/authorization-server-discovery))
3. Para obter `client_id`, o cliente usa as credenciais estáticas fornecidas; sem elas, pode usar CIMD quando o AS anuncia `client_id_metadata_document_supported: true` e o criador escolhe esse mecanismo; caso contrário, usa DCR quando configurado/disponível. Na ordem normativa geral do MCP, um cliente que suporta todos os mecanismos prefere pré-registro, depois CIMD, depois DCR e, por fim, solicita dados ao usuário. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode), [MCP — Client Registration](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration))
4. O ChatGPT executa Authorization Code com PKCE `S256`. O AS precisa anunciar `code_challenge_methods_supported` contendo `S256`; a OpenAI declara o fluxo incompatível se esse metadado estiver ausente ou não contiver `S256`. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))
5. O ChatGPT acrescenta `resource=<URI canônica do MCP>` tanto à requisição de autorização quanto à de token. O AS deve vincular o access token a esse recurso, normalmente por `aud`; o MCP deve rejeitar token sem a audiência esperada ou sem escopo suficiente. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
6. O ChatGPT envia o access token em `Authorization: Bearer` em cada requisição HTTP. O token nunca pode ir na query string. Token inválido/expirado resulta em `401`; escopo insuficiente, em `403`. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))

### Redirect URI do ChatGPT

A URI exata exibida na tela de gerenciamento do MCP deve ser incluída na allowlist do authorization server. Quando o AS atende aos requisitos de identificação de issuer, a OpenAI usa a URI estável `https://chatgpt.com/connector_platform_oauth_redirect`; em fluxos que não atendem a esses requisitos, pode usar `https://chatgpt.com/connector/oauth/{callback_id}`. A allowlist de produção deve ser copiada da UI, sem inferência. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))

## Requisitos MCP de autorização aplicáveis

- Autorização é opcional no protocolo em geral, mas implementações HTTP que a oferecem devem seguir o perfil MCP; o authorization server deve implementar OAuth 2.1. Para dados financeiros pessoais, OAuth não é opcional na arquitetura do FinFlow. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
- O servidor MCP atua como **OAuth resource server**, o ChatGPT como **OAuth client** e o OpenIddict como **authorization server**. O MCP não deve aceitar tokens destinados a outro recurso. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
- O MCP deve publicar Protected Resource Metadata RFC 9728 por URL HTTPS, com ao menos `resource` e `authorization_servers`; deve também apontá-lo no desafio `WWW-Authenticate` quando responde `401`. ([MCP — Authorization Server Discovery](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/authorization-server-discovery), [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))
- O AS precisa expor metadata RFC 8414 ou OIDC Discovery com issuer e endpoints exatos. O cliente deve validar o `iss` da resposta de autorização contra o issuer descoberto, evitando mix-up attacks. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization), [MCP — Authorization Server Discovery](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/authorization-server-discovery))
- O cliente deve pedir o menor conjunto de escopos: primeiro o `scope` indicado no desafio `WWW-Authenticate`; se ausente, os `scopes_supported` do metadata do recurso. O FinFlow deve começar apenas com escopos de leitura. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
- Refresh tokens devem ser tratados como informação confidencial; para clientes públicos, rotação é obrigatória pelo perfil de segurança. O MCP recomenda access tokens de curta duração. ([MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))

## CIMD, pré-registro e DCR

| Mecanismo | Estado atual | Ajuste ao FinFlow |
|---|---|---|
| Pré-registro | Primeiro na prioridade MCP quando credenciais já existem; usa relação administrativa prévia. | **Escolha inicial.** Registrar um cliente ChatGPT conhecido no OpenIddict e copiar exatamente a redirect URI da UI. |
| CIMD | Recomendado pelo MCP/OpenAI para clientes sem relação prévia; `client_id` é uma URL HTTPS de metadata. ChatGPT suporta `none` e `private_key_jwt` nesse fluxo. | **Plano de evolução** para distribuição pública ou muitas organizações; requer extensão/customização do AS e defesa SSRF. |
| DCR | Compatibilidade legada/depreciada no MCP; ChatGPT registra uma vez por conexão e pode proliferar registros. | **Não adotar** na v1; OpenIddict 7.7.0 não o entrega nativamente segundo o issue oficial aberto. |

Fontes da tabela: [MCP — Client Registration](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration), [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [OpenIddict issue #2404](https://github.com/openiddict/openiddict-core/issues/2404).

### Se CIMD for adotado depois

- O AS deve anunciar `client_id_metadata_document_supported: true` e aceitar como `client_id` uma URL HTTPS com caminho não vazio; redirect URIs devem corresponder exatamente ao documento. ([MCP — Client Registration](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/client-registration))
- No ChatGPT, `none` representa cliente público protegido por PKCE; `private_key_jwt` usa assertions assinadas pelo ChatGPT e JWKS publicado no próprio documento. A OpenAI publica ambos em `token_endpoint_auth_methods_supported`, mantendo ainda um campo singular legado. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))
- O fetch do documento precisa bloquear IPs privados/loopback/link-local, redirects inseguros e DNS rebinding, além de impor limites de tempo/tamanho. Esses controles decorrem do risco SSRF normativo de CIMD. ([MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))

## OpenIddict 7.7.0: capacidade e lacunas

O pacote oficial `OpenIddict.Server.AspNetCore` 7.7.0 existe, foi publicado em 2026-09-06 e suporta `net8.0`, `net9.0` e `net10.0` (além de TFMs .NET Framework declarados no pacote). ([NuGet — OpenIddict.Server.AspNetCore 7.7.0](https://www.nuget.org/packages/OpenIddict.Server.AspNetCore/7.7.0))

### O que ele cobre

- O OpenIddict suporta Authorization Code e os demais fluxos OAuth/OIDC centrais; para o FinFlow deve ser habilitado somente o fluxo necessário. ([OpenIddict — Choosing the right flow](https://documentation.openiddict.com/guides/choosing-the-right-flow.html))
- O servidor dispõe da opção `RequireProofKeyForCodeExchange`, que rejeita authorization requests sem `code_challenge`; isso permite tornar PKCE obrigatório. ([OpenIddict — `OpenIddictServerOptions`](https://github.com/openiddict/openiddict-core/blob/dev/src/OpenIddict.Server/OpenIddictServerOptions.cs))
- Desde a linha 7.0, OpenIddict valida `resource` em authorization/PAR e permite registrar recursos e permissões por aplicação. O recurso canônico do MCP deve ser registrado e permitido ao cliente ChatGPT; não se recomenda desabilitar essa validação. ([OpenIddict — migração 6.0 para 7.0](https://documentation.openiddict.com/guides/migration/60-to-70))
- O stack de validação pode validar tokens localmente ou por introspecção; validação de entradas de autorização permite revogação imediata, com o custo documentado de I/O adicional. ([OpenIddict — Authorization storage](https://documentation.openiddict.com/configuration/authorization-storage))

### O que precisa ser implementado ao redor dele

- **Protected Resource Metadata RFC 9728:** há um issue oficial ainda aberto para suporte na stack de validação; portanto o FinFlow deve publicar seu endpoint RFC 9728 e o desafio `WWW-Authenticate` na camada MCP/API, sem pressupor geração automática pelo OpenIddict 7.7.0. ([OpenIddict issue #2401 — Protected Resource Metadata](https://github.com/openiddict/openiddict-core/issues/2401), [MCP — Authorization Server Discovery](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/authorization-server-discovery))
- **CIMD:** não há documentação oficial do OpenIddict 7.7.0 anunciando suporte a `client_id_metadata_document_supported`. Se escolhido, será necessária extensão do pipeline para buscar/validar metadata e autenticar `none` ou `private_key_jwt`; isso deve ser tratado como trabalho customizado até prova de interoperabilidade. ([OpenIddict — documentação oficial](https://documentation.openiddict.com/), [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))
- **DCR:** o issue oficial de RFC 7591/7592 continua aberto; não planejar `/register` como funcionalidade nativa da versão 7.7.0. ([OpenIddict issue #2404 — Dynamic Client Registration](https://github.com/openiddict/openiddict-core/issues/2404))
- **Metadata específica do ChatGPT:** confirmar em teste que o discovery publicado anuncia `S256`, o método de autenticação escolhido e o issuer exato. A existência das opções no OpenIddict não substitui esse teste de wire compatibility. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))

## Riscos e controles obrigatórios

- **Confused deputy/token forwarding:** o MCP deve validar `aud/resource`, issuer, expiração e escopos; nunca encaminhar o bearer recebido para APIs downstream. Para downstream, obtenha um token separado destinado àquela API. ([MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))
- **CSRF e interception:** exigir PKCE S256, validar `state`, validar issuer e fazer correspondência exata de redirect URI. ([MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))
- **SSRF em CIMD:** não habilitar CIMD sem política de egress/URL robusta; esse risco é uma razão adicional para o pré-registro na v1. ([MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))
- **Excesso de privilégio:** separar escopos por capacidade e iniciar somente leitura; nunca autorizar ações financeiras por simples posse de um token genérico. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
- **Segredos e chaves:** usar certificados/chaves de produção protegidos e persistentes; as credenciais de desenvolvimento do OpenIddict não são uma estratégia de produção. ([OpenIddict — Encryption and signing credentials](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html))

## Critérios de aceitação de interoperabilidade

1. `/.well-known/oauth-protected-resource/...` responde por HTTPS com `resource` exatamente igual à URL canônica do MCP e aponta para o issuer correto.
2. O discovery do OpenIddict contém issuer/endpoints corretos e `code_challenge_methods_supported` inclui `S256`.
3. O cliente pré-registrado contém exatamente a redirect URI exibida pelo ChatGPT e somente os grants, endpoints, scopes e resource necessários.
4. Authorization e token requests observadas em ambiente de teste contêm o mesmo `resource` canônico.
5. O access token emitido contém audiência do MCP; token para outra audiência recebe `401`; falta de escopo recebe `403`.
6. O fluxo completo conecta, autentica, lista e chama uma Tool somente leitura no ChatGPT sem DCR.
7. Logout/revogação e expiração impedem novo uso do token no prazo esperado.

Esses critérios derivam de [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization), [MCP — Authorization Server Discovery](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/authorization-server-discovery) e [MCP — Security considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations).

## Confidence

- **Alta:** requisitos normativos do MCP 2026-07-28; comportamento documentado do ChatGPT para OAuth, PKCE S256, `resource`, CIMD/DCR e redirect URI; existência e TFMs do pacote OpenIddict 7.7.0.
- **Média:** escolha de pré-registro como melhor opção para o FinFlow, pois depende da disponibilidade concreta do campo de credenciais estáticas na conta/superfície do ChatGPT usada no rollout.
- **Baixa:** suporte nativo futuro do OpenIddict a CIMD/RFC 9728/DCR e qualquer comportamento do ChatGPT não descrito nas páginas oficiais; não deve ser inferido.

## Validação Humana Necessária

- Confirmar na UI real do ChatGPT se o cadastro do app permite fornecer `client_id`/`client_secret` estáticos e copiar a redirect URI de produção exibida.
- Confirmar se o app será privado/controlado ou publicado para múltiplas organizações; publicação ampla muda a preferência prática de pré-registro para CIMD.
- Aprovar os escopos financeiros mínimos e a política de consentimento/revogação.
- Executar o teste de wire compatibility acima contra o endpoint implantado, inspecionando discovery, parâmetros `resource`, audiência e códigos `401/403`.
- Revalidar as páginas oficiais imediatamente antes do rollout, pois o fluxo de produto do ChatGPT e o draft CIMD podem mudar.
