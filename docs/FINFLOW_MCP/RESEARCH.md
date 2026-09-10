# Pesquisa primária — MCP remoto do FinFlow para ChatGPT

> Data da pesquisa: 2026-09-10  
> Escopo: requisitos atuais de interoperabilidade com ChatGPT e requisitos/bons padrões normativos do MCP.  
> Fontes: somente documentação oficial da OpenAI e do projeto Model Context Protocol (MCP).

## Resumo executivo

1. O ChatGPT conecta-se a **servidores MCP remotos** e, no modo de desenvolvedor, declara suporte a **SSE e streaming HTTP**, com autenticação `OAuth`, sem autenticação ou mista. Para publicação/revisão, a OpenAI exige um domínio publicamente acessível e não aceita endpoint local/de teste. Portanto, a integração real do FinFlow precisa de um endpoint remoto alcançável pelo ChatGPT; ausência de autenticação não é aceitável para dados financeiros pessoais, embora seja tecnicamente suportada pelo cliente. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode), [OpenAI — Remote MCP server review requirements](https://developers.openai.com/plugins/deploy/app-review))
2. A revisão corrente do protocolo é **MCP 2026-07-28**. Seu Streamable HTTP moderno é stateless: um endpoint único aceita `POST`, cada mensagem JSON-RPC usa seu próprio `POST`, e a resposta é JSON ou SSE restrito àquela requisição; o `GET` de stream e as sessões de protocolo foram removidos nessa revisão. ([MCP — Streamable HTTP 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http), [MCP — anúncio da revisão 2026-07-28](https://blog.modelcontextprotocol.io/posts/2026-07-28/))
3. Há uma diferença que precisa virar teste de compatibilidade: a documentação do ChatGPT fala genericamente em “SSE e streaming HTTP”, enquanto a especificação atual alterou o wire protocol em 2026-07-28. Não há, nas fontes oficiais consultadas, confirmação suficiente de que todo fluxo do ChatGPT já negocia essa revisão moderna. O servidor deve usar SDK oficial com compatibilidade de versões e a aceitação deve incluir `Scan Tools`/conexão real no ChatGPT; não se deve inferir compatibilidade apenas pelo nome do transporte. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode), [MCP — Streamable HTTP 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http))
4. Para dados privados do FinFlow, o modelo recomendado é **OAuth 2.1 authorization-code + PKCE**, com o MCP como resource server, validação de token em toda requisição, escopo somente leitura e isolamento do usuário derivado do token. A OpenAI recomenda um provedor de identidade estabelecido em vez de construir autenticação do zero. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
5. A primeira versão deve expor um conjunto pequeno de **Tools somente leitura, agregadoras e paginadas**, com `inputSchema`, `outputSchema`, `structuredContent` e `readOnlyHint: true`. Resources e Prompts são capacidades úteis do protocolo, mas as fontes do ChatGPT consultadas documentam o fluxo de criação/varredura principalmente em termos de Tools; eles não devem ser dependência funcional da v1 sem teste explícito no cliente alvo. ([OpenAI — Define tools](https://developers.openai.com/plugins/plan/tools), [MCP — Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [MCP — Resources](https://modelcontextprotocol.io/specification/2026-07-28/server/resources), [MCP — Prompts](https://modelcontextprotocol.io/specification/2026-07-28/server/prompts))

## 1. Requisitos para expor o servidor ao ChatGPT

### 1.1 Endpoint e transporte

- O ChatGPT permite criar uma app de modo desenvolvedor a partir da URL de um servidor MCP remoto; a documentação oficial lista `SSE` e `streaming HTTP` como protocolos suportados. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode))
- Para revisão/publicação, o servidor precisa estar hospedado em domínio publicamente acessível e o endpoint de exemplo precisa ser real e alcançável pela OpenAI; endpoints locais ou de teste não atendem aos requisitos de revisão. ([OpenAI — Remote MCP server review requirements](https://developers.openai.com/plugins/deploy/app-review))
- No MCP 2026-07-28, Streamable HTTP exige um único endpoint (por exemplo, `https://finflow.example/mcp`) que aceite `POST`. Cada requisição/notificação JSON-RPC viaja em um novo `POST`; respostas de requisição usam `application/json` ou `text/event-stream`. ([MCP — Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http))
- O cliente deve enviar `Accept: application/json, text/event-stream`. Cada `POST` moderno inclui `MCP-Protocol-Version`; `Mcp-Method` é obrigatório para toda requisição e `Mcp-Name` para `tools/call`, `resources/read` e `prompts/get`. O servidor deve rejeitar divergência entre headers e corpo. ([MCP — Streamable HTTP, Request Metadata](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http))
- O servidor deve validar `Origin` em conexões recebidas e responder `403` a origem presente e inválida; localmente, deve bindar em loopback, não em todas as interfaces. ([MCP — Streamable HTTP, Security & Endpoint](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http))
- CORS amplo não é um requisito MCP nem do ChatGPT documentado nas fontes consultadas. Como a chamada é remota/servidor-a-servidor, a decisão segura é não habilitar origens genéricas; validar `Origin` conforme o protocolo e liberar somente origens efetivamente necessárias por uma UI separada.

### 1.2 Cadastro e operação no ChatGPT

- No modo de desenvolvedor, o criador fornece o endpoint, escolhe a autenticação e executa a varredura das Tools. Alterações futuras no conjunto de Tools não são automaticamente adotadas: o app precisa ser atualizado/varrido novamente. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode), [OpenAI — Remote MCP server review requirements](https://developers.openai.com/plugins/deploy/app-review))
- `search` e `fetch` não são requisitos universais do modo desenvolvedor; qualquer Tool exposta pode ser usada. Logo, o FinFlow pode publicar Tools financeiras específicas, sem fingir ser um servidor de busca documental. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode))
- A documentação atual lista disponibilidade do modo desenvolvedor para contas Pro, Plus, Business, Enterprise e Education na web. A disponibilidade é requisito de conta/cliente, não do servidor; deve ser revalidada antes do rollout porque é condição de produto mutável. ([OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode))

## 2. Primitivas MCP e decisão para o FinFlow

### 2.1 Tools

Tools são controladas pelo modelo: o cliente as descobre e o LLM pode escolhê-las/invocá-las. O servidor que suporta Tools deve declarar a capability `tools` e responder a `tools/list`; cada Tool tem nome, descrição, `inputSchema`, `outputSchema` opcional e annotations opcionais. ([MCP — Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools))

Requisitos/recomendações aplicáveis ao FinFlow:

- usar nomes estáveis e orientados à ação, descrições que digam “quando usar”, limites e distinção de Tools semelhantes; não espelhar mecanicamente cada endpoint REST interno; ([OpenAI — Define tools](https://developers.openai.com/plugins/plan/tools))
- definir entradas explícitas com tipos, enums, limites, paginação e períodos máximos; não fazer o modelo adivinhar usuário/tenant ou identificadores necessários à correção; ([OpenAI — Define tools](https://developers.openai.com/plugins/plan/tools))
- manter identidade do usuário fora dos argumentos: ela deve vir do token verificado; isso evita que o modelo selecione outro `userId`;
- fornecer `outputSchema` e `structuredContent`; se `outputSchema` existir, o resultado estruturado do servidor deve obedecê-lo. Para retrocompatibilidade, a especificação recomenda também serializar o JSON em um bloco de texto. ([MCP — Tools, Structured Content](https://modelcontextprotocol.io/specification/2026-07-28/server/tools))
- marcar todas as Tools da v1 com `readOnlyHint: true` somente se forem realmente incapazes de mudar estado. A annotation é dica, não substitui autorização ou validação no servidor. ([OpenAI — Define tools](https://developers.openai.com/plugins/plan/tools))
- declarar `destructiveHint: false`; para consultas ao próprio FinFlow privado e delimitado, `openWorldHint: false` é coerente, pois a OpenAI esclarece que uma conta/workspace privado delimitado não vira open-world só por ser hospedado externamente. ([OpenAI — Define tools](https://developers.openai.com/plugins/plan/tools))
- não usar `x-mcp-header` em descrições de transação, tokens, PII ou outros campos sensíveis; esses headers ficam visíveis a intermediários de rede. ([MCP — Tools, x-mcp-header](https://modelcontextprotocol.io/specification/2026-07-28/server/tools))

A especificação exige que o servidor valide todas as entradas, aplique controle de acesso, limite a taxa de invocação e higienize saídas; também recomenda timeouts e auditoria no cliente. Isso sustenta critérios de aceitação para limites de período/página, timeout/cancelamento, autorização por usuário e logs sem payload financeiro. ([MCP — Tools, Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/server/tools))

### 2.2 Resources

Resources são controlados pela aplicação cliente e identificados por URI; servem para contexto como arquivos, schemas ou dados específicos da aplicação. Um servidor que os oferece declara `resources`, responde a `resources/list` e `resources/read`, pode fornecer templates de URI, paginação e hints de cache. ([MCP — Resources](https://modelcontextprotocol.io/specification/2026-07-28/server/resources))

Para a v1 do FinFlow, dados financeiros variáveis por filtros e período se encaixam melhor em Tools agregadoras: a seleção é orientada pelo modelo, os argumentos podem ser validados e o resultado pode ser minimizado. Resources podem ser considerados mais tarde para artefatos estáveis e explicitamente selecionáveis (por exemplo, um catálogo de categorias ou explicação do significado das métricas), mas não para expor um dump de transações nem como contorno à paginação. Essa é uma inferência arquitetural baseada no modelo de controle definido pelo protocolo; precisa ser validada no cliente ChatGPT alvo. ([MCP — visão geral das primitivas](https://modelcontextprotocol.io/specification/2026-07-28/server/index), [MCP — Resources](https://modelcontextprotocol.io/specification/2026-07-28/server/resources))

Se Resources privados forem usados, a lista pode variar conforme a autorização da requisição e o conteúdo deve usar cache privado/TTL coerente; nunca se deve publicar um catálogo cross-user. A especificação admite conjuntos diferentes por credencial e hints `cacheScope: private`. ([MCP — Resources](https://modelcontextprotocol.io/specification/2026-07-28/server/resources))

### 2.3 Prompts

Prompts são templates controlados pelo usuário: o servidor declara `prompts`, responde a `prompts/list` e `prompts/get`, e pode receber argumentos. Implementações devem validar entradas e saídas de Prompt para evitar injection e acesso não autorizado. ([MCP — Prompts](https://modelcontextprotocol.io/specification/2026-07-28/server/prompts))

Prompts como “fechamento mensal” ou “comparar meses” podem melhorar descoberta do fluxo, mas não são necessários para a capacidade de consulta e duplicariam instruções que um projeto do ChatGPT já pode guardar. A v1 deve tratá-los como opcional pós-validação, nunca como mecanismo de autorização, política financeira ou garantia de comportamento do modelo. Essa é uma recomendação de produto derivada do papel user-controlled dos Prompts. ([MCP — Prompts](https://modelcontextprotocol.io/specification/2026-07-28/server/prompts))

## 3. Autenticação e autorização

### 3.1 Contrato OAuth necessário

Embora autorização seja opcional no protocolo genérico, dados específicos de cliente devem autenticar o usuário, e a OpenAI espera OAuth 2.1 conforme a especificação MCP para servidores autenticados. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))

Para o FinFlow:

1. O endpoint MCP atua como **OAuth resource server** e verifica o Bearer token em toda requisição. O authorization server emite tokens e publica metadata; ChatGPT é o OAuth client agindo em nome do usuário. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))
2. O MCP deve publicar OAuth Protected Resource Metadata (RFC 9728) em URL HTTPS well-known, ou indicá-la no `WWW-Authenticate` de um `401`. O documento define o identificador canônico do resource server, authorization server(s) e, opcionalmente, escopos suportados. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
3. O authorization server deve publicar metadata OAuth ou OpenID Connect, incluindo endpoints, issuer e suporte a PKCE `S256`. A especificação atual recomenda Client ID Metadata Documents (CIMD), mantém DCR apenas para compatibilidade e exige que clientes verifiquem suporte a PKCE antes de prosseguir. ([MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization), [MCP — Authorization Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))
4. ChatGPT suporta CIMD com autenticação de token endpoint `none` (public client) ou `private_key_jwt`, e DCR quando configurado. Não suporta grants machine-to-machine como client credentials/service accounts nem API keys personalizadas. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))
5. O parâmetro `resource` canônico deve ser propagado nas requisições de autorização e token. O MCP deve validar assinatura, `iss`, audience/resource, `exp`/`nbf`, escopos e política específica; token ausente/inválido/expirado retorna `401`, e escopo insuficiente retorna `403` com challenge adequado. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth), [MCP — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization))
6. O access token recebido nunca deve ser repassado à API REST interna ou a outro serviço. Token passthrough é proibido; qualquer credencial downstream precisa ser separada e emitida para o audience correto. ([MCP — Authorization Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations), [MCP — Security Best Practices](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/security_best_practices))

### 3.2 Modelo recomendado para a v1

- Escopo inicial único e estreito, por exemplo `finflow:financial.read`, sem qualquer escopo de escrita publicado. O guia MCP recomenda escopos progressivos, mínimos e evita curingas/omnibus. ([MCP — Security Best Practices, Scope Minimization](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/security_best_practices))
- `securitySchemes` `oauth2` explicitado em cada Tool com esse escopo; ainda assim, o servidor verifica token, scope e audience em toda invocação. ([OpenAI — Authentication, Triggering authentication UI](https://developers.openai.com/plugins/build/auth))
- `sub`/identidade validada é mapeada no servidor para o usuário FinFlow. Nenhuma Tool aceita `userId`, e toda query aplica o filtro do usuário autenticado, preservando a fronteira multi-tenant.
- Access tokens curtos, armazenamento seguro e refresh-token rotation para clientes públicos. A especificação recomenda tokens curtos e exige rotação de refresh token para public clients. ([MCP — Authorization Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations))
- Revogação e rotação devem ser operáveis. A OpenAI orienta planejar revogação, refresh e mudanças de escopo e tratar tokens ausentes/obsoletos como não autenticados. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))
- Para continuidade sem relink frequente, o authorization server precisa emitir refresh tokens de acordo com sua política. A necessidade exata de `offline_access` deve ser validada no provedor escolhido; não se deve embutir secrets no frontend ou no schema MCP.
- mTLS gerenciado pela OpenAI pode autenticar ChatGPT como cliente de transporte, mas não substitui OAuth para autenticar o usuário e autorizar acesso às Tools. ([OpenAI — Authentication, Mutual TLS](https://developers.openai.com/plugins/build/auth))

## 4. Riscos específicos e controles verificáveis

| Risco | Consequência no FinFlow | Controle mínimo verificável |
|---|---|---|
| Prompt injection em outra fonte/app | Um conteúdo malicioso pode induzir o modelo a buscar dados financeiros e enviá-los a outro destino. Confiar no servidor FinFlow não elimina esse risco. | V1 sem escrita; menor escopo e menor conjunto de Tools; agregação server-side; não retornar dados fora do necessário; evitar uso simultâneo com apps não confiáveis; testes de exfiltração cross-tool. ([OpenAI — MCP risks and safety](https://developers.openai.com/api/docs/mcp)) |
| Prompt/tool poisoning | Descrições ou metadata maliciosas influenciam a escolha de Tool; annotations são não confiáveis para o cliente. | Tool metadata revisada em código; descrições estritamente funcionais; diff/scan no ChatGPT; servidor sem instruções ocultas; annotations não usadas como controle de segurança. ([MCP — Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [OpenAI — Remote MCP server review requirements](https://developers.openai.com/plugins/deploy/app-review)) |
| Parâmetros manipulados | Períodos enormes, páginas excessivas, filtros fora do domínio ou IDs de outro usuário geram vazamento/DoS. | JSON Schema com limites e `additionalProperties: false`; allowlists/enums; intervalo máximo; page size máximo; ID sempre reautorizado; validação server-side. ([MCP — Tools, Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/server/tools), [OpenAI — Security & Privacy](https://developers.openai.com/plugins/guides/security-privacy)) |
| Exfiltração por resposta excessiva | Transações detalhadas ampliam dados expostos ao contexto e a outras Tools. | Agregar no banco/serviço; paginação cursor-based; campos mínimos; omitir descrições quando a consulta pede só totais; teto de payload e de período. A OpenAI manda incluir apenas os dados necessários ao prompt. ([OpenAI — Security & Privacy](https://developers.openai.com/plugins/guides/security-privacy)) |
| Chamadas excessivas/DoS | Carga no banco e aumento de latência/custo. | Rate limit por principal e Tool; timeout/cancellation; limites de período e página; query budget; métricas e alertas. O MCP exige rate limiting de invocações e validação das entradas. ([MCP — Tools, Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)) |
| Confused deputy/token passthrough | Token de outro audience pode ser reutilizado ou encaminhado à API interna. | Validar issuer/audience/resource/scope em cada request; jamais encaminhar o token MCP; usar credencial interna separada quando necessário. ([MCP — Authorization Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations)) |
| Roubo de token | Acesso financeiro aparenta ser legítimo. | HTTPS; tokens curtos; refresh rotation; secrets fora de logs; armazenamento seguro; revogação. ([MCP — Authorization Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations)) |
| DNS rebinding/Origin | Site malicioso tenta falar com endpoint MCP local/privado. | Validar Origin; `403` para origem inválida; localhost-only em desenvolvimento; autenticar todas as conexões. ([MCP — Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http)) |
| SSRF em descoberta OAuth | URLs de metadata induzem requisições a rede interna/metadata cloud. | No componente que fetches metadata/CIMD: HTTPS, bloqueio de IPs privados/reservados, validação de redirects e egress policy. ([MCP — Security Best Practices, SSRF](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/security_best_practices)) |
| Vazamento por logs | Descrições, valores e tokens persistem na observabilidade. | Logar Tool, status, duração, contagem/tamanho aproximado e correlation ID; não logar argumentos/resultado financeiro, prompts ou tokens; redigir PII. ([OpenAI — Security & Privacy](https://developers.openai.com/plugins/guides/security-privacy)) |
| Erro interno verboso | Stack trace/schema interno ajuda exploração e pode conter dados. | Erro MCP estável e sanitizado para o cliente; detalhe técnico somente em log protegido e redigido. A especificação distingue erro de protocolo de erro de execução recuperável. ([MCP — Tools, Error Handling](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)) |

## 5. Implicações para a especificação e o plano do FinFlow

### Requisitos que devem aparecer na Spec

- todas as capacidades da v1 são somente leitura, inclusive no nível de serviços/credenciais de banco, não apenas na annotation;
- toda Tool exige usuário autenticado e escopo de leitura, sem argumento `userId`;
- isolamento multi-tenant é testado em cada caminho de consulta;
- agregações são feitas no servidor e os detalhes são retornados somente quando indispensáveis;
- períodos, páginas, payloads, taxa, duração e concorrência têm limites explícitos;
- contratos de moeda, datas, timezone, precisão decimal e valores nulos são estáveis;
- nenhum token, segredo, stack trace, prompt bruto ou transação completa aparece em logs;
- toda resposta é validada contra `outputSchema` e usa `structuredContent`;
- todas as Tools têm annotations coerentes (`readOnlyHint: true`, `destructiveHint: false`, `openWorldHint: false`);
- conexão é testada via MCP Inspector e via `Scan Tools`/execução real no ChatGPT, incluindo negociação de versão/transporte. A OpenAI recomenda o Inspector para depuração OAuth. ([OpenAI — Authentication](https://developers.openai.com/plugins/build/auth))

### Decisões sugeridas para o Plan

1. **Tools como superfície principal.** Consultas agregadas e filtráveis são o encaixe mais confiável no ChatGPT atual; Resources e Prompts ficam fora do caminho crítico da v1 até teste de suporte/UX.
2. **OAuth por servidor para a v1.** Todos os dados são sensíveis e não há Tool pública útil; exigir auth desde o início reduz ambiguidades. Ainda declarar `securitySchemes` por Tool melhora a metadata que o ChatGPT usa.
3. **Compatibilidade de protocolo explícita.** Adotar SDK oficial que sirva a revisão atual e o legado necessário; transformar a matriz suportada pelo ChatGPT em teste de contrato, não em suposição.
4. **Observabilidade fora do protocolo MCP.** O logging MCP foi marcado como deprecated em 2026-07-28; a própria revisão recomenda novos projetos não adotarem Roots, Sampling e Logging. Usar logs estruturados internos/OpenTelemetry, com redação. ([MCP — revisão 2026-07-28](https://blog.modelcontextprotocol.io/posts/2026-07-28/), [MCP — SEP-2577](https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging))
5. **Sem dependência de sessão MCP.** A revisão moderna é stateless; autorização e tenant vêm de cada requisição, e qualquer estado futuro precisa de handle explícito reautorizado. ([MCP — Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http), [MCP — Tools, Stateful Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools))

## 6. Checklist de validação normativa

- [ ] endpoint remoto HTTPS real e acessível ao ChatGPT;
- [ ] Streamable HTTP conforme versão negociada; compatibilidade SSE/legada comprovada se necessária;
- [ ] JSON-RPC, content types, metadata/headers e erros de transporte conformes;
- [ ] `Origin` validado e endpoint local restrito a loopback;
- [ ] `tools` capability e `tools/list` determinístico;
- [ ] schemas de entrada/saída válidos; `structuredContent` validado;
- [ ] annotations somente leitura verdadeiras e conferidas no `Scan Tools`;
- [ ] Protected Resource Metadata e Authorization Server Metadata válidos;
- [ ] authorization-code + PKCE S256; CIMD preferencial ou DCR/pre-registration compatível;
- [ ] assinatura, issuer, audience/resource, validade e scope verificados em toda requisição;
- [ ] `401`/`403` e `WWW-Authenticate` corretos;
- [ ] token passthrough inexistente; tokens/secrets ausentes de URL, schemas, payloads e logs;
- [ ] limites de período, paginação, payload, timeout e rate limiting testados;
- [ ] testes de acesso cross-user, prompt injection, exfiltração e chamada de operações inexistentes/de escrita;
- [ ] métricas por Tool/status/duração/tamanho aproximado, sem conteúdo financeiro;
- [ ] Resources/Prompts, se adicionados, autorizados por requisição e não necessários ao caminho crítico.

## 7. Fontes primárias consultadas

- [OpenAI — ChatGPT Developer mode](https://developers.openai.com/api/docs/guides/developer-mode)
- [OpenAI — Building MCP servers for plugins and API integrations](https://developers.openai.com/api/docs/mcp)
- [OpenAI — Define tools](https://developers.openai.com/plugins/plan/tools)
- [OpenAI — Authentication](https://developers.openai.com/plugins/build/auth)
- [OpenAI — Security & Privacy](https://developers.openai.com/plugins/guides/security-privacy)
- [OpenAI — Remote MCP server review requirements](https://developers.openai.com/plugins/deploy/app-review)
- [MCP Specification 2026-07-28 — Overview](https://modelcontextprotocol.io/specification/2026-07-28/server/index)
- [MCP Specification 2026-07-28 — Streamable HTTP](https://modelcontextprotocol.io/specification/2026-07-28/basic/transports/streamable-http)
- [MCP Specification 2026-07-28 — Authorization](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization)
- [MCP Specification 2026-07-28 — Authorization Security Considerations](https://modelcontextprotocol.io/specification/2026-07-28/basic/authorization/security-considerations)
- [MCP Specification 2026-07-28 — Tools](https://modelcontextprotocol.io/specification/2026-07-28/server/tools)
- [MCP Specification 2026-07-28 — Resources](https://modelcontextprotocol.io/specification/2026-07-28/server/resources)
- [MCP Specification 2026-07-28 — Prompts](https://modelcontextprotocol.io/specification/2026-07-28/server/prompts)
- [MCP — Security Best Practices 2026-07-28](https://modelcontextprotocol.io/docs/2026-07-28/tutorials/security/security_best_practices)
- [MCP — SEP-2577](https://modelcontextprotocol.io/seps/2577-deprecate-roots-sampling-and-logging)

## Confidence

### Alta

- Requisitos normativos do MCP 2026-07-28 sobre Tools, Resources, Prompts, Streamable HTTP, OAuth, validação de tokens e segurança, confirmados diretamente na especificação e documentação oficiais do projeto MCP.
- Requisitos documentados pela OpenAI para servidor MCP remoto, modo desenvolvedor, autenticação, metadata de Tools e revisão/publicação, confirmados diretamente na documentação oficial OpenAI Developers.

### Média

- A recomendação de manter Resources e Prompts fora do caminho crítico da v1 é uma inferência arquitetural baseada no modelo de controle das primitivas e no foco atual da documentação do ChatGPT em Tools; depende de validação prática no cliente alvo.
- A necessidade de compatibilidade simultânea com revisão moderna e legado depende da versão efetivamente negociada pelo ChatGPT no ambiente da conta do usuário.

### Baixa

- Nenhuma afirmação factual foi baseada em fonte secundária. Não há confirmação documental suficiente, porém, de que todos os ambientes do ChatGPT já negociem MCP 2026-07-28.

## Validação Humana Necessária

- Confirmar o plano e a elegibilidade da conta/workspace ChatGPT que receberá a integração.
- Executar `Scan Tools` e chamadas reais no ChatGPT para registrar a revisão MCP e o transporte efetivamente negociados.
- Aprovar o provedor de identidade, o fluxo de vínculo entre o `sub` OAuth e o usuário FinFlow, a política de refresh/revogação e o escopo somente leitura.
- Aprovar os limites de período, paginação, payload e retenção de logs antes da implementação.


