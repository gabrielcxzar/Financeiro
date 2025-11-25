using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Transactions (Traz todas as transações + dados da Categoria)
        // GET: api/Transactions?month=11&year=2025
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions([FromQuery] int? month, [FromQuery] int? year)
        {
            var query = _context.Transactions
                .Include(t => t.Category)
                .AsQueryable();

            // Se o mês e ano forem informados, filtra. Se não, traz tudo (bom para o histórico).
            if (month.HasValue && year.HasValue)
            {
                // DateTime no banco pode ter hora, então comparamos Month e Year
                query = query.Where(t => t.Date.Month == month && t.Date.Year == year);
            }

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        // POST: api/Transactions
        [HttpPost]
        public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
        {
            if (transaction.Date == default) transaction.Date = DateTime.UtcNow;

            // 1. Salva a transação no histórico
            _context.Transactions.Add(transaction);

            // 2. BUSCA A CONTA VINCULADA PARA ATUALIZAR O SALDO
            var account = await _context.Accounts.FindAsync(transaction.AccountId);
            
            if (account != null)
            {
                // Se for Receita, soma. Se for Despesa, subtrai.
                if (transaction.Type == "Income")
                {
                    account.CurrentBalance += transaction.Amount;
                }
                else 
                {
                    account.CurrentBalance -= transaction.Amount;
                }
                
                // Marca a conta como modificada para o banco salvar
                _context.Entry(account).State = EntityState.Modified;
            }

            // 3. Salva tudo (Transação + Novo Saldo da Conta) numa tacada só
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTransactions", new { id = transaction.Id }, transaction);
        }

        // DELETE: api/Transactions/5
        // DELETE: api/Transactions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            // --- LÓGICA DE ESTORNO ---
            // Se estou apagando, preciso reverter o efeito na conta.
            var account = await _context.Accounts.FindAsync(transaction.AccountId);
            
            if (account != null)
            {
                // Se era Receita e apaguei: O saldo diminui.
                // Se era Despesa e apaguei: O dinheiro volta pra conta (soma).
                if (transaction.Type == "Income")
                {
                    account.CurrentBalance -= transaction.Amount;
                }
                else
                {
                    account.CurrentBalance += transaction.Amount;
                }
                _context.Entry(account).State = EntityState.Modified;
            }
            // --------------------------

            _context.Transactions.Remove(transaction);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}