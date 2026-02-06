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

            var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync();
            var count = 0;

            using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8))
            {
                await reader.ReadLineAsync();

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = ParseCsvLine(line);
                    if (values.Count < 4) continue;

                    if (!DateTime.TryParseExact(values[0], "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                        continue;

                    if (!decimal.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal rawAmount))
                        continue;

                    var description = values[3].Replace("\"", "").Trim();

                    bool exists = await _context.Transactions.AnyAsync(t =>
                        t.UserId == userId &&
                        t.AccountId == accountId &&
                        t.Date.Date == date.Date &&
                        t.Amount == Math.Abs(rawAmount) &&
                        t.Description == description
                    );

                    if (exists) continue;

                    var categoryId = GuessCategory(description, categories);
                    var type = rawAmount < 0 ? "Expense" : "Income";

                    var transaction = new Transaction
                    {
                        UserId = userId,
                        AccountId = accountId,
                        CategoryId = categoryId,
                        Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                        Description = description,
                        Amount = Math.Abs(rawAmount),
                        Type = type,
                        Paid = true
                    };

                    _context.Transactions.Add(transaction);

                    if (account.IsCreditCard)
                    {
                        if (type == "Expense") account.CurrentBalance -= transaction.Amount;
                        else account.CurrentBalance += transaction.Amount;
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

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }

        private int? GuessCategory(string desc, List<Category> categories)
        {
            desc = desc.ToLowerInvariant();
            string catName = "Outros";

            if (desc.Contains("posto") || desc.Contains("uber") || desc.Contains("99")) catName = "Transporte";
            else if (desc.Contains("ifood") || desc.Contains("food") || desc.Contains("mercado") || desc.Contains("assai")) catName = "Alimentação";
            else if (desc.Contains("claro") || desc.Contains("tim") || desc.Contains("luz") || desc.Contains("energia")) catName = "Contas";
            else if (desc.Contains("spotify") || desc.Contains("netflix")) catName = "Lazer";
            else if (desc.Contains("shopee") || desc.Contains("amazon") || desc.Contains("magalu")) catName = "Compras";
            else if (desc.Contains("salário") || desc.Contains("pix recebido")) catName = "Salário";
            else if (desc.Contains("pagamento de fatura")) catName = "Pagamento Fatura";

            var cat = categories.FirstOrDefault(c => c.Name.Equals(catName, StringComparison.OrdinalIgnoreCase));
            return cat?.Id ?? categories.FirstOrDefault(c => c.Name == "Outros")?.Id;
        }
    }
}
