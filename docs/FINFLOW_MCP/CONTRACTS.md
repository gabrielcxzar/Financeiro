# Contratos MCP

**Feature**: `001-finflow-mcp`  
**Status**: Implementado e validado via `tools/list`, wire test e fluxo real ChatGPT → Render.

> Implementação atual: os oito métodos estão consolidados em `MyFinance.API/Mcp/FinflowMcpTools.cs`, com output estruturado via `UseStructuredContent=true`. O SDK deriva `inputSchema`/`outputSchema` dos métodos e DTOs; a validação HTTP e o fluxo real foram concluídos. O subject OAuth é a única fonte de identidade e não existe argumento `userId`.

## Convenções comuns

- Todos os inputs usam JSON Schema estrito, `additionalProperties: false`.
- `userId` nunca é input; o sujeito vem do token OAuth.
- Datas de negócio: `YYYY-MM-DD`, timezone `America/Sao_Paulo`, início e fim inclusivos.
- Valores: JSON number produzido a partir de `decimal`, moeda explícita `BRL`.
- Instantes técnicos: ISO 8601 UTC.
- Campos sem valor conhecido são `null`; não omitir para esconder incerteza.
- Toda tool: `readOnlyHint=true`, `destructiveHint=false`, `idempotentHint=true`, `openWorldHint=false`.

## Envelope comum

```json
{
  "data": {},
  "meta": {
    "currency": "BRL",
    "timezone": "America/Sao_Paulo",
    "generatedAt": "2026-09-10T15:30:00Z",
    "dataAsOf": "2026-09-10",
    "partial": false,
    "warnings": []
  }
}
```

Erros estáveis: `AUTHENTICATION_REQUIRED`, `INSUFFICIENT_SCOPE`, `INVALID_ARGUMENT`, `PERIOD_TOO_LARGE`, `PAGE_LIMIT_EXCEEDED`, `INVALID_CURSOR`, `ENTITY_NOT_FOUND`, `RATE_LIMITED`, `QUERY_TIMEOUT`, `INTERNAL_ERROR`. O cliente nunca recebe stack trace ou SQL.

## `get_financial_summary`

**Quando usar**: primeira consulta para entender um período sem expor lançamentos.

**Input**:

```json
{
  "startDate": "2026-09-01",
  "endDate": "2026-09-30",
  "includeMonthlyBreakdown": true,
  "topCategories": 5
}
```

- Datas opcionais em conjunto; ausência significa mês atual.
- Período máximo: 24 meses. `topCategories`: 0–20, padrão 5.

**Data**: `period`, `income`, `expenses`, `netCashFlow`, `savingsRate`, `transactionCount`, `monthly[]`, `topSpendingCategories[]`. Cada categoria contém `categoryId`, `name`, `amount`, `shareOfExpenses`.

## `compare_periods`

**Quando usar**: explicar mudança entre duas janelas sem buscar registros brutos.

**Input**:

```json
{
  "basePeriod": { "startDate": "2026-08-01", "endDate": "2026-08-31" },
  "comparisonPeriod": { "startDate": "2026-09-01", "endDate": "2026-09-30" },
  "topCategoryChanges": 5
}
```

- Cada período máximo: 12 meses; `topCategoryChanges`: 0–20.

**Data**: `base`, `comparison`, `changes`. Cada mudança de métrica contém `absolute`, `percentage`; percentual é `null` quando a base é zero. `categoryChanges[]` contém base, comparação e delta, sem transações.

## `get_spending_by_category`

**Quando usar**: identificar composição dos gastos.

**Input**:

```json
{
  "startDate": "2026-07-01",
  "endDate": "2026-09-30",
  "limit": 10,
  "includeUncategorized": true
}
```

- Período máximo: 24 meses; limite padrão 10, máximo 50.

**Data**: `period`, `totalExpenses`, `categories[]` com `categoryId`, `name`, `amount`, `share`, `transactionCount`; ordenação por `amount desc`, depois nome.

## `get_transactions`

**Quando usar**: somente depois de um agregado indicar que detalhes são necessários.

**Input**:

```json
{
  "startDate": "2026-09-01",
  "endDate": "2026-09-30",
  "accountId": 12,
  "categoryId": 4,
  "type": "expense",
  "status": "paid",
  "minAmount": 10.00,
  "maxAmount": 500.00,
  "includeNonOperational": false,
  "limit": 50,
  "cursor": null
}
```

- Período obrigatório e máximo de 92 dias.
- `type`: `income|expense|all`; `status`: `paid|pending|all`.
- `includeNonOperational` padrão `false`; habilitá-lo não remove os demais limites.
- Limite padrão 50, máximo 100; cursor opaco vinculado aos filtros/sujeito.

**Data**: `period`, `items[]`, `nextCursor`, `hasMore`. Item permitido: `id`, `date`, `description`, `amount`, `type`, `paid`, `category{id,name}`, `account{id,name,isCreditCard}`, `reportingKind`. Proibidos: `UserId`, `RawMemo`, `SourceFile`, `Source`, `ExternalId`, `ImportBatchId`, `ImportedAt`, grupos técnicos e entidades completas.

## `get_account_balances`

**Quando usar**: avaliar liquidez e passivos conhecidos agora.

**Input**:

```json
{
  "includeProjected": true,
  "includeCreditCards": true
}
```

**Data**: `asOfDate`, `cashAccounts[]`, `creditCards[]`, `totals`. Contas incluem saldo contábil real e, se solicitado, pendente/projetado. Cartões incluem passivo e limite conhecido. `totals` distingue `knownCash`, `knownCardLiability`, `knownLedgerNetWorth`; não inclui valor de mercado não persistido.

## `get_recurring_expenses`

**Quando usar**: estimar compromissos fixos conhecidos.

**Input**:

```json
{
  "accountId": null,
  "categoryId": null
}
```

**Data**: `monthlyTotal`, `items[]` com `id`, `description`, `amount`, `dayOfMonth`, categoria e conta minimizadas. Somente regras ativas e do tipo despesa.

## `get_financial_goals`

**Quando usar**: considerar reservas, dívidas e aportes planejados.

**Input**:

```json
{
  "status": "active"
}
```

- `status`: `active|paused|completed|all`; padrão `active`.

**Data**: `monthlyContributionTotal`, `items[]` com `id`, `name`, `goalType`, `targetAmount`, `currentAmount`, `progress`, `targetDate`, `monthlyContribution`, `status`. `notes` não é exposto.

## `get_investment_positions`

**Quando usar**: identificar posições que o FinFlow realmente conhece.

**Input**:

```json
{
  "includeInvestmentAccounts": true,
  "includeFiiHoldings": true
}
```

**Data**: `investmentAccounts[]`, `fiiHoldings[]`, `totals`, `valuation`. FII contém `ticker`, `shares`, `averagePrice`, `costBasis`; `marketPrice` e `marketValue` são `null` enquanto não houver cotação confiável persistida. Notas não são expostas.

## Resources e Prompts

Nenhum na v1. As descrições das tools e as instruções do servidor devem explicar a semântica contábil. Se uso real demonstrar valor, uma nova revisão de contrato pode propor prompt de fechamento mensal; ele não poderá ampliar scope ou contornar limites.

## Compatibilidade e versionamento

- Mudanças aditivas opcionais mantêm a versão da tool.
- Renomear/remover campo, mudar cálculo, limite ou semântica exige nova versão de contrato e teste de compatibilidade.
- Uma futura tool de escrita nunca será adicionada silenciosamente à allowlist v1; exige feature separada.

## Confidence

### Alta

- Campos permitidos/proibidos refletem entidades e riscos verificados no código.

### Média

- Nomes finais e suporte a output schema dependem da versão do SDK/cliente validada no spike.

### Baixa

- Nenhuma.

## Validação Humana Necessária

- Aprovar nomes, limites e inclusão das quatro tools P2 antes de congelar o contrato.
