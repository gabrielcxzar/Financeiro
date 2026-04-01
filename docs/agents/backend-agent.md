# Backend Agent Guide

## Missao

Este agente trabalha exclusivamente na API ASP.NET Core hospedada no Render e integrada ao PostgreSQL do Supabase.

## Stack

- ASP.NET Core 8
- Entity Framework Core 8
- Npgsql
- JWT Bearer Authentication
- BCrypt para hash de senha
- Swagger

## Arquivos centrais

- `MyFinance.API/Program.cs`
- `MyFinance.API/Data/AppDbContext.cs`
- `MyFinance.API/Controllers`
- `MyFinance.API/Models`

## Regras de negocio do back-end

- Quase toda operacao e filtrada por `userId` obtido do JWT.
- O cadastro cria categorias padrao automaticamente.
- O login retorna apenas `token` e `name`.
- Contas de cartao usam `closingDay`, `dueDay` e `creditLimit`.
- Recorrencias podem gerar transacoes futuras e alimentar projecoes.
- A exclusao de categoria falha se houver transacoes associadas.
- O endpoint `users/wipe-data` apaga dados transacionais e recria categorias padrao.
- Investimentos possuem duas fontes:
  - holdings FII no banco
  - taxas do Tesouro via fonte CSV externa

## Convencoes de implementacao

- Controllers concentram endpoints REST.
- `ClaimTypes.NameIdentifier` e a origem de verdade para segregacao por usuario.
- `SaveChangesAsync` e o mecanismo padrao de persistencia.
- `AsNoTracking` deve ser preferido em consultas de leitura.
- `CancellationToken` deve ser propagado em endpoints de leitura pesados.
- Filtros por usuario devem ser aplicados em toda consulta mutavel e de leitura.

## Contratos sensiveis

- JWT depende de `AppSettings:Token`.
- Conexao com banco depende de `ConnectionStrings:DefaultConnection`.
- CORS e configurado globalmente em `Program.cs`.
- A app assume que o prefixo da API e `/api`.

## Riscos conhecidos para futuras IAs

- Existem strings e arquivos com problemas de encoding historicos.
- Ha configuracoes sensiveis versionadas em `appsettings.json`; isso e um problema de seguranca e nao deve ser expandido.
- Parte do bootstrap de schema esta embutida em `Program.cs`, o que dificulta manutencao.
- Nao ha camada de servicos separada; regras de negocio estao nos controllers.

## Testes recomendados para toda alteracao

- Autenticacao: register, login, credenciais invalidas.
- Segregacao de dados por usuario.
- CRUD de contas, categorias, transacoes, recorrencias e orcamentos.
- Projecao de recorrencias.
- Fluxo de importacao de CSV.
- Resiliencia de `tesouro/latest` contra indisponibilidade da fonte externa.

