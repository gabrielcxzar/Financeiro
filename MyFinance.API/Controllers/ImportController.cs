using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Models;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImportController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ImportController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file, [FromQuery] int accountId)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId);
            
            if (account == null)
                return BadRequest("Conta inválida.");

            // Busca as categorias do usuário para tentar adivinhar
            var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync();
            var count = 0;

            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                // Pula a primeira linha (Cabeçalho: Data, Valor, Identificador...)
                await reader.ReadLineAsync();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',');
                    if (values.Length < 4) continue; // Linha inválida

                    // Formato Nubank: Data, Valor, Identificador, Descrição
                    // Ex: 01/01/2025,-20.00,ID123,Compra no débito - Posto
                    
                    // 1. Parse Data
                    if (!DateTime.TryParseExact(values[0], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                        continue;

                    // 2. Parse Valor (Pode vir -20.00 ou 20.00)
                    if (!decimal.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rawAmount))
                        continue;

                    var description = values[3].Replace("\"", ""); // Remove aspas extras se tiver

                    // 3. Verifica se já existe (Evita duplicata)
                    // Critério: Mesma data, mesmo valor, mesma descrição
                    bool exists = await _context.Transactions.AnyAsync(t => 
                        t.UserId == userId && 
                        t.AccountId == accountId &&
                        t.Date.Date == date.Date &&
                        t.Amount == Math.Abs(rawAmount) &&
                        t.Description == description
                    );

                    if (exists) continue;

                    // 4. Adivinha a Categoria
                    var categoryId = GuessCategory(description, categories);
                    var type = rawAmount < 0 ? "Expense" : "Income";

                    // 5. Cria a transação
                    var transaction = new Transaction
                    {
                        UserId = userId,
                        AccountId = accountId,
                        CategoryId = categoryId,
                        Date = date, // O controller de transação ajusta UTC, aqui salvamos direto
                        Description = description,
                        Amount = Math.Abs(rawAmount), // Salvamos sempre positivo, o Type define se entra ou sai
                        Type = type,
                        Paid = true // Importação de extrato passado é sempre Pago
                    };

                    _context.Transactions.Add(transaction);
                    
                    // Atualiza o Saldo da Conta
                    if (account.IsCreditCard)
                    {
                        if (type == "Expense") account.CurrentBalance -= transaction.Amount; // Aumenta dívida
                        else account.CurrentBalance += transaction.Amount; // Pagamento fatura
                    }
                    else
                    {
                        if (type == "Income") account.CurrentBalance += transaction.Amount;
                        else account.CurrentBalance -= transaction.Amount;
                    }
                    _context.Entry(account).State = EntityState.Modified;

                    count++;
                }
                
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = $"{count} transações importadas com sucesso!" });
        }

        // Lógica de Adivinhação (Inteligência Artificial Tabajara)
        private int? GuessCategory(string desc, List<Category> categories)
        {
            desc = desc.ToLower();
            string catName = "Outros";

            if (desc.Contains("posto") || desc.Contains("uber") || desc.Contains("99")) catName = "Transporte";
            else if (desc.Contains("ifood") || desc.Contains("food") || desc.Contains("mercado") || desc.Contains("assai")) catName = "Alimentação";
            else if (desc.Contains("claro") || desc.Contains("tim") || desc.Contains("luz") || desc.Contains("energia")) catName = "Contas Fixas";
            else if (desc.Contains("spotify") || desc.Contains("netflix")) catName = "Lazer";
            else if (desc.Contains("shopee") || desc.Contains("amazon") || desc.Contains("magalu")) catName = "Compras/Shopping";
            else if (desc.Contains("salário") || desc.Contains("pix recebido")) catName = "Salário";
            else if (desc.Contains("pagamento de fatura")) catName = "Pagamento Fatura";

            var cat = categories.FirstOrDefault(c => c.Name.ToLower() == catName.ToLower());
            return cat?.Id ?? categories.FirstOrDefault(c => c.Name == "Outros")?.Id;
        }
    }
}