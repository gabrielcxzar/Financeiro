using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ImportController : ControllerBase
    {
        private static readonly string[] DateFormats =
        [
            "dd/MM/yyyy",
            "d/M/yyyy",
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "dd-MM-yyyy",
            "d-M-yyyy",
            "MM/dd/yyyy",
            "M/d/yyyy"
        ];

        private readonly AppDbContext _context;

        public ImportController(AppDbContext context)
        {
            _context = context;
        }

        private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost("upload")]
        public async Task<IActionResult> UploadStatement(IFormFile file, [FromQuery] int accountId, CancellationToken cancellationToken)
        {
            if (file == null || file.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not ".csv" and not ".xlsx")
                return BadRequest("Formato nao suportado. Envie um arquivo CSV ou XLSX.");

            var userId = GetUserId();
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);

            if (account == null)
                return BadRequest("Conta invalida.");

            var rows = await ReadRowsAsync(file, extension, cancellationToken);
            if (rows.Count < 2)
                return BadRequest("Arquivo sem dados suficientes para importacao.");

            var layout = DetectLayout(rows);
            if (layout == null)
                return BadRequest("Nao foi possivel identificar as colunas do arquivo do Nubank.");

            var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
            categories = await EnsureImportCategoriesAsync(categories, userId, cancellationToken);

            var importedCount = 0;
            var skippedCount = 0;

            foreach (var row in rows.Skip(1))
            {
                if (!TryBuildTransactionCandidate(row, layout, account, out var candidate))
                {
                    skippedCount++;
                    continue;
                }

                bool exists = await _context.Transactions.AnyAsync(t =>
                    t.UserId == userId &&
                    t.AccountId == accountId &&
                    t.Date.Date == candidate.Date.Date &&
                    t.Amount == candidate.Amount &&
                    t.Type == candidate.Type &&
                    t.Description == candidate.Description,
                    cancellationToken);

                if (exists)
                {
                    skippedCount++;
                    continue;
                }

                var categoryId = ResolveCategoryId(candidate, categories);
                var transaction = new Transaction
                {
                    UserId = userId,
                    AccountId = accountId,
                    CategoryId = categoryId,
                    Date = candidate.Date,
                    Description = candidate.Description,
                    Amount = candidate.Amount,
                    Type = candidate.Type,
                    Paid = true
                };

                _context.Transactions.Add(transaction);
                ApplyBalance(account, transaction);
                importedCount++;
            }

            _context.Entry(account).State = EntityState.Modified;
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = $"{importedCount} transacoes importadas com sucesso.",
                importedCount,
                skippedCount,
                detectedLayout = layout.Kind
            });
        }

        private static async Task<List<List<string>>> ReadRowsAsync(IFormFile file, string extension, CancellationToken cancellationToken)
        {
            await using var stream = file.OpenReadStream();

            return extension switch
            {
                ".csv" => await ReadCsvRowsAsync(stream, cancellationToken),
                ".xlsx" => await ReadXlsxRowsAsync(stream, cancellationToken),
                _ => throw new InvalidOperationException("Formato nao suportado.")
            };
        }

        private static async Task<List<List<string>>> ReadCsvRowsAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            var rows = new List<List<string>>();
            string? line;
            char? delimiter = null;

            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                delimiter ??= DetectDelimiter(line);
                rows.Add(ParseDelimitedLine(line, delimiter.Value));
            }

            return rows;
        }

        private static async Task<List<List<string>>> ReadXlsxRowsAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;

            using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
            var sharedStrings = ReadSharedStrings(archive);
            var sheetPath = GetFirstWorksheetPath(archive);
            var sheetEntry = archive.GetEntry(sheetPath)
                ?? throw new InvalidOperationException("Planilha XLSX invalida: primeira aba nao encontrada.");

            var document = XDocument.Load(sheetEntry.Open());
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var rows = new List<List<string>>();

            foreach (var row in document.Descendants(ns + "row"))
            {
                var values = new List<string>();
                var currentColumn = 0;

                foreach (var cell in row.Elements(ns + "c"))
                {
                    var reference = cell.Attribute("r")?.Value;
                    var targetColumn = GetColumnIndex(reference);
                    while (currentColumn < targetColumn)
                    {
                        values.Add(string.Empty);
                        currentColumn++;
                    }

                    values.Add(ReadCellValue(cell, sharedStrings, ns));
                    currentColumn++;
                }

                if (values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    rows.Add(values);
            }

            return rows;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return [];

            var document = XDocument.Load(entry.Open());
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            return document
                .Descendants(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
                .ToList();
        }

        private static string GetFirstWorksheetPath(ZipArchive archive)
        {
            var workbookEntry = archive.GetEntry("xl/workbook.xml")
                ?? throw new InvalidOperationException("Planilha XLSX invalida: workbook.xml nao encontrado.");

            var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
                ?? throw new InvalidOperationException("Planilha XLSX invalida: workbook rels nao encontrado.");

            XNamespace mainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace pkgNs = "http://schemas.openxmlformats.org/package/2006/relationships";

            var workbook = XDocument.Load(workbookEntry.Open());
            var rels = XDocument.Load(relsEntry.Open());

            var firstSheet = workbook.Descendants(mainNs + "sheet").FirstOrDefault()
                ?? throw new InvalidOperationException("Planilha XLSX invalida: nenhuma aba encontrada.");

            var relationshipId = firstSheet.Attribute(relNs + "id")?.Value
                ?? throw new InvalidOperationException("Planilha XLSX invalida: aba sem relacionamento.");

            var target = rels.Descendants(pkgNs + "Relationship")
                .FirstOrDefault(rel => string.Equals(rel.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal))
                ?.Attribute("Target")?.Value
                ?? throw new InvalidOperationException("Planilha XLSX invalida: destino da aba nao encontrado.");

            return target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                ? target
                : $"xl/{target.TrimStart('/')}";
        }

        private static string ReadCellValue(XElement cell, List<string> sharedStrings, XNamespace ns)
        {
            var type = cell.Attribute("t")?.Value;

            if (type == "inlineStr")
                return string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));

            var rawValue = cell.Element(ns + "v")?.Value ?? string.Empty;

            if (type == "s" && int.TryParse(rawValue, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                return sharedStrings[sharedIndex];

            return rawValue;
        }

        private static int GetColumnIndex(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return 0;

            int column = 0;
            foreach (var character in reference)
            {
                if (!char.IsLetter(character))
                    break;

                column = (column * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
            }

            return Math.Max(column - 1, 0);
        }

        private static char DetectDelimiter(string line)
        {
            var commaCount = line.Count(ch => ch == ',');
            var semicolonCount = line.Count(ch => ch == ';');
            return semicolonCount > commaCount ? ';' : ',';
        }

        private static List<string> ParseDelimitedLine(string line, char delimiter)
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

                if (c == delimiter && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result;
        }

        private static ImportLayout? DetectLayout(List<List<string>> rows)
        {
            var headers = rows[0].Select(NormalizeHeader).ToList();
            var dateIndex = FindHeaderIndex(headers, "data", "date");
            var amountIndex = FindHeaderIndex(headers, "valor", "amount", "quantia");
            var descriptionIndex = FindHeaderIndex(headers, "descricao", "title", "titulo", "historico", "nome");
            var categoryIndex = FindHeaderIndex(headers, "categoria", "category");

            if (dateIndex >= 0 && amountIndex >= 0 && descriptionIndex >= 0)
                return new ImportLayout("header-mapped", dateIndex, amountIndex, descriptionIndex, categoryIndex);

            var sample = rows.Skip(1).FirstOrDefault(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)));
            if (sample == null)
                return null;

            if (sample.Count >= 4 && LooksLikeDate(GetCell(sample, 0)) && LooksLikeAmount(GetCell(sample, 1)))
                return new ImportLayout("legacy-four-columns", 0, 1, 3, 2);

            if (sample.Count >= 3 && LooksLikeDate(GetCell(sample, 0)) && LooksLikeAmount(GetCell(sample, sample.Count - 1)))
                return new ImportLayout("nubank-three-columns", 0, sample.Count - 1, 1, 2 < sample.Count - 1 ? 2 : null);

            if (sample.Count >= 3 && LooksLikeDate(GetCell(sample, 0)) && LooksLikeAmount(GetCell(sample, 1)))
                return new ImportLayout("date-amount-description", 0, 1, 2, 3 < sample.Count ? 3 : null);

            return null;
        }

        private static int FindHeaderIndex(List<string> headers, params string[] acceptedNames)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (acceptedNames.Any(name => headers[i].Contains(name, StringComparison.Ordinal)))
                    return i;
            }

            return -1;
        }

        private static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(character))
                    builder.Append(character);
            }

            return builder.ToString();
        }

        private static string GetCell(List<string> row, int index) => index >= 0 && index < row.Count ? row[index] : string.Empty;

        private static bool TryBuildTransactionCandidate(List<string> row, ImportLayout layout, Account account, out ImportCandidate candidate)
        {
            candidate = default!;

            var rawDate = GetCell(row, layout.DateIndex);
            var rawAmount = GetCell(row, layout.AmountIndex);
            var rawDescription = GetCell(row, layout.DescriptionIndex);
            var rawCategory = layout.CategoryIndex.HasValue ? GetCell(row, layout.CategoryIndex.Value) : string.Empty;

            if (!TryParseDate(rawDate, out var date))
                return false;

            if (!TryParseAmount(rawAmount, out var signedAmount))
                return false;

            var description = CleanValue(rawDescription);
            if (string.IsNullOrWhiteSpace(description))
                return false;

            var type = signedAmount < 0 ? "Expense" : "Income";
            var amount = Math.Abs(signedAmount);

            if (amount == 0)
                return false;

            candidate = new ImportCandidate(
                DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
                amount,
                type,
                description,
                CleanValue(rawCategory),
                account.IsCreditCard);

            return true;
        }

        private static bool TryParseDate(string? rawValue, out DateTime date)
        {
            rawValue = CleanValue(rawValue);

            if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var excelSerial) && excelSerial > 20000 && excelSerial < 80000)
            {
                date = DateTime.FromOADate(excelSerial);
                return true;
            }

            return DateTime.TryParseExact(rawValue, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
                || DateTime.TryParse(rawValue, new CultureInfo("pt-BR"), DateTimeStyles.None, out date)
                || DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static bool TryParseAmount(string? rawValue, out decimal amount)
        {
            rawValue = CleanValue(rawValue);
            rawValue = rawValue.Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase);
            rawValue = rawValue.Replace("BRL", string.Empty, StringComparison.OrdinalIgnoreCase);
            rawValue = Regex.Replace(rawValue, @"\s+", string.Empty);

            if (decimal.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
                return true;

            if (decimal.TryParse(rawValue, NumberStyles.Any, new CultureInfo("pt-BR"), out amount))
                return true;

            var normalized = rawValue;
            if (normalized.Contains(',') && normalized.Contains('.'))
            {
                if (normalized.LastIndexOf(',') > normalized.LastIndexOf('.'))
                    normalized = normalized.Replace(".", string.Empty).Replace(',', '.');
                else
                    normalized = normalized.Replace(",", string.Empty);
            }
            else if (normalized.Count(ch => ch == ',') == 1 && normalized.Count(ch => ch == '.') == 0)
            {
                normalized = normalized.Replace(',', '.');
            }

            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        private static bool LooksLikeDate(string? rawValue) => TryParseDate(rawValue, out _);

        private static bool LooksLikeAmount(string? rawValue) => TryParseAmount(rawValue, out _);

        private static string CleanValue(string? value)
        {
            return (value ?? string.Empty).Replace("\"", string.Empty).Trim();
        }

        private async Task<List<Category>> EnsureImportCategoriesAsync(List<Category> categories, int userId, CancellationToken cancellationToken)
        {
            var changed = false;

            if (!categories.Any(c => c.UserId == userId && c.Type == "Expense" && c.Name.Equals("Importacao", StringComparison.OrdinalIgnoreCase)))
            {
                categories.Add(new Category
                {
                    Name = "Importacao",
                    Type = "Expense",
                    Color = "#595959",
                    Icon = "IM",
                    UserId = userId
                });
                changed = true;
            }

            if (!categories.Any(c => c.UserId == userId && c.Type == "Income" && c.Name.Equals("Importacao", StringComparison.OrdinalIgnoreCase)))
            {
                categories.Add(new Category
                {
                    Name = "Importacao",
                    Type = "Income",
                    Color = "#595959",
                    Icon = "IM",
                    UserId = userId
                });
                changed = true;
            }

            if (changed)
            {
                _context.Categories.AddRange(categories.Where(c => c.Id == 0));
                await _context.SaveChangesAsync(cancellationToken);
            }

            return categories;
        }

        private int? ResolveCategoryId(ImportCandidate candidate, List<Category> categories)
        {
            if (!string.IsNullOrWhiteSpace(candidate.RawCategory))
            {
                var directMatch = categories.FirstOrDefault(category =>
                    category.Type == candidate.Type &&
                    category.Name.Equals(candidate.RawCategory, StringComparison.OrdinalIgnoreCase));

                if (directMatch != null)
                    return directMatch.Id;
            }

            var guessed = GuessCategory(candidate.Description, candidate.Type, categories);
            if (guessed.HasValue)
                return guessed.Value;

            return categories
                .FirstOrDefault(category => category.Type == candidate.Type && category.Name.Equals("Importacao", StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        private static void ApplyBalance(Account account, Transaction transaction)
        {
            if (account.IsCreditCard)
            {
                if (transaction.Type == "Expense") account.CurrentBalance -= transaction.Amount;
                else account.CurrentBalance += transaction.Amount;
                return;
            }

            if (transaction.Type == "Income") account.CurrentBalance += transaction.Amount;
            else account.CurrentBalance -= transaction.Amount;
        }

        private int? GuessCategory(string description, string type, List<Category> categories)
        {
            var desc = description.ToLowerInvariant();
            string categoryName;

            if (desc.Contains("posto") || desc.Contains("uber") || desc.Contains("99"))
                categoryName = categories.Any(c => c.Name.Equals("Combustivel", StringComparison.OrdinalIgnoreCase)) ? "Combustivel" : "Transporte";
            else if (desc.Contains("ifood") || desc.Contains("food") || desc.Contains("mercado") || desc.Contains("assai"))
                categoryName = categories.Any(c => c.Name.Equals("Mercado", StringComparison.OrdinalIgnoreCase)) ? "Mercado" : "Alimentacao";
            else if (desc.Contains("claro") || desc.Contains("tim") || desc.Contains("energia") || desc.Contains("enel") || desc.Contains("neoenergia"))
                categoryName = "Contas";
            else if (desc.Contains("spotify") || desc.Contains("netflix") || desc.Contains("cinema"))
                categoryName = "Lazer";
            else if (desc.Contains("shopee") || desc.Contains("amazon") || desc.Contains("magalu") || desc.Contains("mercadolivre"))
                categoryName = "Compras";
            else if (desc.Contains("salario") || desc.Contains("salário") || desc.Contains("pix recebido"))
                categoryName = "Salario";
            else if (desc.Contains("pagamento de fatura") || desc.Contains("pagamento fatura"))
                categoryName = "Pagamento Fatura";
            else
                categoryName = "Importacao";

            var directMatch = categories.FirstOrDefault(c =>
                c.Type == type &&
                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

            return directMatch?.Id
                ?? categories.FirstOrDefault(c => c.Type == type && c.Name.Equals("Importacao", StringComparison.OrdinalIgnoreCase))?.Id;
        }

        private sealed record ImportLayout(string Kind, int DateIndex, int AmountIndex, int DescriptionIndex, int? CategoryIndex);

        private sealed record ImportCandidate(DateTime Date, decimal Amount, string Type, string Description, string RawCategory, bool IsCreditCard);
    }
}
