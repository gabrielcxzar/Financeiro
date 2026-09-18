using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using System.Security.Claims;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("me")]
        public async Task<ActionResult<object>> GetMe()
        {
            var userId = GetUserId();
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return new { user.Name, user.Email };
        }

        [HttpPost("wipe-data")]
        public async Task<IActionResult> WipeData()
        {
            var userId = GetUserId();

            if (!_context.Database.IsRelational())
            {
                _context.Transactions.RemoveRange(await _context.Transactions.Where(t => t.UserId == userId).ToListAsync());
                _context.RecurringTransactions.RemoveRange(await _context.RecurringTransactions.Where(t => t.UserId == userId).ToListAsync());
                _context.Budgets.RemoveRange(await _context.Budgets.Where(t => t.UserId == userId).ToListAsync());
                _context.GmailImportRules.RemoveRange(await _context.GmailImportRules.Where(t => t.UserId == userId).ToListAsync());
                _context.Accounts.RemoveRange(await _context.Accounts.Where(t => t.UserId == userId).ToListAsync());
                _context.Categories.RemoveRange(await _context.Categories.Where(t => t.UserId == userId).ToListAsync());
                await _context.SaveChangesAsync();
                _context.Categories.AddRange(DefaultCategories.Create(userId));
                await _context.SaveChangesAsync();
                return Ok(new { message = "Dados apagados e categorias resetadas para o padrao." });
            }

            await _context.Transactions
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.RecurringTransactions
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Budgets
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.GmailImportRules
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Accounts
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Categories
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            var defaults = DefaultCategories.Create(userId);
            _context.Categories.AddRange(defaults);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Dados apagados e categorias resetadas para o padr�o." });
        }
    }
}
