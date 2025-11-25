using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // <--- Exige Login
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        // Pega o ID do usuário logado
        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = GetUserId();
            var query = _context.Transactions
                .Where(t => t.UserId == userId) // <--- Filtra por usuário
                .Include(t => t.Category)
                .AsQueryable();

            if (month.HasValue && year.HasValue)
            {
                query = query.Where(t => t.Date.Month == month && t.Date.Year == year);
            }

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
        {
            var userId = GetUserId();
            transaction.UserId = userId;

            if (transaction.Date == default) transaction.Date = DateTime.UtcNow;

            // VERIFICAÇÃO DE CARTÃO DE CRÉDITO
            // Se a conta escolhida for cartão, precisamos ajustar a data para o vencimento
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);
            
            // Lógica de Parcelamento
            int parcelas = transaction.Installments > 1 ? transaction.Installments : 1;
            decimal valorParcela = transaction.Amount;
            
            // Data base para cálculo (se for cartão, usa lógica de fechamento, senão usa data atual)
            DateTime dataBase = transaction.Date;

            // Se for cartão, joga para o vencimento correto
            if (account != null && account.IsCreditCard && account.ClosingDay.HasValue && account.DueDay.HasValue)
            {
                // Se comprou DEPOIS do fechamento, só cai no outro mês
                if (dataBase.Day >= account.ClosingDay.Value)
                {
                    dataBase = dataBase.AddMonths(1);
                }
                
                // Força o dia para o dia do vencimento
                try {
                    dataBase = new DateTime(dataBase.Year, dataBase.Month, account.DueDay.Value);
                } catch {
                    // Caso o dia de vencimento seja 31 e o mês não tenha (ex: Fev), joga pro último dia
                    dataBase = new DateTime(dataBase.Year, dataBase.Month, 1).AddMonths(1).AddDays(-1);
                }
            }

            // LOOP PARA CRIAR AS PARCELAS
            for (int i = 0; i < parcelas; i++)
            {
                var novaTransacao = new Transaction
                {
                    UserId = userId,
                    CategoryId = transaction.CategoryId,
                    AccountId = transaction.AccountId,
                    Type = transaction.Type,
                    Paid = transaction.Paid, // Se for crédito, geralmente nasce "Pendente" (fatura aberta)
                    
                    // Ajusta valor e descrição
                    Amount = valorParcela,
                    Description = parcelas > 1 ? $"{transaction.Description} ({i + 1}/{parcelas})" : transaction.Description,
                    
                    // A cada loop, adiciona 1 mês na data
                    Date = dataBase.AddMonths(i)
                };

                _context.Transactions.Add(novaTransacao);

                // Atualiza saldo da conta IMEDIATAMENTE apenas se NÃO for cartão de crédito
                // (Cartão de crédito não mexe no saldo da conta bancária na hora da compra, só gera dívida)
                if (account != null && !account.IsCreditCard)
                {
                    if (novaTransacao.Type == "Income") account.CurrentBalance += novaTransacao.Amount;
                    else account.CurrentBalance -= novaTransacao.Amount;
                    _context.Entry(account).State = EntityState.Modified;
                }
                else if (account != null && account.IsCreditCard)
                {
                    // Se for cartão, atualizamos o "Saldo" do cartão (que é a dívida acumulada)
                    if (novaTransacao.Type == "Expense") account.CurrentBalance -= novaTransacao.Amount; // Aumenta dívida (negativo)
                    // Não mexemos no saldo "bancário", apenas no limite do cartão
                    _context.Entry(account).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();

            // Retorna a primeira parcela criada só para confirmar
            return CreatedAtAction("GetTransactions", new { id = transaction.Id }, transaction);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var userId = GetUserId();
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction == null) return NotFound();

            // Estorno
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);
            
            if (account != null)
            {
                if (transaction.Type == "Income") account.CurrentBalance -= transaction.Amount;
                else account.CurrentBalance += transaction.Amount;
                _context.Entry(account).State = EntityState.Modified;
            }

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        // PUT: api/Transactions/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, Transaction transaction)
        {
            var userId = GetUserId();
            if (id != transaction.Id) return BadRequest();

            // 1. Busca a transação ANTIGA no banco (para saber o valor velho)
            var oldTransaction = await _context.Transactions
                .AsNoTracking() // Importante para não travar o Entity Framework
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (oldTransaction == null) return NotFound();

            transaction.UserId = userId; // Garante a segurança

            // 2. Atualiza o saldo da Conta (Estorna o velho -> Aplica o novo)
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

            if (account != null)
            {
                // Reverte o efeito da transação antiga
                if (oldTransaction.Type == "Income") account.CurrentBalance -= oldTransaction.Amount;
                else account.CurrentBalance += oldTransaction.Amount;

                // Aplica o efeito da transação nova
                if (transaction.Type == "Income") account.CurrentBalance += transaction.Amount;
                else account.CurrentBalance -= transaction.Amount;

                _context.Entry(account).State = EntityState.Modified;
            }

            // 3. Salva a transação atualizada
            _context.Entry(transaction).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Transactions.Any(e => e.Id == id)) return NotFound();
                else throw;
            }

            return NoContent();
        }
        // POST: api/Transactions/transfer
        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer(TransferDto request)
        {
            var userId = GetUserId();

            // 1. Cria a Saída da Origem
            var expense = new Transaction
            {
                UserId = userId,
                AccountId = request.FromAccountId,
                Amount = request.Amount,
                Description = $"Transferência para conta {request.ToAccountId}",
                Type = "Expense",
                Date = request.Date,
                Paid = true
            };

            // 2. Cria a Entrada no Destino
            var income = new Transaction
            {
                UserId = userId,
                AccountId = request.ToAccountId,
                Amount = request.Amount,
                Description = $"Transferência recebida da conta {request.FromAccountId}",
                Type = "Income",
                Date = request.Date,
                Paid = true
            };

            // 3. Atualiza Saldos
            var fromAcc = await _context.Accounts.FindAsync(request.FromAccountId);
            var toAcc = await _context.Accounts.FindAsync(request.ToAccountId);

            if (fromAcc == null || toAcc == null || fromAcc.UserId != userId || toAcc.UserId != userId)
                return BadRequest("Contas inválidas");

            fromAcc.CurrentBalance -= request.Amount;
            toAcc.CurrentBalance += request.Amount;

            _context.Transactions.Add(expense);
            _context.Transactions.Add(income);
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Transferência realizada!" });
        }

        // DTO auxiliar (Coloque no fim do arquivo ou num arquivo separado)
        public class TransferDto
        {
            public int FromAccountId { get; set; }
            public int ToAccountId { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
        }
        [HttpGet("invoice")]
        public async Task<ActionResult<object>> GetInvoiceSummary([FromQuery] int accountId, [FromQuery] int month, [FromQuery] int year)
        {
            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
            
            if (account == null || !account.IsCreditCard) return BadRequest("Conta não é cartão de crédito");

            // Lógica de Fechamento
            // Fatura de Maio (Mês 5):
            // Fecha dia 20/05.
            // Pega compras de 21/04 até 20/05.
            
            int closingDay = account.ClosingDay ?? 1;
            
            DateTime closeDate = new DateTime(year, month, closingDay);
            DateTime startDate = closeDate.AddMonths(-1).AddDays(1);

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.AccountId == accountId && t.Date >= startDate && t.Date <= closeDate)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            var total = transactions.Sum(t => t.Amount); // Assumindo que no cartão só tem Expense positivo

            return new { 
                period = $"{startDate:dd/MM} a {closeDate:dd/MM}",
                dueDate = new DateTime(year, month, account.DueDay ?? 1),
                total,
                status = total > 0 ? "Aberta/Fechada" : "Paga", // Simplificação
                transactions 
            };
        }
    }
}