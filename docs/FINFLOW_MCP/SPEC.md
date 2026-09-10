# Especificação da Feature: FinFlow MCP somente leitura

**Feature ID**: `001-finflow-mcp`  
**Branch planejada**: `codex/001-finflow-mcp` (não criada nesta etapa)  
**Criada em**: 2026-09-10  
**Status**: Rascunho para aprovação  
**Entrada**: disponibilizar contexto financeiro confiável do FinFlow ao ChatGPT por MCP, sem qualquer capacidade de escrita.

## Problema

O usuário precisa exportar e copiar manualmente seus dados para que uma IA compreenda seu mês financeiro. Esse processo perde continuidade, favorece análises com dados incompletos e expõe mais informação do que uma pergunta normalmente exige.

## Objetivo e resultado esperado

Oferecer ao ChatGPT consultas autenticadas, estruturadas, pequenas e auditáveis sobre dados já registrados no FinFlow. A versão inicial deve permitir analisar fluxo de caixa, gastos, saldos conhecidos, compromissos recorrentes, metas e posições de investimento sem criar, editar ou excluir qualquer dado.

O MCP fornece fatos e agregados; decisões ou recomendações financeiras continuam sendo responsabilidade do assistente e do usuário. Nenhum retorno deve ser apresentado como garantia de rendimento ou como execução de operação financeira.

## Usuários e atores

- **Titular do FinFlow**: autoriza e revoga o acesso aos próprios dados.
- **ChatGPT/MCP client**: solicita apenas as consultas publicadas e usa os resultados na conversa.
- **Operador do FinFlow**: configura secrets, acompanha métricas e investiga falhas sem acessar descrições financeiras nos logs.
- **Atacante ou conteúdo não confiável**: pode tentar manipular parâmetros, descrições de transações ou instruções para ampliar/exfiltrar dados.

## Fora do escopo da versão 1

- Criar, editar ou excluir transações, contas, categorias, recorrências, metas, orçamentos ou investimentos.
- Importar extratos, confirmar lotes, transferir dinheiro, executar PIX ou acessar bancos.
- Comprar, vender ou recomendar automaticamente ativos específicos.
- Alterar o login atual do front-end.
- Sincronizar preços de mercado ou inventar valor atual de ativos sem cotação persistida.
- Expor senha, hash, token, connection string, memo bruto de importação ou metadados internos desnecessários.
- Fornecer histórico patrimonial “de mercado” quando o FinFlow só conhece valores contábeis.

## Cenários de usuário e testes

### US1 — Compreender a situação financeira (P1)

Como titular, quero que o ChatGPT obtenha um resumo de um período e o compare com outro, para contextualizar decisões de poupança ou investimento.

**Por que P1**: entrega o valor central sem precisar expor transações individuais.

**Teste independente**: com transações de receita, despesa, transferência e pagamento de fatura em dois períodos, a consulta retorna agregados corretos, exclui lançamentos não operacionais por padrão e não altera o banco.

**Cenários de aceite**:

1. **Dado** um mês com receitas e despesas operacionais, **quando** o resumo é solicitado, **então** receitas, despesas, fluxo líquido, taxa de poupança e principais categorias refletem somente o período.
2. **Dado** um período de comparação válido, **quando** a comparação é solicitada, **então** variações absolutas e percentuais são retornadas e divisões por zero resultam em `null`, não em infinito.
3. **Dado** um pagamento de fatura ou transferência interna, **quando** o resumo é solicitado no modo padrão, **então** o valor não é contado novamente como consumo ou renda.

### US2 — Investigar gastos sem exportar todo o histórico (P2)

Como titular, quero consultar categorias e uma página limitada de transações filtradas para explicar variações ou gastos extraordinários.

**Por que P2**: detalhes são úteis para explicação, mas aumentam risco de privacidade e só devem ser usados quando agregados não bastarem.

**Teste independente**: uma consulta filtrada retorna no máximo o limite permitido, cursor estável, campos minimizados e somente registros do usuário autenticado.

**Cenários de aceite**:

1. **Dado** um filtro por data, conta, categoria, tipo e faixa de valor, **quando** a consulta é executada, **então** todos os itens atendem aos filtros e o resultado é ordenado de forma determinística.
2. **Dado** mais resultados que o limite, **quando** a primeira página é retornada, **então** existe um cursor opaco e nenhuma página excede 100 itens.
3. **Dado** um intervalo superior a 92 dias, **quando** detalhes são solicitados, **então** a chamada falha com erro seguro e orienta a reduzir o período ou usar agregação.

### US3 — Avaliar liquidez e compromissos (P2)

Como titular, quero que o ChatGPT consulte saldos conhecidos, passivos de cartão, despesas recorrentes e metas, para não tratar todo saldo em caixa como livre para investir.

**Por que P2**: completa o contexto de decisão financeira sem exigir acesso de escrita.

**Teste independente**: dados sem contas ou recorrências retornam coleções vazias e totais zero; cartões são passivos e metas ativas são identificadas sem expor notas livres.

**Cenários de aceite**:

1. **Dado** contas correntes e cartões, **quando** os saldos são consultados, **então** caixa, passivo conhecido e patrimônio contábil líquido são distinguidos.
2. **Dado** recorrências ativas e inativas, **quando** compromissos são consultados, **então** somente regras ativas do usuário são retornadas.
3. **Dado** posições FII sem cotação atual persistida, **quando** posições são consultadas, **então** quantidade e custo médio são retornados, mas nenhum valor de mercado é inventado.

### US4 — Controlar e auditar o acesso (P1)

Como titular, quero autorizar, limitar e revogar o ChatGPT, e quero que chamadas sejam auditáveis sem registrar meus dados financeiros.

**Por que P1**: dados financeiros não podem ser expostos antes de existir controle de acesso verificável.

**Teste independente**: chamadas anônimas, tokens inválidos, expirados, revogados, com audiência incorreta ou sem `finflow.read` falham; uma chamada válida gera evento técnico sem parâmetros sensíveis.

**Cenários de aceite**:

1. **Dado** um cliente sem autorização, **quando** acessa o endpoint MCP, **então** recebe `401` com descoberta OAuth adequada e nenhum dado.
2. **Dado** um token válido sem escopo de leitura, **quando** chama uma tool, **então** recebe `403` e nenhum dado.
3. **Dado** um acesso revogado, **quando** o token é reutilizado, **então** a chamada falha após a janela de revogação definida.
4. **Dado** uma chamada concluída, **quando** o log é consultado, **então** contém correlação, tool, duração, status e contagem/tamanho aproximado, sem descrições, valores, prompts ou tokens.

## Casos extremos

- Período sem dados, renda zero, valores negativos por estorno e categorias removidas/nulas.
- Datas no limite do mês e registros legados `timestamp without time zone`.
- Cartão sem fechamento/vencimento configurado e passivo negativo por estorno excedente.
- Transações futuras, pendentes, excluídas de relatórios ou classificadas como transferência, pagamento de fatura, repasse ou ajuste técnico.
- Dois itens com mesma data durante paginação.
- IDs de conta/categoria pertencentes a outro usuário.
- Cursor alterado, schema inválido, parâmetros adicionais, números fora de faixa e chamadas repetidas.
- Dados textuais contendo instruções maliciosas; descrições são dados, nunca instruções.
- Falha, timeout ou cancelamento do banco sem exposição de stack trace.

## Requisitos funcionais

- **RF-MCP-001**: o servidor deve publicar somente tools de leitura na versão 1 e marcá-las com a anotação MCP de somente leitura.
- **RF-MCP-002**: toda consulta deve derivar o `UserId` exclusivamente da identidade autenticada; a entrada nunca aceita `userId`.
- **RF-MCP-003**: o servidor deve fornecer resumo financeiro por intervalo de datas, incluindo receitas, despesas, fluxo líquido, taxa de poupança e decomposição mensal/categorias quando solicitada.
- **RF-MCP-004**: o servidor deve comparar dois períodos válidos no lado servidor.
- **RF-MCP-005**: o servidor deve fornecer gastos por categoria agregados no lado servidor.
- **RF-MCP-006**: o servidor deve listar transações com filtros, paginação por cursor, ordenação determinística e projeção de campos minimizada.
- **RF-MCP-007**: o servidor deve fornecer saldos contábeis conhecidos, passivos de cartão e patrimônio contábil líquido, deixando explícita a data de referência e a natureza contábil do valor.
- **RF-MCP-008**: o servidor deve fornecer despesas recorrentes ativas, metas financeiras e posições de investimento conhecidas, sem campos livres desnecessários.
- **RF-MCP-009**: transferências internas, pagamentos de fatura, repasses e ajustes técnicos devem ser excluídos dos agregados operacionais por padrão, com semântica única e testada.
- **RF-MCP-010**: todos os valores devem declarar moeda `BRL`; datas de negócio usam `YYYY-MM-DD`; instantes técnicos usam UTC ISO 8601; período usa início inclusivo e fim inclusivo no contrato.
- **RF-MCP-011**: taxa de poupança deve ser `netCashFlow / income` quando `income > 0`; caso contrário deve ser `null` com motivo estruturado.
- **RF-MCP-012**: respostas devem conter `data`, `meta` e, em falha, erro estruturado com código estável e `correlationId`.
- **RF-MCP-013**: a autorização deve poder ser revogada sem trocar o segredo global do login da aplicação.
- **RF-MCP-014**: nenhuma tool deve chamar endpoints de escrita existentes nem executar `SaveChanges`.

## Requisitos não funcionais

- **RNF-MCP-001 — Correção monetária**: cálculos usam `decimal`/`numeric`; arredondamento, quando necessário, é explícito em duas casas com regra documentada.
- **RNF-MCP-002 — Privacidade**: agregação no servidor é o caminho preferencial; transações detalhadas omitem `RawMemo`, arquivo de origem, identificadores externos e metadados de importação.
- **RNF-MCP-003 — Segurança**: produção usa HTTPS, OAuth 2.1 Authorization Code com PKCE, audiência vinculada ao recurso, escopo mínimo `finflow.read`, tokens curtos e refresh token revogável/rotativo.
- **RNF-MCP-004 — Validação**: schemas rejeitam intervalos, limites, cursores e enumerações inválidos antes da consulta.
- **RNF-MCP-005 — Limites**: resumo/categorias aceitam no máximo 24 meses; cada período de comparação, 12 meses; detalhes, 92 dias; página padrão 50 e máxima 100.
- **RNF-MCP-006 — Desempenho**: agregações são executadas no PostgreSQL quando traduzíveis; leituras usam `AsNoTracking`; não pode haver N+1; toda operação aceita cancelamento e timeout.
- **RNF-MCP-007 — Observabilidade**: registrar tool, duração, sucesso/falha, código de erro, contagem e tamanho aproximado, correlation ID e identificador pseudonimizado do sujeito.
- **RNF-MCP-008 — Resiliência**: falhas internas retornam mensagem genérica; stack trace e SQL nunca são enviados ao cliente.
- **RNF-MCP-009 — Compatibilidade**: transporte remoto Streamable HTTP stateless em endpoint HTTPS estável.
- **RNF-MCP-010 — Controle de abuso**: rate limit por sujeito autenticado e IP, com `429` e indicação de retry, sem filas ilimitadas.
- **RNF-MCP-011 — Evolução**: mudança que introduza escrita exige nova feature, nova análise de ameaça e aprovação explícita.

## Informações consultáveis

- Agregados de receita, despesa, fluxo líquido, taxa de poupança e séries mensais.
- Gastos por categoria e variação entre períodos.
- Transações operacionais minimizadas, sob filtros e limites.
- Saldos conhecidos de contas, passivos de cartão e patrimônio contábil.
- Regras de despesas recorrentes ativas, metas e contribuições planejadas.
- Posições FII conhecidas por quantidade/custo e saldos de contas de investimento.

## Informações não expostas

- Senha, hash, e-mail, secrets, access/refresh tokens e connection strings.
- `RawMemo`, conteúdo de arquivo importado, nome do arquivo, hash, external ID e justificativas de revisão.
- Notas livres de metas/holdings na versão 1.
- Dados de outros usuários ou IDs internos que não sejam necessários para filtros estáveis.
- Qualquer dado bancário externo que não esteja persistido no FinFlow.

## Critérios mensuráveis de sucesso

- **CS-001**: 100% das tools publicadas na v1 são somente leitura e os testes comprovam ausência de alteração no banco.
- **CS-002**: 100% dos testes de isolamento retornam zero registros de outro usuário.
- **CS-003**: 100% dos contratos rejeitam intervalos acima dos limites e páginas acima de 100.
- **CS-004**: agregados de referência conciliam exatamente com fixtures financeiras usando `decimal`.
- **CS-005**: p95 local das consultas agregadas com conjunto de referência é inferior a 500 ms; detalhes, inferior a 750 ms, excluindo latência de rede do cliente.
- **CS-006**: nenhum log de teste contém descrição de transação, valor monetário, prompt, token ou secret.
- **CS-007**: o servidor passa testes de contrato MCP, autenticação, autorização, revogação, rate limit e respostas de erro.

## Premissas

- A versão 1 será usada pelo titular e continuará respeitando isolamento multiusuário.
- `BRL` é a única moeda persistida atualmente; não haverá conversão cambial.
- Datas financeiras são tratadas como datas civis em `America/Sao_Paulo`; a estratégia para registros legados deve ser validada antes da implementação.
- “Patrimônio” na v1 significa patrimônio contábil conhecido, não valor de mercado completo.
- Recomendações de investimento são produzidas pelo ChatGPT, não pelo MCP.

## Riscos

- Autenticação JWT atual não é um servidor OAuth compatível com descoberta MCP, não valida emissor/audiência e usa token de 30 dias.
- O serviço de snapshot carrega todas as transações do usuário; reutilizá-lo para séries longas pode ampliar latência e memória.
- O resumo atual aparenta agregar parte do histórico fora do mês solicitado; o novo contrato exige consultas de período independentes e testes de regressão.
- Índices atuais não cobrem de forma explícita os principais padrões `(user_id, date)` e `(user_id, categoryid, date)`.
- Timestamps legados sem timezone podem deslocar limites se tratados como instantes UTC.
- Descrições importadas são conteúdo não confiável e podem conter prompt injection.

## Confidence

### Alta

- Escopo somente leitura, entidades existentes, isolamento por `UserId`, moeda decimal e limitações atuais foram verificados no código.

### Média

- Limites e metas de latência são propostas conservadoras para validação em implementação.

### Baixa

- A disponibilidade exata do fluxo de conexão no plano atual do ChatGPT deve ser confirmada no ambiente do titular antes do deploy.

## Validação Humana Necessária

- Aprovar o modelo OAuth e a estratégia de exposição pública/túnel.
- Confirmar a interpretação das datas legadas e os limites propostos.
- Confirmar se posições de investimento e metas entram no primeiro incremento ou ficam para uma segunda entrega.
