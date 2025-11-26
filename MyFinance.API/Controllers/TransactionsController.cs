using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // GET: api/Transactions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions([FromQuery] int? month, [FromQuery] int? year)
        {
            var userId = GetUserId();
            var query = _context.Transactions
                .Where(t => t.UserId == userId)
                .Include(t => t.Category)
                .AsQueryable();

            if (month.HasValue && year.HasValue)
            {
                query = query.Where(t => t.Date.Month == month && t.Date.Year == year);
            }

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        // GET: api/Transactions/invoice
        [HttpGet("invoice")]
        public async Task<ActionResult<object>> GetInvoiceSummary([FromQuery] int accountId, [FromQuery] int month, [FromQuery] int year)
        {
            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
            
            if (account == null || !account.IsCreditCard) return BadRequest("Conta inválida ou não é cartão");

            int closingDay = account.ClosingDay ?? 1;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            int safeClosingDay = Math.Min(closingDay, daysInMonth);

            DateTime closeDate = new DateTime(year, month, safeClosingDay, 23, 59, 59, DateTimeKind.Utc);
            DateTime startDate = closeDate.AddMonths(-1).AddDays(1);

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.AccountId == accountId && t.UserId == userId && t.Date >= startDate && t.Date <= closeDate)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            // Expense = Gasto (Positivo na fatura), Income = Pagamento (Negativo na fatura/Abatimento)
            var total = transactions.Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount);

            return new { 
                period = $"{startDate:dd/MM} a {closeDate:dd/MM}",
                total,
                status = total > 0 ? "Aberta" : "Paga",
                transactions 
            };
        }

        // POST: api/Transactions
        [HttpPost]
        public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
        {
            var userId = GetUserId();
            transaction.UserId = userId;

            // Data UTC
            if (transaction.Date == default) transaction.Date = DateTime.UtcNow;
            else transaction.Date = transaction.Date.ToUniversalTime();

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);
            
            int parcelas = transaction.Installments > 1 ? transaction.Installments : 1;
            decimal valorParcela = transaction.Amount; // O front envia o valor da parcela

            // --- GERA ID DO GRUPO DE PARCELAS ---
            string? installmentId = null;
            if (parcelas > 1)
            {
                installmentId = Guid.NewGuid().ToString();
            }

            DateTime dataBase = transaction.Date;

            // Lógica de Data do Cartão
            if (account != null && account.IsCreditCard && account.ClosingDay.HasValue && account.DueDay.HasValue)
            {
                if (dataBase.Day >= account.ClosingDay.Value)
                    dataBase = dataBase.AddMonths(1);
                
                try {
                    dataBase = new DateTime(dataBase.Year, dataBase.Month, account.DueDay.Value, 12, 0, 0, DateTimeKind.Utc);
                } catch {
                    dataBase = new DateTime(dataBase.Year, dataBase.Month, 1, 12, 0, 0, DateTimeKind.Utc).AddMonths(1).AddDays(-1);
                }
            }

            for (int i = 0; i < parcelas; i++)
            {
                var novaTransacao = new Transaction
                {
                    UserId = userId,
                    CategoryId = transaction.CategoryId,
                    AccountId = transaction.AccountId,
                    Type = transaction.Type,
                    Paid = transaction.Paid,
                    Amount = valorParcela,
                    Description = parcelas > 1 ? $"{transaction.Description} ({i + 1}/{parcelas})" : transaction.Description,
                    Date = dataBase.AddMonths(i).ToUniversalTime(),
                    InstallmentId = installmentId // <--- Vínculo
                };

                _context.Transactions.Add(novaTransacao);

                // ATUALIZAÇÃO DE SALDO
                if (account != null)
                {
                    // Se for Cartão:
                    // Expense (Compra) -> Diminui saldo (aumenta dívida negativa)
                    // Income (Pagamento Fatura) -> Aumenta saldo (reduz dívida)
                    
                    // Se for Conta:
                    // Expense -> Diminui saldo
                    // Income -> Aumenta saldo
                    
                    if (novaTransacao.Type == "Income") account.CurrentBalance += novaTransacao.Amount;
                    else account.CurrentBalance -= novaTransacao.Amount;
                    
                    _context.Entry(account).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            return CreatedAtAction("GetTransactions", new { id = transaction.Id }, transaction);
        }

        // PUT: api/Transactions/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, Transaction transaction)
        {
            var userId = GetUserId();
            if (id != transaction.Id) return BadRequest();

            var oldTransaction = await _context.Transactions.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (oldTransaction == null) return NotFound();

            transaction.UserId = userId;
            transaction.Date = transaction.Date.ToUniversalTime();
            // Mantém o InstallmentId original para não quebrar a corrente
            transaction.InstallmentId = oldTransaction.InstallmentId; 

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

            if (account != null)
            {
                // Estorna valor antigo
                if (oldTransaction.Type == "Income") account.CurrentBalance -= oldTransaction.Amount;
                else account.CurrentBalance += oldTransaction.Amount;

                // Aplica valor novo
                if (transaction.Type == "Income") account.CurrentBalance += transaction.Amount;
                else account.CurrentBalance -= transaction.Amount;

                _context.Entry(account).State = EntityState.Modified;
            }

            _context.Entry(transaction).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Transactions/5?deleteAll=true
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id, [FromQuery] bool deleteAll = false)
        {
            var userId = GetUserId();
            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (transaction == null) return NotFound();

            List<Transaction> transactionsToDelete = new List<Transaction>();

            // LÓGICA CORRIGIDA:
            // Se tiver InstallmentId E o usuário pediu pra apagar tudo, apaga o grupo.
            if (deleteAll && !string.IsNullOrEmpty(transaction.InstallmentId))
            {
                transactionsToDelete = await _context.Transactions
                    .Where(t => t.InstallmentId == transaction.InstallmentId && t.UserId == userId)
                    .ToListAsync();
            }
            else
            {
                // Senão, apaga só essa.
                transactionsToDelete.Add(transaction);
            }

            foreach (var t in transactionsToDelete)
            {
                // Estorno do Saldo (Crucial)
                var account = await _context.Accounts.FindAsync(t.AccountId);
                if (account != null)
                {
                    // Se for Receita (Income), tira do saldo.
                    // Se for Despesa (Expense), devolve pro saldo.
                    if (t.Type == "Income") account.CurrentBalance -= t.Amount;
                    else account.CurrentBalance += t.Amount;
                    
                    _context.Entry(account).State = EntityState.Modified;
                }
                
                _context.Transactions.Remove(t);
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/Transactions/transfer
        [HttpPost("transfer")]
        public async Task<IActionResult> Transfer(TransferDto request)
        {
            var userId = GetUserId();
            var dateUtc = request.Date.ToUniversalTime();

            // Lógica: Transferir da Conta X para Cartão Y = Pagar Fatura
            
            var expense = new Transaction
            {
                UserId = userId,
                AccountId = request.FromAccountId,
                Amount = request.Amount,
                Description = $"Transferência para conta/cartão",
                Type = "Expense",
                Date = dateUtc,
                Paid = true
            };

            var income = new Transaction
            {
                UserId = userId,
                AccountId = request.ToAccountId,
                Amount = request.Amount,
                Description = $"Recebido de transferência",
                Type = "Income", // Isso aumenta o saldo do destino (ou abate a fatura)
                Date = dateUtc,
                Paid = true
            };

            var fromAcc = await _context.Accounts.FindAsync(request.FromAccountId);
            var toAcc = await _context.Accounts.FindAsync(request.ToAccountId);

            if (fromAcc == null || toAcc == null || fromAcc.UserId != userId || toAcc.UserId != userId)
                return BadRequest("Contas inválidas");

            // Atualiza Saldos
            fromAcc.CurrentBalance -= request.Amount;
            toAcc.CurrentBalance += request.Amount;

            _context.Transactions.Add(expense);
            _context.Transactions.Add(income);
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Transferência/Pagamento realizado!" });
        }
    }

    public class TransferDto
    {
        public int FromAccountId { get; set; }
        public int ToAccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }
}