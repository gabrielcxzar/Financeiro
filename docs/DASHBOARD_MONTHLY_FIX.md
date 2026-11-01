# Correção dos indicadores mensais do dashboard

## Problema e comportamento esperado

Os totais e categorias somavam o histórico operacional inteiro, embora a tela apresentasse um mês. O resumo deve usar a data civil registrada no intervalo de início inclusivo e início do mês seguinte exclusivo. Receitas e despesas dos cards e gráfico representam lançamentos operacionais confirmados; os valores pendentes cadastrados aparecem separadamente. Recorrências sem lançamento pertencem às projeções, não aos cards. O patrimônio e os saldos continuam sendo snapshots atuais calculados a partir do histórico.

## Critérios de aceitação

- Meses diferentes, limites de período, parcelas futuras, status e política operacional produzem valores independentes e coerentes; categorias e comparação usam despesas confirmadas.
- Transferências, pagamentos de fatura e exclusões não entram nos totais; o valor da fatura por cartão usa o período selecionado.
- Patrimônio não depende do mês; projeções não duplicam recorrência com lançamento correspondente.
- A navegação não exibe resposta atrasada de outro mês, inclusive quando a consulta do mês anterior falha.

## Tarefas e riscos

1. Testar o controller com dados sintéticos de diversos meses, status, fronteiras e classificações.
2. Corrigir os agregados, a fatura e as legendas; testar navegação e comparação.
3. Executar as suítes e builds pertinentes e registrar as decisões.

Riscos: datas legadas são armazenadas com comportamento Npgsql legado; não converter os horários implicitamente evita deslocar datas civis existentes. O MCP inclui transações pendentes cadastradas no resumo e não projeta regras de recorrência, portanto seus totais podem diferir dos cards confirmados. A projeção e livre para gastar têm regras próprias, incluindo faturas e recorrências.

O relatório de despesas e seus percentuais incluem confirmadas e pendentes cadastradas, ao contrário do gráfico de confirmadas do dashboard. A projeção parte do saldo atual mesmo para mês histórico; não é uma reconstrução de fechamento. O livre para gastar subtrai orçamentos essenciais além de transações e faturas, podendo sobrepor valores planejados e efetivos; essa regra preexistente exige revisão funcional separada.

## Confidence

### Alta
- Regras confirmadas na implementação e em testes automatizados.

### Média
- A interpretação de datas civis legadas depende das origens de importação.

### Baixa
- N/A.

## Validação Humana Necessária
- Conferir datas civis de importações e classificar separadamente as movimentações de julho; o fechamento de outubro ainda está pendente.
