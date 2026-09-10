# Recuperação documental — incidente Spec Kit/PowerShell

Data: 2026-09-10

## Classificação

| Documento | Classificação | Evidência |
|---|---|---|
| `SPEC.md` | `RECOVERED_EXACT` | Recuperado do transcript local da sessão Codex de 2026-09-10; conteúdo reaplicado do patch original. |
| `PLAN.md` | `RECOVERED_PARTIAL` | Corpo principal recuperado do patch original no transcript; hash do checkpoint anterior não pôde ser reproduzido após atualizações subsequentes. Deve ser revisado contra decisões aprovadas. |
| `TASKS.md` | `RECOVERED_PARTIAL` | Corpo principal recuperado do patch original no transcript; marcações/ajustes posteriores não puderam ser reproduzidos com certeza. Deve ser revisado antes de usar como backlog. |
| `CONTRACTS.md` | `RECOVERED_EXACT` | Artefato sobrevivente no diretório e hash preservado no checkpoint. |
| `DATA_MODEL.md` | `RECOVERED_EXACT` | Artefato sobrevivente no diretório e hash preservado no checkpoint. |
| `RESEARCH.md` | `RECOVERED_EXACT` | Recuperado integralmente do stdout registrado no transcript local da sessão Codex; conteúdo reaplicado. |

## Causa raiz

O teste de pré-requisitos do Spec Kit tentou criar cópias temporárias `spec.md`, `plan.md`, `tasks.md`, `research.md` e `data-model.md`. Em Windows, o provider PowerShell resolveu os nomes sem distinção de maiúsculas/minúsculas; as cópias apontaram para os mesmos arquivos e a limpeza subsequente removeu os artefatos originais. Como os documentos ainda não estavam versionados, não havia restauração via `git checkout`.

## Proteções adotadas

- Backup externo criado em `C:\Users\Gabriel\.codex\visualizations\2026\09\10\01a08b81-ee55-7a61-ac26-3d53d28a5166\finflow-mcp-recovery-backup`.
- Não executar novamente cópia/remoção case-insensitive dentro de `docs/FINFLOW_MCP`.
- O contexto do Spec Kit permanece apontando para o diretório, mas a análise deve tratar os nomes reais em maiúsculas sem criar aliases.
- Este incidente deve ser mantido no histórico e os artefatos devem ser versionados antes da próxima implementação.

## Estado

Os seis documentos de especificação foram recuperados ou confirmados. A implementação parcial permanece preservada. Nenhum P2 foi iniciado nesta sessão.

## Baseline LogicTests

Foi criado um snapshot limpo via `git archive HEAD` em diretório externo porque o repositório não permitiu `git worktree` (permissão no `.git/worktrees`). A mesma suíte apresentou 17 aprovados e as mesmas 5 falhas observadas no working tree:

- `ImportingInvoicePayment_WithoutExistingCard_ProvisionsCreditCardAndCreatesTransfer` — `PRE_EXISTING`.
- `ImportingInvoicePayment_CreatesTransferInsteadOfFalseIncome` — `PRE_EXISTING`.
- `ImportingSameStatementTwice_DoesNotDuplicateTransactions` — `PRE_EXISTING`.
- `ImportingChargeback_OnCreditCard_ReducesLiability_ForTotalAndPartialReversal` — `PRE_EXISTING`.
- `ImportingInvoicePayment_WithMultipleCardsAndAmbiguousDescription_CreatesManualReview` — `PRE_EXISTING`.

Não há evidência de regressão causada pelo código MCP nessas cinco falhas.
