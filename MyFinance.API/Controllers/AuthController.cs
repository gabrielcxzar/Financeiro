using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyFinance.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(UserDto request)
        {
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest("Email já cadastrado.");

            // 1. Cria o Usuário
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(); // Salva para gerar o ID do usuário

            // 2. Cria as Categorias Padrão para este novo usuário
            var defaultCategories = new List<Category>
            {
                new Category { Name = "Alimentação", Type = "Expense", Color = "#FF6B6B", Icon = "🍽️", UserId = user.Id },
                new Category { Name = "Mercado", Type = "Expense", Color = "#FFA07A", Icon = "🛒", UserId = user.Id },
                new Category { Name = "Transporte", Type = "Expense", Color = "#4ECDC4", Icon = "🚗", UserId = user.Id },
                new Category { Name = "Combustível", Type = "Expense", Color = "#45B7D1", Icon = "⛽", UserId = user.Id },
                new Category { Name = "Moradia", Type = "Expense", Color = "#95E1D3", Icon = "🏠", UserId = user.Id },
                new Category { Name = "Contas", Type = "Expense", Color = "#7FCDCD", Icon = "📄", UserId = user.Id },
                new Category { Name = "Saúde", Type = "Expense", Color = "#A8E6CF", Icon = "⚕️", UserId = user.Id },
                new Category { Name = "Farmácia", Type = "Expense", Color = "#88D4AB", Icon = "💊", UserId = user.Id },
                new Category { Name = "Educação", Type = "Expense", Color = "#FFD93D", Icon = "📚", UserId = user.Id },
                new Category { Name = "Lazer", Type = "Expense", Color = "#BA68C8", Icon = "🎮", UserId = user.Id },
                new Category { Name = "Compras", Type = "Expense", Color = "#FFB74D", Icon = "🛍️", UserId = user.Id },
                new Category { Name = "Salário", Type = "Income", Color = "#4CAF50", Icon = "💰", UserId = user.Id },
                new Category { Name = "Investimentos", Type = "Income", Color = "#81C784", Icon = "📈", UserId = user.Id },
                new Category { Name = "Outros", Type = "Expense", Color = "#9E9E9E", Icon = "📌", UserId = user.Id },
                new Category { Name = "Pagamento Fatura", Type = "Expense", Color = "#595959", Icon = "💳", UserId = user.Id },
                new Category { Name = "Pagamento Fatura", Type = "Income", Color = "#595959", Icon = "💳", UserId = user.Id }, // Entrada no crédito
                new Category { Name = "Transferência Interna", Type = "Expense", Color = "#78909C", Icon = "🔄", UserId = user.Id },
                new Category { Name = "Transferência Interna", Type = "Income", Color = "#78909C", Icon = "🔄", UserId = user.Id }
            };

            _context.Categories.AddRange(defaultCategories);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuário e categorias criados com sucesso!" });
        }

        [HttpPost("login")]
        public async Task<ActionResult<string>> Login(UserDto request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return BadRequest("Email ou senha inválidos.");

            string token = CreateToken(user);
            return Ok(new { token, name = user.Name });
        }

        private string CreateToken(User user)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email)
            };

            // Garante que a chave não é nula
            var tokenKey = _configuration.GetSection("AppSettings:Token").Value;
            if (string.IsNullOrEmpty(tokenKey)) throw new Exception("Chave do Token não configurada no appsettings.json");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));
            
            // --- AQUI ESTÁ A MUDANÇA (256) ---
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(30),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    // Classe auxiliar para receber dados do front
    public class UserDto 
    { 
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
}