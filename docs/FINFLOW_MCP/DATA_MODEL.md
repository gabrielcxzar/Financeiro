# Modelo de dados relevante ao FinFlow MCP

**Feature**: `001-finflow-mcp`  
**Status**: modelo atual consolidado e integração MCP implementada; índices e read models continuam explicitamente propostos quando não adotados.

## Entidades existentes reutilizadas

| Entidade | Papel nas consultas MCP | Campos relevantes | Restrições de exposição |
|---|---|---|---|
| `User` | titular derivado do token | `Id` | nunca expor e-mail, senha/hash; `Id` não é argumento |
| `Account` | conta, cartão ou conta de investimento | nome, saldo inicial, tipo, indicador/limite/dias do cartão | saldo atual deve vir da regra de snapshot, não ser aceito sem reconciliação |
| `Transaction` | fato financeiro principal | valor decimal, data, tipo, pago, conta, categoria, flags de reporting | omitir memo bruto, origem/arquivo, external ID e import batch |
| `Category` | dimensão de receita/despesa | nome, tipo | cor/ícone não são necessários ao LLM na v1 |
| `RecurringTransaction` | compromisso mensal conhecido | descrição, valor, tipo, dia, ativo, conta/categoria | somente despesas ativas em `get_recurring_expenses` |
| `Budget` | limite essencial mensal usado em planejamento | valor, mês/ano, essencial, categoria | não terá tool própria na v1; pode compor resumo futuro |
| `FinancialGoal` | reserva/dívida e aporte planejado | tipo, alvo, atual, data, contribuição, status | omitir notas livres |
| `FiiHolding` | posição de FII ao custo | ticker, cotas, preço médio | omitir notas; não inventar cotação/valor de mercado |
| `ImportBatch` e `ImportedStatementItem` | reconciliação interna | vínculos com transação/conta | não expor na v1 |
| `CategorizationRule` | regra interna de importação | padrão, prioridade | não expor na v1 |

## Semântica de relatório

Uma **movimentação operacional** atende simultaneamente:

```text
ReportingKind == normal
AND IsTransfer == false
AND ExcludeFromReports == false
```

Agregados de renda e consumo usam essa população por padrão. Pagamento de fatura, transferência interna, repasse e ajuste técnico são movimentos de liquidação/contabilidade e não devem duplicar renda ou consumo. Estornos em cartão podem ser receitas operacionais que reduzem despesa; a regra de sinal deve ser testada por tipo de conta.

## Read models não persistidos

- `FinancialSummary`: período, renda, despesa, fluxo líquido, taxa de poupança, contagem, série mensal e categorias.
- `PeriodComparison`: dois resumos e deltas absolutos/percentuais.
- `CategorySpending`: categoria, montante, participação e quantidade.
- `TransactionPage`: projeções minimizadas, cursor e `hasMore`.
- `KnownBalanceSnapshot`: caixa, pendências, projeção, passivos de cartão e patrimônio contábil.
- `RecurringCommitment`, `GoalProgress` e `InvestmentPosition`: projeções minimizadas das entidades existentes.

Esses tipos são DTOs de aplicação/MCP. Não criam tabelas financeiras novas e não devem incorporar entidades EF completas.

## Dados novos de autorização

O provedor OAuth aprovado poderá exigir tabelas para clients, authorizations, grants/tokens e revogação. O schema exato é responsabilidade da biblioteca/provedor escolhido e só será criado após o spike. Requisitos invariantes:

- vínculo inequívoco do `sub` autorizado com um `User.Id` existente;
- tokens ou códigos persistidos somente em formato protegido/hash quando suportado;
- scope, resource/audience, expiração, status de revogação e timestamps auditáveis;
- cascade/retenção definidos para desconectar o ChatGPT sem apagar dados financeiros;
- nenhum secret de client em tabela ou repositório quando o cliente é público com PKCE.

## Relacionamentos e isolamento

```text
OAuth subject ──mapeia──> User
User ──possui──> Account, Transaction, Category, RecurringTransaction,
                  Budget, FinancialGoal, FiiHolding
Transaction ──refere──> Account e Category
```

Toda query começa em `UserId == authenticatedUserId`. Validar um `accountId` ou `categoryId` significa consultar a entidade junto com o mesmo `UserId`; nunca validar o ID isoladamente e aplicar o filtro depois.

## Datas e moeda

- Colunas de dinheiro já usam `decimal` em C#; o contrato exige confirmar `numeric` no schema efetivo.
- `Transaction.Date` é `timestamp without time zone` com comportamento legado do Npgsql.
- Para a v1, inputs são datas civis e queries usam início inclusivo/fim exclusivo.
- A interpretação `America/Sao_Paulo` precisa ser confirmada com registros reais de virada de mês antes de congelar a transformação.
- `FiiHolding.AvgPrice * Shares` produz custo base, não valor de mercado.

## Índices atuais e propostos

Confirmados atualmente: índices simples por usuário/conta/categoria e índice único de importação `(user_id, accountid, source, external_id)`.

Propostos, sujeitos a `EXPLAIN`:

- `transactions (user_id, date DESC, id DESC)` para período/paginação;
- `transactions (user_id, categoryid, date)` para agregação por categoria;
- índice parcial operacional somente se o PostgreSQL demonstrar ganho e o predicado for estável.

## Lacunas que limitam respostas

- Não há histórico persistido de patrimônio/snapshots; séries precisam ser derivadas do ledger e qualificadas.
- Não há cotação de mercado FII persistida vinculada às posições.
- Cotações do Tesouro são externas/cacheadas, mas não representam posições individuais do usuário.
- `Account.CurrentBalance` é derivado e pode ficar defasado; `FinancialSnapshotService` é a fonte de cálculo atual.
- Não existe moeda por transação; a v1 assume BRL para todo o conjunto.

## Confidence

### Alta

- Entidades, relacionamentos, tipos C#, campos sensíveis e índices existentes foram verificados no código e migrations.

### Média

- Índices e read models são propostas a validar por testes e plano de execução.

### Baixa

- A semântica temporal dos dados legados depende de confirmação humana/amostra de produção.

## Validação Humana Necessária

- Confirmar datas legadas e se patrimônio contábil derivado atende à primeira versão.
