using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RecurringController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RecurringController(AppDbContext context)
        {
            _context = context;
        }

        // LISTAR FIXAS
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecurringTransaction>>> GetRecurrings()
        {
            return await _context.RecurringTransactions
                .Include(r => r.Category)
                .Include(r => r.Account)
                .Where(r => r.Active)
                .ToListAsync();
        }

        // CRIAR NOVA FIXA
        [HttpPost]
        public async Task<ActionResult<RecurringTransaction>> PostRecurring(RecurringTransaction recurring)
        {
            _context.RecurringTransactions.Add(recurring);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetRecurrings", new { id = recurring.Id }, recurring);
        }

        // EXCLUIR FIXA
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecurring(int id)
        {
            var item = await _context.RecurringTransactions.FindAsync(id);
            if (item == null) return NotFound();
            
            _context.RecurringTransactions.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- MÁGICA: GERAR TRANSAÇÕES DO MÊS ---
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateTransactions([FromQuery] int month, [FromQuery] int year)
        {
            // 1. Pega todas as regras ativas
            var rules = await _context.RecurringTransactions.Where(r => r.Active).ToListAsync();
            int count = 0;

            foreach (var rule in rules)
            {
                // Data ideal da transação naquele mês
                var targetDate = new DateTime(year, month, Math.Min(rule.DayOfMonth, DateTime.DaysInMonth(year, month))); // Trata fevereiro

                // 2. Verifica se já existe uma transação igual neste mês (para não duplicar)
                // Critério: Mesma descrição, mesmo valor, mesma data (dia/mês/ano)
                bool exists = await _context.Transactions.AnyAsync(t => 
                    t.Description == rule.Description && 
                    t.Amount == rule.Amount &&
                    t.Date.Month == month &&
                    t.Date.Year == year
                );

                if (!exists)
                {
                    // 3. Se não existe, cria a transação real
                    var newTrans = new Transaction
                    {
                        Description = rule.Description,
                        Amount = rule.Amount,
                        Type = rule.Type,
                        CategoryId = rule.CategoryId,
                        AccountId = rule.AccountId,
                        Date = targetDate,
                        Paid = false // Fixas nascem como "Pendente" para você confirmar depois
                    };

                    _context.Transactions.Add(newTrans);
                    
                    // Atualiza saldo da conta (Opcional: se quiser que já afete o saldo, descomente abaixo)
                    // Mas como nasce "Pendente", melhor não mexer no saldo ainda.
                    count++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"{count} transações geradas para {month}/{year}" });
        }
    }
}