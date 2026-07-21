# Finflow - Catálogo de Padrões de Código e Arquitetura

Este documento registra os padrões de projeto (Design Patterns), idiolismos de código e convenções arquiteturais formalmente adotados no repositório **Finflow**.

---

## 1. Padrões de Arquitetura no Back-end (.NET 8)

### 1.1. Injeção do ID do Usuário via Claim (Tenant Context Injection)
Todas as Controllers autenticadas utilizam um método auxiliar privado para extrair o ID do usuário corrente do token JWT.

```csharp
// Padrão adotado em todas as Controllers autenticadas
private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
```

### 1.2. Snapshot Service Pattern (Service Encapsulation)
Para evitar que múltiplos controllers reimplementem a lógica complexa de consolidação de saldo, faturas e fluxo projetado, os cálculos foram encapsulados no serviço assíncrono [FinancialSnapshotService.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Services/FinancialSnapshotService.cs).

```csharp
// Registro de dependência Scoped em Program.cs
builder.Services.AddScoped<IFinancialSnapshotService, FinancialSnapshotService>();
```

### 1.3. Resilient Database Access (EF Core Execution Strategy)
Para operações com transações explícitas no PostgreSQL (ex: criação de usuário com categorias padrão em [AuthController.cs](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.API/Controllers/AuthController.cs#L44)), utiliza-se a estratégia de execução com resiliência a falhas do EF Core:

```csharp
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    await using var tx = await _context.Database.BeginTransactionAsync();
    // Operações de escrita no banco
    await tx.CommitAsync();
});
```

---

## 2. Padrões no Front-end (React 18 / JavaScript)

### 2.1. Centralized HTTP Client with Event-Driven Auth Expiration
O módulo [api.js](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/services/api.js) centraliza o Axios e emite um evento global (`finflow:auth-expired`) quando recebe HTTP status `401 Unauthorized`. O componente raiz [App.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/App.jsx#L187) escuta esse evento para desautenticar a sessão e redirecionar o usuário para o Login sem recarregar a página.

```javascript
// Interceptor de resposta em api.js
if (error.response.status === 401) {
  clearStoredAuth();
  window.dispatchEvent(new Event(authExpiredEvent));
  return Promise.reject(buildApiError(error, 'Sua sessao expirou. Entre novamente.'));
}
```

### 2.2. Modal Driven Action Flow
A criação e edição de registros são tratadas em componentes modais autônomos (ex: [AddTransactionModal.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/components/AddTransactionModal.jsx), [TransferModal.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/components/TransferModal.jsx), [ImportModal.jsx](file:///c:/Users/Dan13/OneDrive/Documentos/Projetos%20dev/Pessoais/Financeiro/MyFinance.Web/src/components/ImportModal.jsx)). Ao concluir com sucesso, o modal invoca o callback `onSuccess()`, forçando a atualização dos dados na página pai.

---

## Confidence

### Alta
- Padrões de C# e React verificados diretamente no código-fonte em produção.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
