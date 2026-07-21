# Bootstrap do Projeto Finflow

Este documento é o guia de onboarding agnóstico de ferramentas para qualquer desenvolvedor humano ou agente de inteligência artificial (Antigravity, Claude, ChatGPT, Cursor, Windsurf, Codex, etc.) que necessite interagir com o repositório **Finflow**.

---

# Objetivo do projeto

O **Finflow** é uma plataforma de gestão financeira pessoal desenvolvida em arquitetura desacoplada (Web SPA em React + Web API RESTful em ASP.NET Core 8 + PostgreSQL no Supabase). O sistema provê funcionalidades de controle patrimonial, fluxo de caixa real e projetado, gestão de cartões de crédito, compras parceladas, transferências entre carteiras, orçamentos, metas de economia, investimentos (FIIs e Tesouro Direto) e importação inteligente de extratos bancários.

---

# Arquitetura Documental

A documentação do repositório adota o padrão de **Estrutura Corporativa para Desenvolvimento Assistido por IA**:

```text
Financeiro/
├── README.md                           # Ponto de entrada para desenvolvedores humanos
├── CHANGELOG.md                        # Histórico de lançamentos funcionais
├── CHANGELOG_AI.md                     # Registro auditável de modificações por IAs
│
├── docs/                               # Documentação técnica do sistema
│   ├── ARCHITECTURE.md                 # Arquitetura técnica e diagramas Mermaid/ERD
│   ├── SPEC.md                         # Requisitos funcionais e não-funcionais
│   ├── ROADMAP.md                      # Backlog técnico e débitos de código
│   ├── DECISIONS.md                    # Registro Histórico de Decisões (ADRs)
│   ├── PATTERNS.md                     # Padrões de código C# e React
│   └── EXAMPLES.md                     # Exemplos de chamadas HTTP e snippets
│
├── .ai/                                # Diretrizes e contexto para Agentes de IA
│   ├── BOOTSTRAP_PROJECT.md            # Onboarding agnóstico e ordem de leitura
│   ├── AGENTS.md                       # Regras de comportamento e compatibilidade
│   ├── AI_CONVENTIONS.md               # Manual operacional padrão para IAs
│   ├── GOVERNANCE.md                   # Políticas permanentes e governança
│   ├── CONTEXT.md                      # Contexto funcional e mapa de estrutura
│   └── MEMORY.md                       # Memória permanente consolidada
│
└── .meta/                              # Artefatos analíticos gerados por auditoria
    ├── AUDIT.md                        # Relatório de auditoria técnica completa
    ├── INVENTORY.md                    # Inventário analítico de componentes
    ├── FILE_INDEX.md                   # Índice completo de arquivos
    ├── DEPENDENCY_GRAPH.md             # Grafo de dependências do projeto
    ├── METRICS.md                      # Métricas e contagem de código
    └── WORKSPACE_ANALYSIS.md           # Análise técnica do workspace
```

---

# Ordem recomendada de leitura

Para obter pleno entendimento do projeto sem perda de contexto ou premissas incorretas, qualquer novo agente de IA ou desenvolvedor deve ler os arquivos exatamente nesta ordem:

1. [README.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/README.md) - Visão geral e mapa de documentos.
2. [.ai/BOOTSTRAP_PROJECT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/BOOTSTRAP_PROJECT.md) - Este guia de onboarding.
3. [.ai/GOVERNANCE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/GOVERNANCE.md) - Regras permanentes e políticas.
4. [.ai/AGENTS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AGENTS.md) - Comportamento e compatibilidade de IA.
5. [.ai/AI_CONVENTIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AI_CONVENTIONS.md) - Manual operacional de edição e criação.
6. [.ai/CONTEXT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/CONTEXT.md) - Contexto funcional detalhado e mapa do projeto.
7. [.ai/MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md) - Memória permanente consolidada.
8. [docs/ARCHITECTURE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/ARCHITECTURE.md) - Arquitetura de software e banco.
9. [docs/SPEC.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/SPEC.md) - Requisitos funcionais e não-funcionais.
10. [docs/DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md) - Decisões Arquiteturais (ADRs).
11. [docs/PATTERNS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/PATTERNS.md) - Padrões C# e React.
12. [docs/EXAMPLES.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/EXAMPLES.md) - Exemplos HTTP e payloads.
13. [docs/ROADMAP.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/ROADMAP.md) - Backlog de débitos técnicos.
14. [.meta/AUDIT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/AUDIT.md) - Relatório de auditoria completa.

---

# Estratégia para compreender o projeto

1. O **código-fonte** é a fonte máxima da verdade.
2. Utilize os artefatos de `.meta/` para auditoria estática rápida:
   - [.meta/INVENTORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/INVENTORY.md) para localizar todos os Controllers, Models e Páginas.
   - [.meta/DEPENDENCY_GRAPH.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/DEPENDENCY_GRAPH.md) para entender pacotes NuGet e NPM.
3. Inspecione os métodos em [FinancialSnapshotService.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Services/FinancialSnapshotService.cs) para entender os cálculos consolidados de saldo.

---

# Estratégia para validar informações

- **Regras de Negócio**: Inspecione a suíte de testes em [FinancialCoreLogicTests.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.LogicTests/FinancialCoreLogicTests.cs).
- **Contratos de API**: Inspecione [FinflowApiClient.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/tests/backend/Finflow.Api.ContractTests/FinflowApiClient.cs).
- **Validação de Execução**: Execute os testes automatizados (`dotnet test`) antes de finalizar tarefas.

---

# Estratégia para atualizar documentação

- Atualize arquivos existentes em suas pastas oficiais (`docs/`, `.ai/`, `.meta/`).
- Marque qualquer inferência com o rótulo **Inferência**.
- Adicione obrigatoriamente a seção `## Confidence` (Alta, Média, Baixa) e `## Validação Humana Necessária` em todo documento criado ou modificado.

---

# Estratégia para preservar conhecimento

1. NUNCA remova seções históricas ou regras sem justificativa.
2. Registre novidades em [docs/SPEC.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/SPEC.md).
3. Registre decisões arquiteturais em [docs/DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md).

---

# Estratégia para registrar decisões

Abra [docs/DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md) e adicione uma nova entrada numerada sequencialmente preservando todas as decisões anteriores.

---

# Estratégia para atualizar memória

Adicione em [.ai/MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md) apenas fatos estáveis e comportamentais verificados. Nunca insira estados temporários ou logs de bugs resolvidos.

---

# Checklist inicial

- [ ] Ler [README.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/README.md)
- [ ] Ler [.ai/BOOTSTRAP_PROJECT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/BOOTSTRAP_PROJECT.md)
- [ ] Ler [.ai/GOVERNANCE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/GOVERNANCE.md)
- [ ] Ler [.ai/AGENTS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/AGENTS.md)
- [ ] Analisar arquitetura em [docs/ARCHITECTURE.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/ARCHITECTURE.md)
- [ ] Inspecionar pontos críticos em [.meta/AUDIT.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.meta/AUDIT.md)

---

# Checklist antes de finalizar

- [ ] Código testado e validado (`dotnet test` / `npm test`)
- [ ] Documentação afetada atualizada nas pastas `docs/`, `.ai/` ou `.meta/`
- [ ] Novas decisões registradas em [docs/DECISIONS.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/docs/DECISIONS.md)
- [ ] Memória permanente atualizada em [.ai/MEMORY.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/.ai/MEMORY.md)
- [ ] Log de alteração de IA registrado em [CHANGELOG_AI.md](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/CHANGELOG_AI.md)
- [ ] Links entre documentos conferidos e utilizando o esquema `file:///...`

---

## Confidence

### Alta
- Guia de onboarding e estrutura corporativa verificados no repositório.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
