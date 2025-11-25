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
    public class AccountsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AccountsController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Account>>> GetAccounts()
        {
            var userId = GetUserId();
            return await _context.Accounts.Where(a => a.UserId == userId).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Account>> PostAccount(Account account)
        {
            account.UserId = GetUserId(); // Define o dono
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetAccounts", new { id = account.Id }, account);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutAccount(int id, Account account)
        {
            var userId = GetUserId();
            if (id != account.Id) return BadRequest();

            // Verifica se a conta é do usuário antes de editar
            var existingAccount = await _context.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (existingAccount == null) return NotFound();

            account.UserId = userId; // Garante que não muda o dono
            _context.Entry(account).State = EntityState.Modified;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(int id)
        {
            var userId = GetUserId();
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (account == null) return NotFound();

            var transactions = _context.Transactions.Where(t => t.AccountId == id);
            _context.Transactions.RemoveRange(transactions);

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}