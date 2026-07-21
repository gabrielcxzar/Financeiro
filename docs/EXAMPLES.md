# Finflow - Exemplos de Utilização

Este documento apresenta exemplos práticos de requisições HTTP, payloads JSON, snippets de código e fluxos de uso da API do **Finflow**.

---

## 1. Exemplos de Requisições HTTP (API)

### 1.1. Login de Usuário (`POST /api/auth/login`)
- **Request**:
```http
POST /api/auth/login HTTP/1.1
Host: localhost:10000
Content-Type: application/json

{
  "email": "usuario@exemplo.com",
  "password": "SenhaForte123!"
}
```

- **Response (200 OK)**:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "name": "Maria Silva"
}
```

---

### 1.2. Criar Transação Parcelada (`POST /api/transactions`)
- **Request**:
```http
POST /api/transactions HTTP/1.1
Host: localhost:10000
Authorization: Bearer <seu_token_jwt>
Content-Type: application/json

{
  "description": "Notebook em 10x",
  "amount": 450.00,
  "date": "2026-07-21T00:00:00Z",
  "type": "Expense",
  "paid": true,
  "accountId": 1,
  "categoryId": 11,
  "installments": 10
}
```

- **Response (201 Created)**:
```json
{
  "id": 102,
  "description": "Notebook em 10x (01/10)",
  "amount": 450.00,
  "date": "2026-07-21T00:00:00Z",
  "type": "Expense",
  "paid": true,
  "accountId": 1,
  "categoryId": 11,
  "userId": 5,
  "installmentId": "b1a2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

---

### 1.3. Realizar Transferência entre Contas (`POST /api/transactions/transfer`)
- **Request**:
```http
POST /api/transactions/transfer HTTP/1.1
Host: localhost:10000
Authorization: Bearer <seu_token_jwt>
Content-Type: application/json

{
  "fromAccountId": 1,
  "toAccountId": 2,
  "amount": 250.00,
  "date": "2026-07-21T00:00:00Z"
}
```

- **Response (200 OK)**:
```json
{
  "message": "Transferencia/Pagamento realizado!"
}
```

---

### 1.4. Resumo do Dashboard (`GET /api/dashboard/summary?month=7&year=2026`)
- **Request**:
```http
GET /api/dashboard/summary?month=7&year=2026 HTTP/1.1
Host: localhost:10000
Authorization: Bearer <seu_token_jwt>
```

- **Response (200 OK)**:
```json
{
  "month": 7,
  "year": 2026,
  "summary": {
    "totalBalance": 12500.50,
    "totalIncome": 8500.00,
    "totalExpense": 3200.00,
    "predictedFixed": 1500.00,
    "pendingTotal": 12500.50,
    "projectedTotal": 16300.50,
    "cardLiability": 850.00,
    "pendingCardLiability": 850.00,
    "projectedCardLiability": 1200.00,
    "netWorth": 11650.50,
    "pendingNetWorth": 11650.50,
    "projectedNetWorth": 15100.50,
    "freeToSpend": 3800.00
  }
}
```

---

## 2. Snippet de Chamada no Front-end (Axios)

```javascript
import api from '../services/api';

export const fetchDashboardSummary = async (month, year) => {
  try {
    const response = await api.get(`/dashboard/summary?month=${month}&year=${year}`);
    return response.data;
  } catch (error) {
    console.error('Erro ao carregar dashboard:', error.message);
    throw error;
  }
};
```

---

## Confidence

### Alta
- Exemplos de requisição e resposta validados diretamente contra as assinaturas dos Controllers em C#.

### Média
- N/A.

### Baixa
- N/A.

## Validação Humana Necessária
- Nenhuma validação manual necessária para este documento.
