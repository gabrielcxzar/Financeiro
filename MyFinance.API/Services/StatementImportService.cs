using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyFinance.API.Data;
using MyFinance.API.Models;

namespace MyFinance.API.Services;

public sealed record StatementImportRequest(int UserId, int AccountId, string FileName, byte[] Bytes);
public sealed record StatementImportPreview(ImportBatch Batch, decimal CalculatedBalance, bool ExistingFileHash);

public interface IStatementImportService
{
    Task<StatementImportPreview> PreviewAsync(StatementImportRequest request, CancellationToken cancellationToken);
}

public sealed class StatementImportService : IStatementImportService
{
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "dd-MM-yyyy", "d-M-yyyy", "MM/dd/yyyy", "M/d/yyyy"];
    private static readonly Regex InstallmentPattern = new(@"(?:parcela\s*)?(?<current>\d{1,2})\s*/\s*(?<total>\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppDbContext _context;
    private readonly IFinancialSnapshotService _financialSnapshotService;

    public StatementImportService(AppDbContext context, IFinancialSnapshotService financialSnapshotService)
    {
        _context = context;
        _financialSnapshotService = financialSnapshotService;
    }

    public async Task<StatementImportPreview> PreviewAsync(StatementImportRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId && a.UserId == request.UserId, cancellationToken)
            ?? throw new InvalidDataException("Conta invalida.");
        var parsed = extension == ".ofx"
            ? ParseOfx(request.Bytes)
            : await ParseTabularAsync(request.Bytes, extension, cancellationToken);
        if (parsed.Candidates.Count == 0) throw new InvalidDataException("Arquivo sem transacoes validas para importacao.");

        var source = extension.TrimStart('.');
        var fileHash = Convert.ToHexString(SHA256.HashData(request.Bytes)).ToLowerInvariant();
        var existingBatch = await _context.ImportBatches.Include(x => x.Account).Include(x => x.Items).ThenInclude(x => x.Category)
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && x.AccountId == request.AccountId && x.FileHash == fileHash, cancellationToken);
        if (existingBatch is not null)
            return new StatementImportPreview(existingBatch, await CalculateBalanceAsync(existingBatch, request.UserId, cancellationToken), true);

        var categories = await EnsureCategoriesAndRulesAsync(request.UserId, cancellationToken);
        var rules = await _context.CategorizationRules.AsNoTracking().Where(r => r.UserId == request.UserId && r.Active).OrderByDescending(r => r.Priority).ToListAsync(cancellationToken);
        var recurring = await _context.RecurringTransactions.AsNoTracking().Where(r => r.UserId == request.UserId && r.Active).ToListAsync(cancellationToken);
        var accounts = await _context.Accounts.AsNoTracking().Where(a => a.UserId == request.UserId).ToListAsync(cancellationToken);
        var existing = await _context.Transactions.AsNoTracking().Where(t => t.UserId == request.UserId && t.AccountId == request.AccountId).ToListAsync(cancellationToken);
        var priorItems = await _context.ImportedStatementItems.AsNoTracking().Where(i => i.UserId == request.UserId && i.AccountId == request.AccountId).ToListAsync(cancellationToken);
        var batch = new ImportBatch
        {
            UserId = request.UserId, AccountId = request.AccountId, FileName = Path.GetFileName(request.FileName),
            FileType = extension, Source = source, FileHash = fileHash, LedgerBalance = parsed.LedgerBalance,
            PeriodStart = parsed.PeriodStart ?? parsed.Candidates.Min(c => c.Date),
            PeriodEnd = parsed.PeriodEnd ?? parsed.Candidates.Max(c => c.Date)
        };
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in parsed.Candidates)
        {
            var item = new ImportedStatementItem
            {
                UserId = request.UserId, ImportBatch = batch, AccountId = request.AccountId, Source = source,
                ExternalId = string.IsNullOrWhiteSpace(candidate.ExternalId) ? null : candidate.ExternalId,
                PostedAt = candidate.Date, SignedAmount = candidate.SignedAmount, Memo = candidate.Description,
                RawData = candidate.RawData, ResolvedDescription = candidate.Description, ResolvedDate = candidate.Date,
                ResolvedAmount = Math.Abs(candidate.SignedAmount), ResolvedType = candidate.SignedAmount < 0 ? "Expense" : "Income"
            };
            Classify(item, candidate, account, accounts, categories, rules, recurring, existing, priorItems, seenIds);
            batch.Items.Add(item);
        }
        RefreshCounts(batch);
        IDbContextTransaction? transaction = null;
        try
        {
            if (_context.Database.IsRelational()) transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            _context.ImportBatches.Add(batch);
            await _context.SaveChangesAsync(cancellationToken);
            var calculated = await CalculateBalanceAsync(batch, request.UserId, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new StatementImportPreview(batch, calculated, false);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static void ValidateRequest(StatementImportRequest request)
    {
        if (request.Bytes.Length == 0) throw new InvalidDataException("Nenhum arquivo enviado.");
        if (request.Bytes.Length > 10 * 1024 * 1024) throw new InvalidDataException("Arquivo muito grande. Limite de 10MB.");
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx" and not ".ofx") throw new InvalidDataException("Formato nao suportado. Envie um arquivo OFX, CSV ou XLSX.");
    }

    private static void Classify(ImportedStatementItem i, ImportCandidate c, Account account, IReadOnlyCollection<Account> accounts, IReadOnlyCollection<Category> categories, IReadOnlyCollection<CategorizationRule> rules, IReadOnlyCollection<RecurringTransaction> recurring, IReadOnlyCollection<Transaction> existing, IReadOnlyCollection<ImportedStatementItem> prior, HashSet<string> seen)
    {
        var type = i.ResolvedType!;
        var amount = i.ResolvedAmount!.Value;
        if ((!string.IsNullOrWhiteSpace(i.ExternalId) && (!seen.Add(i.ExternalId) || existing.Any(t => t.Source == i.Source && t.ExternalId == i.ExternalId) || prior.Any(p => p.Source == i.Source && p.ExternalId == i.ExternalId && p.Status != ImportItemStatuses.Ignored))) ||
            (string.IsNullOrWhiteSpace(i.ExternalId) && (existing.Any(t => t.Date.Date == i.PostedAt.Date && t.Amount == amount && t.Type == type && t.Description.Equals(i.Memo, StringComparison.OrdinalIgnoreCase) && !t.IsTransfer) || prior.Any(p => p.ExternalId == null && p.PostedAt.Date == i.PostedAt.Date && Math.Abs(p.SignedAmount) == amount && p.Memo.Equals(i.Memo, StringComparison.OrdinalIgnoreCase) && p.Status != ImportItemStatuses.Ignored))))
        { i.Status = ImportItemStatuses.Duplicate; i.Reason = "Identificador externo ou lancamento equivalente ja importado."; return; }
        var installment = InstallmentPattern.Match(i.Memo);
        if (installment.Success && int.TryParse(installment.Groups["current"].Value, out var current) && int.TryParse(installment.Groups["total"].Value, out var total) && total > 1 && current <= total)
        { i.InstallmentNumber = current; i.TotalInstallments = total; i.Status = ImportItemStatuses.NeedsReview; i.Reason = "Parcelamento detectado; confirme antes de importar."; return; }
        if (!account.IsCreditCard && type == "Expense" && LooksLikeInvoice(i.Memo))
        {
            var card = ResolveCard(i.Memo, accounts);
            if (card == null) { i.Status = ImportItemStatuses.NeedsReview; i.Reason = "Pagamento de fatura sem cartao inequivoco."; }
            else { i.TargetAccountId = card.Id; i.ReportingKind = ReportingKinds.InvoicePayment; i.Status = ImportItemStatuses.Ready; i.Reason = "Pagamento de fatura identificado."; }
            return;
        }
        var recur = recurring.FirstOrDefault(r => (!r.AccountId.HasValue || r.AccountId == i.AccountId) && r.Type == type && r.Amount == amount && Math.Abs(r.DayOfMonth - i.PostedAt.Day) <= 3 && Overlap(r.Description, i.Memo));
        if (recur != null) { i.CategoryId = recur.CategoryId; i.RecurringRuleId = recur.Id; i.Status = ImportItemStatuses.Ready; i.Reason = "Recorrencia casada."; return; }
        var rule = rules.FirstOrDefault(r => MatchRule(r, i));
        if (rule != null) { i.CategoryId = rule.CategoryId; i.Status = rule.Confidence >= .9m ? ImportItemStatuses.Ready : ImportItemStatuses.NeedsReview; i.Reason = "Regra de categorizacao aplicada."; return; }
        var direct = !string.IsNullOrWhiteSpace(c.RawCategory) ? categories.FirstOrDefault(x => x.Type == type && x.Name.Equals(c.RawCategory, StringComparison.OrdinalIgnoreCase)) : null;
        if (direct != null) { i.CategoryId = direct.Id; i.Status = ImportItemStatuses.Ready; i.Reason = "Categoria reconhecida no arquivo."; return; }
        i.Status = ImportItemStatuses.NeedsReview; i.Reason = "Sem regra ou recorrencia com confianca suficiente.";
    }

    private static bool MatchRule(CategorizationRule r, ImportedStatementItem i) => r.Type == i.ResolvedType && (!r.AccountId.HasValue || r.AccountId == i.AccountId) && r.TextPattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(p => i.Memo.Contains(p, StringComparison.OrdinalIgnoreCase)) && r.ValueOperator switch { "<" => (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) < r.ValueThreshold, "<=" => (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) <= r.ValueThreshold, ">" => (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) > r.ValueThreshold, ">=" => (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) >= r.ValueThreshold, "=" => (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) == r.ValueThreshold, _ => true };
    private static bool Overlap(string a, string b) => Normalize(a).Split(' ').Where(x => x.Length >= 4).Intersect(Normalize(b).Split(' ')).Any();
    private static string Normalize(string s) => string.Concat(s.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    private static bool LooksLikeInvoice(string s) { var n = Normalize(s); return n.Contains("pagamento fatura") || n.Contains("pagamento de fatura") || n.Contains("pgto fatura") || n.Contains("fatura cartao"); }
    private static Account? ResolveCard(string text, IEnumerable<Account> accounts) { var cards = accounts.Where(a => a.IsCreditCard).ToList(); var named = cards.Where(a => text.Contains(a.Name, StringComparison.OrdinalIgnoreCase)).ToList(); return named.Count == 1 ? named[0] : named.Count == 0 && cards.Count == 1 ? cards[0] : null; }

    private async Task<List<Category>> EnsureCategoriesAndRulesAsync(int userId, CancellationToken ct)
    {
        var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync(ct);
        foreach (var x in new[] { ("Alimentacao", "Expense", "#FF6B6B", "AL"), ("Contas", "Expense", "#7FCDCD", "CT"), ("Salario", "Income", "#4CAF50", "SL"), ("Lazer", "Expense", "#BA68C8", "LZ"), ("Outros", "Expense", "#9E9E9E", "OT") })
            if (!categories.Any(c => c.Name == x.Item1 && c.Type == x.Item2)) { var c = new Category { UserId = userId, Name = x.Item1, Type = x.Item2, Color = x.Item3, Icon = x.Item4 }; categories.Add(c); _context.Categories.Add(c); }
        await _context.SaveChangesAsync(ct);
        if (!await _context.CategorizationRules.AnyAsync(r => r.UserId == userId, ct))
        {
            int C(string n, string t) => categories.First(x => x.Name == n && x.Type == t).Id;
            _context.CategorizationRules.AddRange(new CategorizationRule { UserId = userId, TextPattern = "Posto Shell|Posto Sumare", ValueOperator = "<", ValueThreshold = 30, Type = "Expense", CategoryId = C("Alimentacao", "Expense"), Priority = 100 }, new CategorizationRule { UserId = userId, TextPattern = "Claro", Type = "Expense", CategoryId = C("Contas", "Expense"), Priority = 90 }, new CategorizationRule { UserId = userId, TextPattern = "Receita Federal", Type = "Expense", CategoryId = C("Contas", "Expense"), Priority = 90 }, new CategorizationRule { UserId = userId, TextPattern = "Gnaritas|TechSallus|Felipe e Menezes", Type = "Income", CategoryId = C("Salario", "Income"), Priority = 90 }, new CategorizationRule { UserId = userId, TextPattern = "Socio Bahia|Socio Esquadr", Type = "Expense", CategoryId = C("Lazer", "Expense"), Priority = 90 });
            await _context.SaveChangesAsync(ct);
        }
        return categories;
    }

    private static void RefreshCounts(ImportBatch b) { b.TotalItems = b.Items.Count; b.ImportedCount = b.Items.Count(i => i.Status is ImportItemStatuses.Ready or ImportItemStatuses.Committed); b.DuplicateCount = b.Items.Count(i => i.Status == ImportItemStatuses.Duplicate); b.ReviewCount = b.Items.Count(i => i.Status == ImportItemStatuses.NeedsReview); b.IgnoredCount = b.Items.Count(i => i.Status == ImportItemStatuses.Ignored); }
    private async Task<decimal> CalculateBalanceAsync(ImportBatch b, int userId, CancellationToken ct) { if (!b.AccountId.HasValue) return 0m; var account = b.Account ?? await _context.Accounts.FirstAsync(a => a.Id == b.AccountId.Value, ct); var end = (b.PeriodEnd ?? DateTime.UtcNow).Date.AddDays(1); var tx = await _context.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.AccountId == b.AccountId.Value && t.Paid && t.Date < end).ToListAsync(ct); return account.IsCreditCard ? tx.Where(t => !t.IsTransfer).Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount) : account.InitialBalance + tx.Sum(t => t.Type == "Income" ? t.Amount : -t.Amount); }

    private static ParsedStatement ParseOfx(byte[] bytes)
    {
        var text = Decode(bytes);
        var list = new List<ImportCandidate>();
        foreach (Match m in Regex.Matches(text, @"<STMTTRN>(.*?)(?=<STMTTRN>|</STMTTRN>|</BANKTRANLIST>)", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            if (!TryAmount(Value(m.Value, "TRNAMT"), out var signed) || !TryOfxDate(Value(m.Value, "DTPOSTED"), out var date) || signed == 0) continue;
            list.Add(new ImportCandidate(date, signed, Value(m.Value, "MEMO") ?? Value(m.Value, "NAME") ?? "Lancamento OFX", "", Value(m.Value, "FITID"), m.Value));
        }
        if (list.Count == 0) throw new InvalidDataException("OFX sem transacoes reconheciveis.");
        decimal? balance = null;
        var balances = Regex.Matches(text, @"<BALAMT>\s*([^<\r\n]+)", RegexOptions.IgnoreCase);
        if (balances.Count > 0 && TryAmount(balances[^1].Groups[1].Value, out var parsedBalance)) balance = parsedBalance;
        return new ParsedStatement(list, balance, OptionalOfx(Value(text, "DTSTART")), OptionalOfx(Value(text, "DTEND")));
    }

    private static string Decode(byte[] bytes) { var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 512)); return head.Contains("ENCODING:1252", StringComparison.OrdinalIgnoreCase) ? Encoding.Latin1.GetString(bytes) : Encoding.UTF8.GetString(bytes); }
    private static string? Value(string text, string tag) { var m = Regex.Match(text, $@"<{tag}>\s*([^<\r\n]+)", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value.Trim() : null; }
    private static bool TryOfxDate(string? raw, out DateTime date) { date = default; var d = new string((raw ?? "").TakeWhile(char.IsDigit).ToArray()); if (d.Length < 8 || !DateTime.TryParseExact(d[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false; date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc); return true; }
    private static DateTime? OptionalOfx(string? value) => TryOfxDate(value, out var d) ? d : null;
    private static async Task<ParsedStatement> ParseTabularAsync(byte[] bytes, string ext, CancellationToken ct) { await using var s = new MemoryStream(bytes, false); var rows = ext == ".csv" ? await Csv(s, ct) : Xlsx(s); if (rows.Count < 2) throw new InvalidDataException("Arquivo sem dados suficientes para importacao."); var layout = Layout(rows) ?? throw new InvalidDataException("Nao foi possivel identificar as colunas do arquivo."); var list = rows.Skip(1).Select(r => TryCandidate(r, layout, out var c) ? c : null).Where(c => c != null).Cast<ImportCandidate>().ToList(); return new ParsedStatement(list, null, null, null); }
    private static async Task<List<List<string>>> Csv(Stream s, CancellationToken ct) { using var r = new StreamReader(s, Encoding.UTF8, true, leaveOpen: true); var result = new List<List<string>>(); char? delimiter = null; while (await r.ReadLineAsync(ct) is { } line) { if (string.IsNullOrWhiteSpace(line)) continue; delimiter ??= line.Count(c => c == ';') > line.Count(c => c == ',') ? ';' : ','; result.Add(Split(line, delimiter.Value)); } return result; }
    private static List<List<string>> Xlsx(Stream s) { using var archive = new ZipArchive(s, ZipArchiveMode.Read, true); var sharedEntry = archive.GetEntry("xl/sharedStrings.xml"); var shared = sharedEntry == null ? [] : XDocument.Load(sharedEntry.Open()).Descendants(XName.Get("t", "http://schemas.openxmlformats.org/spreadsheetml/2006/main")).Select(x => x.Value).ToList(); var path = archive.GetEntry("xl/worksheets/sheet1.xml") != null ? "xl/worksheets/sheet1.xml" : archive.Entries.First(e => e.FullName.StartsWith("xl/worksheets/sheet")).FullName; var doc = XDocument.Load(archive.GetEntry(path)!.Open()); XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; return doc.Descendants(n + "row").Select(row => row.Elements(n + "c").Select(c => c.Attribute("t")?.Value == "s" && int.TryParse(c.Element(n + "v")?.Value, out var i) && i < shared.Count ? shared[i] : c.Element(n + "v")?.Value ?? string.Empty).ToList()).Where(r => r.Any(x => x != "")).ToList(); }
    private static List<string> Split(string line, char delimiter) { var result = new List<string>(); var current = new StringBuilder(); var quoted = false; foreach (var c in line) { if (c == '"') quoted = !quoted; else if (c == delimiter && !quoted) { result.Add(current.ToString().Trim()); current.Clear(); } else current.Append(c); } result.Add(current.ToString().Trim()); return result; }
    private static ImportLayout? Layout(List<List<string>> rows) { var h = rows[0].Select(x => Normalize(x).Replace(" ", "")).ToList(); var date = Header(h, "data", "date"); var amount = Header(h, "valor", "amount", "quantia"); var description = Header(h, "descricao", "title", "titulo", "historico", "nome"); var category = Header(h, "categoria", "category"); if (date >= 0 && amount >= 0 && description >= 0) return new ImportLayout(date, amount, description, category); var sample = rows[1]; if (sample.Count >= 4 && TryDate(sample[0], out _) && TryAmount(sample[1], out _)) return new ImportLayout(0, 1, 3, 2); if (sample.Count >= 3 && TryDate(sample[0], out _) && TryAmount(sample[^1], out _)) return new ImportLayout(0, sample.Count - 1, 1, sample.Count > 3 ? 2 : -1); return null; }
    private static int Header(List<string> header, params string[] names) => header.FindIndex(x => names.Any(x.Contains));
    private static bool TryCandidate(List<string> row, ImportLayout layout, out ImportCandidate? candidate) { candidate = null; if (!TryDate(Cell(row, layout.Date), out var date) || !TryAmount(Cell(row, layout.Amount), out var amount) || amount == 0 || string.IsNullOrWhiteSpace(Cell(row, layout.Description))) return false; candidate = new ImportCandidate(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), amount, Clean(Cell(row, layout.Description)), layout.Category >= 0 ? Clean(Cell(row, layout.Category)) : "", null, string.Join(" | ", row)); return true; }
    private static string Cell(List<string> row, int index) => index >= 0 && index < row.Count ? row[index] : "";
    private static string Clean(string? value) => (value ?? "").Replace("\"", "").Trim();
    private static bool TryDate(string? value, out DateTime date) { value = Clean(value); if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial is > 20000 and < 80000) { date = DateTime.FromOADate(serial); return true; } return DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) || DateTime.TryParse(value, new CultureInfo("pt-BR"), DateTimeStyles.None, out date) || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date); }
    private static bool TryAmount(string? value, out decimal amount) { value = Regex.Replace(Clean(value).Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace("BRL", "", StringComparison.OrdinalIgnoreCase), @"\s+", ""); if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out amount)) return true; if (decimal.TryParse(value, NumberStyles.Any, new CultureInfo("pt-BR"), out amount)) return true; var normalized = value; if (normalized.Contains(',') && normalized.Contains('.')) normalized = normalized.LastIndexOf(',') > normalized.LastIndexOf('.') ? normalized.Replace(".", "").Replace(',', '.') : normalized.Replace(",", ""); else if (normalized.Count(c => c == ',') == 1) normalized = normalized.Replace(',', '.'); return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out amount); }

    private sealed record ImportLayout(int Date, int Amount, int Description, int Category);
    private sealed record ImportCandidate(DateTime Date, decimal SignedAmount, string Description, string RawCategory, string? ExternalId, string RawData);
    private sealed record ParsedStatement(List<ImportCandidate> Candidates, decimal? LedgerBalance, DateTime? PeriodStart, DateTime? PeriodEnd);
}
