# Plano de Implementação: FinFlow MCP somente leitura

**Feature ID**: `001-finflow-mcp`  
**Data**: 2026-09-10  
**Spec**: [SPEC.md](SPEC.md)  
**Status**: Implementado e validado; este documento preserva o plano original e identifica como propostas somente os itens que não foram adotados.

## Resumo

Hospedar um endpoint MCP remoto e stateless na aplicação ASP.NET Core existente. As tools serão adaptadores finos para um módulo profundo de consultas financeiras que concentra semântica de relatórios, filtros por titular, limites, agregações SQL e projeção de DTOs seguros. O módulo reutilizará `FinancialSnapshotService` apenas onde sua semântica já é adequada (saldos, passivos e faturas) e não reutilizará controllers REST como camada de aplicação.

## Contexto técnico

- **Linguagem/versão**: C# 12, .NET 8.
- **Aplicação**: ASP.NET Core Web API stateless.
- **MCP**: SDK oficial C# `ModelContextProtocol.AspNetCore`, versão estável compatível com .NET 8 a ser fixada na implementação.
- **Transporte**: Streamable HTTP stateless em `POST /mcp`, HTTPS em produção; a compatibilidade exata anunciada pelo ChatGPT deve ser validada com “Scan Tools”.
- **Persistência**: PostgreSQL/Supabase via EF Core 8 e `numeric`/`decimal` para dinheiro.
- **Identidade atual**: usuário próprio + JWT Bearer simétrico, validade de 30 dias, sem issuer/audience.
- **Testes**: xUnit; testes de lógica com EF InMemory; contratos MCP/integrados com `WebApplicationFactory` e PostgreSQL efêmero ou banco isolado; testes HTTP reais existentes como validação de deploy.
- **Plataforma alvo**: mesmo container/serviço Render da API.
- **Metas iniciais**: p95 local < 500 ms para agregados e < 750 ms para detalhes no conjunto de referência; resposta detalhada <= 100 itens e payload <= 256 KiB.
- **Escala**: sistema pessoal/multiusuário pequeno, sem assumir que o histórico cabe integralmente em memória.

## Constitution check

- [x] Há spec verificável antes do código.
- [x] Código, docs, arquitetura, banco, autenticação e testes atuais foram lidos.
- [x] Isolamento por `UserId` é obrigatório e não pode vir de parâmetro.
- [x] Sem código de produção nesta etapa.
- [x] Segurança → correção financeira → simplicidade → performance → manutenção.
- [x] Nova documentação está dentro de `docs/`, em pasta de módulo e com nomes maiúsculos.
- [x] Mudanças documentais serão registradas em `CHANGELOG_AI.md`.
- [ ] Modelo OAuth e estratégia de publicação aguardam aprovação humana.
- [ ] Rechecagem deste gate após o desenho detalhado da autenticação.

## Arquitetura atual confirmada

```text
React SPA
   │ REST JSON + JWT
   ▼
ASP.NET Core Controllers ── FinancialSnapshotService
   │                               │
   └──────────── EF Core ──────────┘
                   │
                   ▼
             PostgreSQL/Supabase
```

- Controllers concentram boa parte das regras; `FinancialSnapshotService` é a fonte existente para saldos, passivos, faturas e projeções.
- Todas as entidades financeiras carregam `UserId`; controllers extraem o titular do claim `NameIdentifier`.
- `ReportingKind`, `IsTransfer` e `ExcludeFromReports` separam consumo operacional de liquidações, repasses e ajustes.
- `BuildUserSnapshotAsync` carrega todo o histórico em memória. Isso é aceitável em algumas telas atuais, mas não é a base adequada para agregações MCP arbitrárias.
- O endpoint `GET /api/transactions` não pagina e pode devolver todo o histórico.
- Não existe servidor OAuth, refresh token, revogação individual nem índices compostos voltados a consultas por período.

## Encaixe proposto

```text
ChatGPT / MCP client
   │ Streamable HTTP + OAuth access token (finflow.read)
   ▼
/mcp ── autenticação, rate limit, correlação e schema
   │
   ▼
MCP Tools (adaptadores finos)
   │
   ▼
FinancialInsights module
   ├── semântica operacional única
   ├── agregações/paginação EF Core
   ├── FinancialSnapshotService (saldos/faturas)
   └── DTOs financeiros minimizados
            │
            ▼
      PostgreSQL/Supabase
```

O seam externo é o contrato MCP. O seam interno é a interface de consultas financeiras, usada por tools e testes. O módulo deve esconder exclusões contábeis, datas, paginação, precisão, autorização contextual e formato de erro; não será um simples repasse para controllers.

## Comparação arquitetural

| Critério | Integrado à API atual | Serviço MCP separado |
|---|---|---|
| Segurança | Reutiliza middleware, configuração e contexto de identidade; exige separar políticas REST/MCP | Isola superfície, mas duplica secrets, identidade e rede para o banco |
| Regras financeiras | Chama diretamente o módulo e `FinancialSnapshotService` | Exige pacote compartilhado ou chamadas REST, hoje inexistentes/sem paginação |
| Implantação | Um container, um domínio e uma observabilidade | Novo serviço, deploy, health check e configuração |
| Acoplamento | Acoplado ao backend por escolha explícita | Menor acoplamento de processo, maior acoplamento operacional/contratual |
| Performance | Sem salto HTTP interno | Salto HTTP ou acesso duplicado ao banco |
| Escalabilidade | Streamable HTTP stateless permite réplicas | Escala isolada, sem benefício relevante no volume atual |
| Manutenção | Menor superfície e uma solução .NET | Segundo projeto/processo e duplicação inicial |

**Decisão proposta**: integração no backend atual. Reavaliar serviço separado somente se o MCP precisar de escala, ciclo de deploy ou política de rede independente. Esta decisão é relevante, mas permanece “proposta” até aprovação; só então deve virar ADR permanente.

## Interface de consultas financeiras

Criar `IFinancialInsightsService` com operações orientadas ao domínio e DTOs próprios. A interface deve aceitar `userId` apenas de código confiável, datas civis já normalizadas e `CancellationToken`. Implementações aplicam filtro de usuário na primeira composição da query e projetam somente campos permitidos.

Responsabilidades:

- traduzir intervalo civil de `America/Sao_Paulo` para a semântica legada do banco sem deslocar datas;
- aplicar uma expressão EF reutilizável para “movimentação operacional”;
- agregar no banco e executar comparações;
- paginar por cursor assinado/opaco baseado em `(Date, Id)`;
- chamar `IFinancialSnapshotService` apenas para snapshot atual e regras de cartão já testadas;
- devolver valores `decimal`, `currency=BRL`, `dataAsOf` e avisos de completude.

## Tools MCP propostas

| Tool | Finalidade | Limites principais |
|---|---|---|
| `get_financial_summary` | Receita, despesa, fluxo líquido, taxa de poupança, série mensal e principais categorias | padrão mês atual; máximo 24 meses |
| `compare_periods` | Comparar renda, gastos, poupança e categorias entre dois períodos | cada período até 12 meses; sem registros brutos |
| `get_spending_by_category` | Agregar gastos por categoria e participação no total | máximo 24 meses; top N padrão 10/máximo 50 |
| `get_transactions` | Investigar detalhes sob filtros de período, conta, categoria, tipo, status e valor | período obrigatório <= 92 dias; 50 itens padrão/100 máximo; cursor |
| `get_account_balances` | Saldos reais/pendentes/projetados, passivos de cartão e patrimônio contábil líquido | snapshot atual; sem histórico de mercado |
| `get_recurring_expenses` | Compromissos ativos e custo mensal mínimo conhecido | somente regras ativas; sem notas livres |
| `get_financial_goals` | Metas, progresso e contribuição mensal planejada | sem notas livres; filtro de status |
| `get_investment_positions` | Saldos de contas de investimento e posições FII conhecidas | custo conhecido; valor de mercado `null` sem cotação persistida |

### Tools deliberadamente não criadas

- `get_month_summary`: duplicaria `get_financial_summary` com período mensal.
- `get_income`: o resumo e filtros já cobrem renda.
- `get_net_worth`: integra `get_account_balances`, evitando divergência.
- `get_spending_statistics`: médias/tendências vêm da série de `get_financial_summary`.
- Tools por categoria: criariam uma interface rasa e extensa.
- Qualquer tool de escrita/importação/transferência: proibida pela spec.

## Resources e Prompts

**Versão 1: não publicar Resources nem Prompts.** Dados privados, parametrizados e atualizados pertencem às Tools, que validam escopo, período e minimização. Um resource estático de “política contábil” repetiria descrições/instruções do servidor. Prompts de fechamento mensal não dão acesso adicional aos dados e podem viver nas instruções do projeto ChatGPT. Reavaliar após teste de uso real, sem bloquear a v1.

## Contrato de resposta

Sucesso:

```json
{
  "data": {},
  "meta": {
    "currency": "BRL",
    "timezone": "America/Sao_Paulo",
    "generatedAt": "2026-09-10T15:30:00Z",
    "dataAsOf": "2026-09-10",
    "partial": false
  }
}
```

Erro seguro:

```json
{
  "error": {
    "code": "PERIOD_TOO_LARGE",
    "message": "Reduza o período ou use uma consulta agregada.",
    "correlationId": "...",
    "retryable": false
  }
}
```

Tools retornam `structuredContent` validado por output schema. Texto, se exigido pelo cliente, contém apenas síntese curta derivada do mesmo objeto. Todas recebem `readOnlyHint: true`, `destructiveHint: false` e `idempotentHint: true`; essas anotações não substituem controles no servidor.

## Autenticação e autorização

### Modelo recomendado

- OAuth 2.1 Authorization Code + PKCE.
- API atua como MCP resource server e, preferencialmente, também hospeda o authorization server para reutilizar o usuário FinFlow sem sincronização externa.
- Biblioteca candidata: OpenIddict para endpoints, consentimento, emissão, rotação e revogação; a escolha e versão devem passar por spike de compatibilidade antes do commit de dependência.
- Protected Resource Metadata em `/.well-known/oauth-protected-resource` e metadata do authorization server.
- Escopo único inicial `finflow.read`; access token de 15 minutos; refresh token de até 30 dias com rotação e revogação.
- Access token com issuer, audience/resource canônico, subject mapeado ao usuário e scope; nunca aceitar token destinado à API genérica ou a outro recurso.
- Endpoint de revogação e tela/fluxo explícito para desconectar o ChatGPT.
- Segredos somente em variáveis de ambiente/secret store do Render; nunca no frontend, logs, exemplos ou repositório.

### Por que o JWT atual não basta

O token atual é uma credencial de sessão de 30 dias assinada por segredo simétrico global. Ele não oferece descoberta OAuth, consentimento, refresh/revogação individual, issuer/audience ou scope. Reutilizá-lo diretamente ampliaria o privilégio do ChatGPT e tornaria rotação/revogação perigosamente ampla.

### CORS e Origin

MCP é comunicação servidor-servidor; CORS não concede segurança e o endpoint não deve herdar `AllowAll`. Validar `Origin` quando presente contra allowlist do cliente/host suportado, validar `Host`/proxy headers e manter a política MCP separada da SPA. Requisições sem `Origin` ainda dependem de OAuth e HTTPS.

## Segurança e privacidade

- Rejeitar parâmetros desconhecidos e enums livres; normalizar IDs e datas sem interpolação SQL.
- Tratar descrição/categoria como dados não confiáveis; nunca inseri-las em instruções da tool.
- Aplicar autorização antes de resolver a tool e repetir o filtro `UserId` dentro do módulo.
- Sem token passthrough para APIs externas e sem SSRF: nenhuma tool aceita URL.
- Limitar período, página, top N, payload, concorrência e tempo de execução.
- Não cachear payload financeiro compartilhado. Se houver cache futuro, chave obrigatória por usuário+filtros e TTL curto.
- Omitir campos de importação e notas livres; fornecer agregados antes de detalhes.
- Definir retenção dos logs técnicos e acesso administrativo mínimo.
- Adicionar revisão de dependências e threat model antes do deploy.

## Correção financeira e modelo temporal

- `decimal` no código e `numeric` no PostgreSQL; sem `double`/`float` em regras.
- Intervalos do contrato são datas civis inclusivas. Internamente usar início inclusivo/fim exclusivo.
- `savingsRate = netCashFlow / income`; renda zero produz `null`.
- Estornos reduzem despesa; totais podem ser negativos e não devem ser truncados, exceto passivo de cartão exibido, cuja normalização deve ser explicitada.
- A expressão operacional deve representar `ReportingKind == normal`, `!IsTransfer` e `!ExcludeFromReports`.
- “Saldo” e “patrimônio” devem carregar qualificadores `knownLedger`/`accounting`, evitando prometer valor de mercado.
- Antes do código, criar fixtures para virada do mês e confirmar se timestamps históricos representam data civil local. Não migrar dados nesta feature sem decisão separada.

## Performance e banco

- Não usar `BuildUserSnapshotAsync` para resumo, comparação ou categorias.
- Consultas agregadas projetam e agrupam no PostgreSQL com `AsNoTracking`.
- Detalhes usam paginação keyset `(date DESC, id DESC)`, sem `Skip` profundo.
- Propor migration para índices: `(user_id, date DESC, id DESC)`, `(user_id, categoryid, date)` e, se o plano de consulta justificar, índice parcial de relatórios operacionais.
- Validar planos com `EXPLAIN (ANALYZE, BUFFERS)` em volume sintético antes de manter cada índice.
- Timeout de consulta inferior ao command timeout global; cancellation propagado desde HTTP.
- Rate limit inicial proposto: 60 chamadas/minuto por sujeito, burst 10; detalhes podem ter limite adicional de 20/minuto. Tornar configurável.

## Observabilidade e erros

Um filtro MCP envolve cada chamada e registra:

- `correlationId`, tool, duração, status, código de erro;
- sujeito pseudonimizado (HMAC/identificador não reversível), nunca e-mail;
- quantidade de itens e bytes aproximados, sem valores financeiros;
- eventos separados para falha de autenticação, autorização, rate limit e validação.

Não registrar: argumentos completos, prompt, descrição, categoria digitada, montantes, resposta, token, claims brutos, SQL com parâmetros ou stack trace no payload. Métricas agregadas por tool: total, falhas, duração e tamanho.

## Estratégia de testes

### Unitários

- validação de períodos, datas, limites, enums, top N e cursor;
- taxa de poupança, variação percentual/zero, estorno e arredondamento;
- política operacional para normal, transferência, fatura, repasse e ajuste;
- transformação para DTO minimizado e output schema;
- autorização por scope/audience e pseudonimização de logs.

### Integração

- MCP → tool adapter → `FinancialInsightsService` → PostgreSQL;
- autenticação OAuth, descoberta, PKCE, refresh, rotação e revogação;
- isolamento com dois usuários em todas as tools;
- paginação estável com datas iguais;
- cancelamento, timeout, `429`, falha de banco e payload máximo;
- comprovação read-only comparando estado/contagem/hash lógico antes e depois.

### Segurança

- sem token, inválido, expirado, revogado, audience/resource incorreto e sem scope;
- ID de entidade de outro usuário, cursor adulterado e parâmetros excedentes;
- período excessivo, página excessiva, chamadas concorrentes e tool inexistente/de escrita;
- strings de prompt injection em descrição/categoria não alteram seleção, escopo ou resposta;
- logs e erros não contêm secrets ou dados financeiros.

### Contrato

- listagem de tools contém exatamente o allowlist da v1 e hints read-only;
- input/output schemas aceitam exemplos válidos e rejeitam inválidos;
- `structuredContent` corresponde ao schema e aos exemplos documentados;
- smoke test real pelo ChatGPT “Scan Tools” antes de produção.

## Fases de entrega

1. **Gate de decisão**: aprovar arquitetura, OAuth, datas, tools e limites.
2. **Fundação segura**: corrigir configuração de secrets necessária, adicionar OAuth/resource metadata, rate limit, erros e observabilidade.
3. **Módulo de consultas**: política operacional, datas, agregações, paginação e índices medidos.
4. **MCP P1**: resumo, comparação e saldos; validar ponta a ponta.
5. **MCP P2**: categorias, detalhes, recorrências, metas e investimentos.
6. **Hardening e documentação**: segurança, contrato, quickstart, revogação e deploy controlado.

## Estrutura e arquivos provavelmente afetados

```text
MyFinance.API/
├── Program.cs
├── MyFinance.API.csproj
├── appsettings.json
├── Authentication/                 # OAuth/resource-server adapters
├── Mcp/                            # tool adapters, contracts e filters
├── Services/
│   ├── FinancialInsightsService.cs
│   ├── FinancialSnapshotService.cs # somente se interface reutilizável precisar evoluir
│   └── ReportingPolicy.cs
├── Data/AppDbContext.cs
└── Migrations/                     # OAuth e índices aprovados

tests/backend/
├── Finflow.Api.LogicTests/         # regras financeiras e validações
├── Finflow.Api.ContractTests/      # smoke/deploy
└── Finflow.Api.McpIntegrationTests/# novo projeto se WebApplicationFactory justificar

docs/
├── FINFLOW_MCP/                    # artefatos desta feature
├── ARCHITECTURE.md                 # após implementação/aprovação
├── SPEC.md                         # após implementação/aprovação
├── DECISIONS.md                    # ADR após aprovação
└── EXAMPLES.md                     # conexão e chamadas após implementação

.env.example                        # apenas se aprovado pela governança; sem secrets
CHANGELOG.md                         # somente no lançamento
CHANGELOG_AI.md                      # em toda sessão com alterações
```

O front-end React não precisa ser alterado, exceto se a autorização integrada exigir uma tela explícita de consentimento/revogação. Essa necessidade deve ser decidida no spike OAuth antes de incluir trabalho de UI.

## Decisões e dúvidas que exigem aprovação

1. **OAuth integrado (recomendado) ou IdP externo**: recomenda-se OpenIddict no backend para reutilizar usuários; IdP externo reduz código de segurança, mas exige nova infraestrutura e vinculação de identidades.
2. **Exposição**: endpoint público HTTPS no Render (recomendado para simplicidade) ou Secure MCP Tunnel/rede privada.
3. **Datas legadas**: confirmar que `transactions.date` representa data civil brasileira, não instante UTC real.
4. **Escopo da primeira entrega**: P1 com resumo/comparação/saldos primeiro (recomendado), deixando detalhes/metas/investimentos para P2.
5. **Limites**: aprovar 24 meses agregado, 12 meses por comparação, 92 dias de detalhes e página máxima 100.
6. **Retenção de auditoria**: definir prazo; proposta inicial de 30 dias para logs técnicos sem conteúdo financeiro.

## Complexidade justificada

| Complexidade | Por que é necessária | Alternativa rejeitada |
|---|---|---|
| OAuth 2.1 separado do JWT de sessão | consentimento, scope, audience e revogação individual para dados privados | reutilizar JWT de 30 dias concede privilégio amplo e não cumpre descoberta MCP |
| Novo módulo de consultas | concentra semântica e agrega no banco | tools chamando controllers ou tabelas diretamente duplicariam regras e testes |
| Índices compostos medidos | consultas por titular+período/categoria | confiar apenas em índices simples degrada com crescimento do histórico |

## Confidence

### Alta

- Stack, entidades, autenticação atual, semântica de reporting, serviço reutilizável e limitações de consulta foram confirmados no código.
- Streamable HTTP, OAuth para dados privados e schemas/hints seguem documentação primária do MCP e OpenAI.

### Média

- OpenIddict integrado é a opção recomendada, mas ainda requer spike de compatibilidade com o cliente ChatGPT e o SDK MCP escolhido.
- Metas de latência, rate limit e índices são propostas a medir.

### Baixa

- O suporte exato do plano/ambiente ChatGPT do titular e a necessidade de fallback de transporte só podem ser validados conectando o servidor de teste.

## Validação Humana Necessária

- Aprovar as seis decisões listadas antes de executar `TASKS.md`.
