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

            // --- MÉTODO OTIMIZADO (.NET 8) ---
            // Apaga direto no banco sem ler para a memória (evita erros de nulo)

            await _context.Transactions
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.RecurringTransactions
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Budgets
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            await _context.Accounts
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();

            // Mantemos as categorias, ou apagamos também? 
            // Se quiser apagar categorias personalizadas, descomente:
            /*
            await _context.Categories
                .Where(t => t.UserId == userId)
                .ExecuteDeleteAsync();
            */
            
            // Como apagamos as contas, precisamos resetar o usuário para o estado inicial?
            // Talvez seja legal recriar as categorias padrão aqui se tiver apagado.

            return Ok(new { message = "Todos os dados foram apagados com sucesso." });
        }
    }
}