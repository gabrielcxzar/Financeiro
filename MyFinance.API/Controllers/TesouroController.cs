using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text;

namespace MyFinance.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TesouroController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        private const string CacheKey = "tesouro_latest";
        private const string DatasetUrl = "https://www.tesourotransparente.gov.br/ckan/dataset/7e6d8dbf-9446-478d-b8a0-1d8f75a9f2f4/resource/8a6f1769-0b72-4a59-8d34-5db7e9b8a6f6/download/PrecoTaxaTesouroDireto.csv";

        public TesouroController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        [HttpGet("latest")]
        public async Task<IActionResult> GetLatestRates()
        {
            if (_cache.TryGetValue(CacheKey, out var cached) && cached != null)
            {
                return Ok(cached);
            }

            var client = _httpClientFactory.CreateClient();
            using var stream = await client.GetStreamAsync(DatasetUrl);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);

            string? headerLine = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("CSV vazio.");

            var delimiter = DetectDelimiter(headerLine);
            var headers = SplitLine(headerLine, delimiter)
                .Select(NormalizeHeader)
                .ToList();

            int idxDate = FindHeaderIndex(headers, new[] { "data base", "data" });
            int idxTitle = FindHeaderIndex(headers, new[] { "titulo", "titulo" });
            int idxType = FindHeaderIndex(headers, new[] { "tipo" });
            int idxBuyRate = FindHeaderIndex(headers, new[] { "taxa compra", "taxa_compra" });
            int idxSellRate = FindHeaderIndex(headers, new[] { "taxa venda", "taxa_venda" });
            int idxBuyPrice = FindHeaderIndex(headers, new[] { "pu compra", "preco compra", "preco_compra" });
            int idxSellPrice = FindHeaderIndex(headers, new[] { "pu venda", "preco venda", "preco_venda" });

            if (idxDate < 0 || idxTitle < 0)
                return BadRequest("Cabeçalhos inesperados no CSV do Tesouro.");

            DateTime? latestDate = null;
            var rows = new List<object>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var cols = SplitLine(line, delimiter);
                if (cols.Count <= Math.Max(idxDate, idxTitle)) continue;

                if (!TryParseDate(cols[idxDate], out var date)) continue;

                if (latestDate == null || date > latestDate)
                {
                    latestDate = date;
                    rows.Clear();
                }

                if (date < latestDate) continue;

                var title = cols[idxTitle].Trim();
                var type = idxType >= 0 && idxType < cols.Count ? cols[idxType].Trim() : "";
                var buyRate = idxBuyRate >= 0 && idxBuyRate < cols.Count ? ParseDecimal(cols[idxBuyRate]) : null;
                var sellRate = idxSellRate >= 0 && idxSellRate < cols.Count ? ParseDecimal(cols[idxSellRate]) : null;
                var buyPrice = idxBuyPrice >= 0 && idxBuyPrice < cols.Count ? ParseDecimal(cols[idxBuyPrice]) : null;
                var sellPrice = idxSellPrice >= 0 && idxSellPrice < cols.Count ? ParseDecimal(cols[idxSellPrice]) : null;

                rows.Add(new
                {
                    title,
                    type,
                    buyRate,
                    sellRate,
                    buyPrice,
                    sellPrice
                });
            }

            if (latestDate == null) return BadRequest("Sem dados no CSV do Tesouro.");

            var payload = new
            {
                date = latestDate.Value.ToString("yyyy-MM-dd"),
                items = rows
            };

            _cache.Set(CacheKey, payload, TimeSpan.FromMinutes(60));
            return Ok(payload);
        }

        private static char DetectDelimiter(string line)
        {
            var semis = line.Count(c => c == ';');
            var commas = line.Count(c => c == ',');
            return semis >= commas ? ';' : ',';
        }

        private static List<string> SplitLine(string line, char delimiter)
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

        private static string NormalizeHeader(string header)
        {
            var normalized = header.Trim().ToLowerInvariant();
            normalized = normalized.Replace("_", " ");
            return RemoveDiacritics(normalized);
        }

        private static int FindHeaderIndex(List<string> headers, IEnumerable<string> candidates)
        {
            foreach (var c in candidates)
            {
                var norm = RemoveDiacritics(c.ToLowerInvariant());
                var idx = headers.FindIndex(h => h.Contains(norm));
                if (idx >= 0) return idx;
            }
            return -1;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static bool TryParseDate(string value, out DateTime date)
        {
            value = value.Trim();
            return DateTime.TryParse(value, new CultureInfo("pt-BR"), DateTimeStyles.AssumeUniversal, out date)
                   || DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date)
                   || DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
        }

        private static decimal? ParseDecimal(string value)
        {
            value = value.Trim();
            if (string.IsNullOrEmpty(value)) return null;

            if (decimal.TryParse(value, NumberStyles.Any, new CultureInfo("pt-BR"), out var pt)) return pt;
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv)) return inv;

            value = value.Replace(".", "").Replace(",", ".");
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out inv)) return inv;

            return null;
        }
    }
}
