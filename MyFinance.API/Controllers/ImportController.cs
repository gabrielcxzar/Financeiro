using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using MyFinance.API.Services;
using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MyFinance.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ImportController : ControllerBase
{
    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "dd-MM-yyyy", "d-M-yyyy", "MM/dd/yyyy", "M/d/yyyy"];
    private static readonly Regex InstallmentPattern = new(@"(?:parcela\s*)?(?<current>\d{1,2})\s*/\s*(?<total>\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppDbContext _context;
    private readonly IFinancialSnapshotService _financialSnapshotService;
    public ImportController(AppDbContext context, IFinancialSnapshotService financialSnapshotService) { _context = context; _financialSnapshotService = financialSnapshotService; }
    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadStatement(IFormFile file, [FromQuery] int accountId, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("Nenhum arquivo enviado.");
        if (accountId <= 0) return BadRequest("Conta invalida.");
        if (file.Length > 10 * 1024 * 1024) return BadRequest("Arquivo muito grande. Limite de 10MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".csv" and not ".xlsx" and not ".ofx") return BadRequest("Formato nao suportado. Envie um arquivo OFX, CSV ou XLSX.");
        var userId = GetUserId();
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);
        if (account == null) return BadRequest("Conta invalida.");
        byte[] bytes;
        await using (var input = file.OpenReadStream()) await using (var memory = new MemoryStream()) { await input.CopyToAsync(memory, cancellationToken); bytes = memory.ToArray(); }
        ParsedStatement parsed;
        try { parsed = extension == ".ofx" ? ParseOfx(bytes) : await ParseTabularAsync(bytes, extension, account, cancellationToken); }
        catch (InvalidDataException e) { return BadRequest(e.Message); }
        if (parsed.Candidates.Count == 0) return BadRequest("Arquivo sem transacoes validas para importacao.");

        var categories = await EnsureCategoriesAndRulesAsync(userId, cancellationToken);
        var rules = await _context.CategorizationRules.AsNoTracking().Where(r => r.UserId == userId && r.Active).OrderByDescending(r => r.Priority).ToListAsync(cancellationToken);
        var recurring = await _context.RecurringTransactions.AsNoTracking().Where(r => r.UserId == userId && r.Active).ToListAsync(cancellationToken);
        var accounts = await _context.Accounts.AsNoTracking().Where(a => a.UserId == userId).ToListAsync(cancellationToken);
        var existing = await _context.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.AccountId == accountId).ToListAsync(cancellationToken);
        var priorItems = await _context.ImportedStatementItems.AsNoTracking().Where(i => i.UserId == userId && i.AccountId == accountId).ToListAsync(cancellationToken);
        var source = extension.TrimStart('.');
        var batch = new ImportBatch { UserId = userId, AccountId = accountId, FileName = Path.GetFileName(file.FileName), FileType = extension, Source = source, FileHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), LedgerBalance = parsed.LedgerBalance, PeriodStart = parsed.PeriodStart ?? parsed.Candidates.Min(c => c.Date), PeriodEnd = parsed.PeriodEnd ?? parsed.Candidates.Max(c => c.Date) };
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in parsed.Candidates)
        {
            var item = new ImportedStatementItem { UserId = userId, ImportBatch = batch, AccountId = accountId, Source = source, ExternalId = string.IsNullOrWhiteSpace(candidate.ExternalId) ? null : candidate.ExternalId, PostedAt = candidate.Date, SignedAmount = candidate.SignedAmount, Memo = candidate.Description, RawData = candidate.RawData, ResolvedDescription = candidate.Description, ResolvedDate = candidate.Date, ResolvedAmount = Math.Abs(candidate.SignedAmount), ResolvedType = candidate.SignedAmount < 0 ? "Expense" : "Income" };
            Classify(item, candidate, account, accounts, categories, rules, recurring, existing, priorItems, seenIds);
            batch.Items.Add(item);
        }
        RefreshCounts(batch); _context.ImportBatches.Add(batch); await _context.SaveChangesAsync(cancellationToken);
        var calculatedBalance = await CalculateBalanceAsync(batch, userId, cancellationToken);
        return Ok(new { message = "Prévia gerada. Confirme os itens seguros para lançar.", batch = Summary(batch), importedCount = batch.ImportedCount, skippedCount = batch.DuplicateCount, manualReviewCount = batch.ReviewCount, items = batch.Items.Select(ItemResponse), reconciliation = new { ledgerBalance = batch.LedgerBalance, calculatedBalance, difference = batch.LedgerBalance.HasValue ? batch.LedgerBalance.Value - calculatedBalance : (decimal?)null } });
    }

    [HttpGet("batches")]
    public async Task<IActionResult> GetBatches(CancellationToken ct) => Ok((await _context.ImportBatches.AsNoTracking().Include(b => b.Account).Where(b => b.UserId == GetUserId()).OrderByDescending(b => b.CreatedAt).ToListAsync(ct)).Select(Summary));

    [HttpGet("batches/{id:int}")]
    public async Task<IActionResult> GetBatch(int id, CancellationToken ct) { var value = await BuildBatchResponseAsync(id, GetUserId(), ct); return value == null ? NotFound() : Ok(value); }

    [HttpPost("batches/{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        var userId = GetUserId(); var batch = await _context.ImportBatches.Include(b => b.Account).Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, ct);
        if (batch == null) return NotFound(); if (batch.Status == ImportBatchStatuses.Confirmed) return Conflict("Este lote ja foi confirmado.");
        var created = 0;
        foreach (var item in batch.Items.Where(i => i.Status == ImportItemStatuses.Ready))
        {
            if (await DuplicateOnConfirm(item, ct)) { item.Status = ImportItemStatuses.Duplicate; continue; }
            var tx = item.ReportingKind == ReportingKinds.InvoicePayment ? await CreateInvoicePayment(batch, item, userId, ct) : CreateImported(batch, item, userId);
            item.Transaction = tx; item.Status = ImportItemStatuses.Committed; created++;
        }
        batch.Status = ImportBatchStatuses.Confirmed; batch.ConfirmedAt = DateTime.UtcNow; RefreshCounts(batch); await _context.SaveChangesAsync(ct); await _financialSnapshotService.RecalculateAccountBalancesAsync(userId, ct);
        return Ok(new { message = $"{created} itens confirmados.", createdCount = created, batch = await BuildBatchResponseAsync(id, userId, ct) });
    }

    [HttpPost("items/{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id, [FromBody] ResolveImportItemRequest request, CancellationToken ct)
    {
        var userId = GetUserId(); var item = await _context.ImportedStatementItems.Include(i => i.ImportBatch).FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId, ct);
        if (item == null) return NotFound(); if (item.ImportBatch?.Status == ImportBatchStatuses.Confirmed || item.Status == ImportItemStatuses.Committed) return Conflict("O item pertence a um lote ja confirmado.");
        var accountId = request.AccountId ?? item.AccountId; if (!await _context.Accounts.AnyAsync(a => a.Id == accountId && a.UserId == userId, ct)) return BadRequest("Conta invalida.");
        var type = request.Type ?? (item.SignedAmount < 0 ? "Expense" : "Income"); if (type is not "Expense" and not "Income") return BadRequest("Tipo invalido.");
        if (request.Amount is <= 0) return BadRequest("Valor deve ser maior que zero.");
        var kind = request.ReportingKind ?? item.ReportingKind; if (!ReportingKinds.IsValid(kind)) return BadRequest("Classificacao de relatorio invalida.");
        if (kind == ReportingKinds.TechnicalAdjustment && string.IsNullOrWhiteSpace(request.Justification)) return BadRequest("Informe uma justificativa para o ajuste tecnico.");
        if (request.CategoryId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == request.CategoryId && c.UserId == userId && c.Type == type, ct)) return BadRequest("Categoria invalida para o tipo selecionado.");
        if (kind == ReportingKinds.InvoicePayment && (!request.TargetAccountId.HasValue || !await _context.Accounts.AnyAsync(a => a.Id == request.TargetAccountId && a.UserId == userId && a.IsCreditCard, ct))) return BadRequest("Selecione o cartao pago.");
        item.AccountId = accountId; item.CategoryId = request.CategoryId ?? item.CategoryId; item.TargetAccountId = request.TargetAccountId ?? item.TargetAccountId; item.ReportingKind = kind; item.ResolvedDescription = string.IsNullOrWhiteSpace(request.Description) ? item.Memo : request.Description.Trim(); item.ResolvedDate = request.Date?.ToUniversalTime() ?? item.PostedAt; item.ResolvedAmount = request.Amount ?? Math.Abs(item.SignedAmount); item.ResolvedType = type; item.InstallmentNumber = request.InstallmentNumber; item.TotalInstallments = request.TotalInstallments; item.Status = ImportItemStatuses.Ready; item.Reason = request.Justification ?? "Revisado manualmente."; item.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct); await RefreshCountsStored(item.ImportBatchId, ct); return Ok(ItemResponse(item));
    }

    [HttpPost("items/{id:int}/ignore")]
    public async Task<IActionResult> Ignore(int id, CancellationToken ct)
    {
        var item = await _context.ImportedStatementItems.Include(i => i.ImportBatch).FirstOrDefaultAsync(i => i.Id == id && i.UserId == GetUserId(), ct); if (item == null) return NotFound(); if (item.ImportBatch?.Status == ImportBatchStatuses.Confirmed || item.Status == ImportItemStatuses.Committed) return Conflict("O item pertence a um lote ja confirmado."); item.Status = ImportItemStatuses.Ignored; item.Reason = "Ignorado pelo usuario."; await _context.SaveChangesAsync(ct); await RefreshCountsStored(item.ImportBatchId, ct); return Ok(ItemResponse(item));
    }

    private static void Classify(ImportedStatementItem i, ImportCandidate c, Account account, IReadOnlyCollection<Account> accounts, IReadOnlyCollection<Category> categories, IReadOnlyCollection<CategorizationRule> rules, IReadOnlyCollection<RecurringTransaction> recurring, IReadOnlyCollection<Transaction> existing, IReadOnlyCollection<ImportedStatementItem> prior, HashSet<string> seen)
    {
        var type = i.ResolvedType!; var amount = i.ResolvedAmount!.Value;
        if ((!string.IsNullOrWhiteSpace(i.ExternalId) && (!seen.Add(i.ExternalId) || existing.Any(t => t.Source == i.Source && t.ExternalId == i.ExternalId) || prior.Any(p => p.Source == i.Source && p.ExternalId == i.ExternalId && p.Status != ImportItemStatuses.Ignored))) || (string.IsNullOrWhiteSpace(i.ExternalId) && (existing.Any(t => t.Date.Date == i.PostedAt.Date && t.Amount == amount && t.Type == type && t.Description.Equals(i.Memo, StringComparison.OrdinalIgnoreCase) && !t.IsTransfer) || prior.Any(p => p.ExternalId == null && p.PostedAt.Date == i.PostedAt.Date && Math.Abs(p.SignedAmount) == amount && p.Memo.Equals(i.Memo, StringComparison.OrdinalIgnoreCase) && p.Status != ImportItemStatuses.Ignored)))) { i.Status = ImportItemStatuses.Duplicate; i.Reason = "Identificador externo ou lancamento equivalente ja importado."; return; }
        var installment = InstallmentPattern.Match(i.Memo); if (installment.Success && int.TryParse(installment.Groups["current"].Value, out var current) && int.TryParse(installment.Groups["total"].Value, out var total) && total > 1 && current <= total) { i.InstallmentNumber = current; i.TotalInstallments = total; i.Status = ImportItemStatuses.NeedsReview; i.Reason = "Parcelamento detectado; confirme antes de importar."; return; }
        if (!account.IsCreditCard && type == "Expense" && LooksLikeInvoice(i.Memo)) { var card = ResolveCard(i.Memo, accounts); if (card == null) { i.Status = ImportItemStatuses.NeedsReview; i.Reason = "Pagamento de fatura sem cartao inequivoco."; } else { i.TargetAccountId = card.Id; i.ReportingKind = ReportingKinds.InvoicePayment; i.Status = ImportItemStatuses.Ready; i.Reason = "Pagamento de fatura identificado."; } return; }
        var recur = recurring.FirstOrDefault(r => (!r.AccountId.HasValue || r.AccountId == i.AccountId) && r.Type == type && r.Amount == amount && Math.Abs(r.DayOfMonth - i.PostedAt.Day) <= 3 && Overlap(r.Description, i.Memo)); if (recur != null) { i.CategoryId = recur.CategoryId; i.RecurringRuleId = recur.Id; i.Status = ImportItemStatuses.Ready; i.Reason = "Recorrencia casada."; return; }
        var rule = rules.FirstOrDefault(r => MatchRule(r, i)); if (rule != null) { i.CategoryId = rule.CategoryId; i.Status = rule.Confidence >= .9m ? ImportItemStatuses.Ready : ImportItemStatuses.NeedsReview; i.Reason = "Regra de categorizacao aplicada."; return; }
        var direct = !string.IsNullOrWhiteSpace(c.RawCategory) ? categories.FirstOrDefault(x => x.Type == type && x.Name.Equals(c.RawCategory, StringComparison.OrdinalIgnoreCase)) : null; if (direct != null) { i.CategoryId = direct.Id; i.Status = ImportItemStatuses.Ready; i.Reason = "Categoria reconhecida no arquivo."; return; }
        i.Status = ImportItemStatuses.NeedsReview; i.Reason = "Sem regra ou recorrencia com confianca suficiente.";
    }

    private static bool MatchRule(CategorizationRule r, ImportedStatementItem i) { if (r.Type != i.ResolvedType || r.AccountId.HasValue && r.AccountId != i.AccountId || !r.TextPattern.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(p => i.Memo.Contains(p, StringComparison.OrdinalIgnoreCase))) return false; var a = i.ResolvedAmount ?? Math.Abs(i.SignedAmount); return r.ValueOperator switch { "<" => a < r.ValueThreshold, "<=" => a <= r.ValueThreshold, ">" => a > r.ValueThreshold, ">=" => a >= r.ValueThreshold, "=" => a == r.ValueThreshold, _ => true }; }
    private static bool Overlap(string a, string b) => Normalize(a).Split(' ').Where(x => x.Length >= 4).Intersect(Normalize(b).Split(' ')).Any();
    private static string Normalize(string s) => string.Concat(s.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    private static bool LooksLikeInvoice(string s) { var n = Normalize(s); return n.Contains("pagamento fatura") || n.Contains("pagamento de fatura") || n.Contains("pgto fatura") || n.Contains("fatura cartao"); }
    private static Account? ResolveCard(string text, IEnumerable<Account> accounts) { var cards = accounts.Where(a => a.IsCreditCard).ToList(); var named = cards.Where(a => text.Contains(a.Name, StringComparison.OrdinalIgnoreCase)).ToList(); return named.Count == 1 ? named[0] : named.Count == 0 && cards.Count == 1 ? cards[0] : null; }

    private Transaction CreateImported(ImportBatch b, ImportedStatementItem i, int userId) {
        var account = _context.Accounts.Local.FirstOrDefault(a => a.Id == i.AccountId) ?? _context.Accounts.First(a => a.Id == i.AccountId);
        var total = Math.Max(i.TotalInstallments ?? 1, 1); var current = Math.Clamp(i.InstallmentNumber ?? 1, 1, total); var totalCents = decimal.ToInt64(decimal.Round((i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) * 100m, 0, MidpointRounding.AwayFromZero)); var remaining = total - current + 1; var baseCents = totalCents / remaining; var remainder = totalCents % remaining; var date = i.ResolvedDate ?? i.PostedAt; var series = total > 1 ? Guid.NewGuid().ToString("N") : null; Transaction? first = null;
        for (var index = 0; index < remaining; index++) {
            var number = current + index; var tx = new Transaction { UserId = userId, AccountId = i.AccountId, CategoryId = i.CategoryId, Date = date, Description = total > 1 ? $"{StripInstallment(i.ResolvedDescription ?? i.Memo)} ({number}/{total})" : i.ResolvedDescription ?? i.Memo, Amount = (baseCents + (index < remainder ? 1 : 0)) / 100m, Type = i.ResolvedType ?? "Expense", Paid = true, RecurringRuleId = i.RecurringRuleId, Source = b.Source, SourceFile = b.FileName, ExternalId = index == 0 ? i.ExternalId : null, RawMemo = i.Memo, ImportedAt = DateTime.UtcNow, ImportBatchId = b.Id, ReportingKind = i.ReportingKind, ExcludeFromReports = i.ReportingKind != ReportingKinds.Normal, InstallmentId = series }; _context.Transactions.Add(tx); first ??= tx; if (number < total) date = NextInstallmentDate(account, date); }
        return first!;
    }
    private DateTime NextInstallmentDate(Account account, DateTime current) { if (!account.IsCreditCard) return current.AddMonths(1); foreach (var offset in new[] { -1, 0, 1 }) { var reference = new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(offset); var window = _financialSnapshotService.GetInvoiceWindow(account, reference.Month, reference.Year); if (current >= window.StartDate && current < window.CloseDate) { var next = reference.AddMonths(1); return _financialSnapshotService.GetInvoiceWindow(account, next.Month, next.Year).StartDate.Add(current.TimeOfDay == TimeSpan.Zero ? TimeSpan.FromHours(12) : current.TimeOfDay); } } return current.AddMonths(1); }
    private static string StripInstallment(string value) => InstallmentPattern.Replace(value, string.Empty).Trim(' ', '-', '(', ')');
    private async Task<Transaction> CreateInvoicePayment(ImportBatch b, ImportedStatementItem i, int userId, CancellationToken ct) { if (!i.TargetAccountId.HasValue) throw new InvalidOperationException("Pagamento sem cartao."); var group = Guid.NewGuid().ToString("N"); var tx = CreateImported(b, i, userId); tx.Type = "Expense"; tx.IsTransfer = true; tx.TransferGroupId = group; tx.ReportingKind = ReportingKinds.InvoicePayment; tx.ExcludeFromReports = true; var card = new Transaction { UserId = userId, AccountId = i.TargetAccountId.Value, Date = tx.Date, Description = "Pagamento de fatura importado", Amount = tx.Amount, Type = "Income", Paid = true, IsTransfer = true, TransferGroupId = group, Source = b.Source, SourceFile = b.FileName, RawMemo = i.Memo, ImportedAt = DateTime.UtcNow, ImportBatchId = b.Id, ReportingKind = ReportingKinds.InvoicePayment, ExcludeFromReports = true }; _context.Transactions.Add(card); await Task.CompletedTask; return tx; }
    private async Task<bool> DuplicateOnConfirm(ImportedStatementItem i, CancellationToken ct) { if (!string.IsNullOrWhiteSpace(i.ExternalId)) return await _context.Transactions.AnyAsync(t => t.UserId == i.UserId && t.AccountId == i.AccountId && t.Source == i.Source && t.ExternalId == i.ExternalId, ct); var d = i.ResolvedDate ?? i.PostedAt; return await _context.Transactions.AnyAsync(t => t.UserId == i.UserId && t.AccountId == i.AccountId && t.Date.Date == d.Date && t.Amount == (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) && t.Type == (i.ResolvedType ?? "Expense") && t.Description == (i.ResolvedDescription ?? i.Memo) && !t.IsTransfer, ct); }

    private async Task<List<Category>> EnsureCategoriesAndRulesAsync(int userId, CancellationToken ct) { var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync(ct); foreach (var x in new[] { ("Alimentacao", "Expense", "#FF6B6B", "AL"), ("Contas", "Expense", "#7FCDCD", "CT"), ("Salario", "Income", "#4CAF50", "SL"), ("Lazer", "Expense", "#BA68C8", "LZ"), ("Outros", "Expense", "#9E9E9E", "OT") }) if (!categories.Any(c => c.Name == x.Item1 && c.Type == x.Item2)) { var c = new Category { UserId = userId, Name = x.Item1, Type = x.Item2, Color = x.Item3, Icon = x.Item4 }; categories.Add(c); _context.Categories.Add(c); } await _context.SaveChangesAsync(ct); if (!await _context.CategorizationRules.AnyAsync(r => r.UserId == userId, ct)) { int C(string n, string t) => categories.First(x => x.Name == n && x.Type == t).Id; _context.CategorizationRules.AddRange(new CategorizationRule { UserId = userId, TextPattern = "Posto Shell|Posto Sumare", ValueOperator = "<", ValueThreshold = 30, Type = "Expense", CategoryId = C("Alimentacao", "Expense"), Priority = 100 }, new CategorizationRule { UserId = userId, TextPattern = "Claro", Type = "Expense", CategoryId = C("Contas", "Expense"), Priority = 90 }, new CategorizationRule { UserId = userId, TextPattern = "Receita Federal", Type = "Expense", CategoryId = C("Contas", "Expense"), Priority = 90 }, new CategorizationRule { UserId = userId, TextPattern = "Gnaritas|TechSallus|Felipe e Menezes", Type = "Income", CategoryId = C("Salario", "Income"), Priority = 90 }, new CategorizationRule { UserId = userId, TextPattern = "Socio Bahia|Socio Esquadr", Type = "Expense", CategoryId = C("Lazer", "Expense"), Priority = 90 }); await _context.SaveChangesAsync(ct); } return categories; }
    private static void RefreshCounts(ImportBatch b) { b.TotalItems = b.Items.Count; b.ImportedCount = b.Items.Count(i => i.Status is ImportItemStatuses.Ready or ImportItemStatuses.Committed); b.DuplicateCount = b.Items.Count(i => i.Status == ImportItemStatuses.Duplicate); b.ReviewCount = b.Items.Count(i => i.Status == ImportItemStatuses.NeedsReview); b.IgnoredCount = b.Items.Count(i => i.Status == ImportItemStatuses.Ignored); }
    private async Task RefreshCountsStored(int id, CancellationToken ct) { var b = await _context.ImportBatches.Include(x => x.Items).FirstAsync(x => x.Id == id, ct); RefreshCounts(b); await _context.SaveChangesAsync(ct); }
    private static object Summary(ImportBatch b) => new { b.Id, b.AccountId, accountName = b.Account?.Name, b.FileName, b.FileType, b.Source, b.FileHash, b.LedgerBalance, b.PeriodStart, b.PeriodEnd, b.Status, b.TotalItems, b.ImportedCount, b.DuplicateCount, b.ReviewCount, b.IgnoredCount, b.CreatedAt, b.ConfirmedAt };
    private static object ItemResponse(ImportedStatementItem i) => new { i.Id, i.ImportBatchId, i.AccountId, i.ExternalId, i.PostedAt, i.SignedAmount, i.Memo, i.Status, i.Reason, i.TransactionId, i.CategoryId, categoryName = i.Category?.Name, i.TargetAccountId, i.ReportingKind, i.ResolvedDescription, i.ResolvedDate, i.ResolvedAmount, i.ResolvedType, i.InstallmentNumber, i.TotalInstallments };
    private async Task<object?> BuildBatchResponseAsync(int id, int userId, CancellationToken ct) { var b = await _context.ImportBatches.AsNoTracking().Include(x => x.Account).Include(x => x.Items).ThenInclude(x => x.Category).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct); if (b == null) return null; var calculated = await CalculateBalanceAsync(b, userId, ct); return new { batch = Summary(b), items = b.Items.OrderBy(x => x.PostedAt).Select(ItemResponse), reconciliation = new { ledgerBalance = b.LedgerBalance, calculatedBalance = calculated, difference = b.LedgerBalance.HasValue ? b.LedgerBalance.Value - calculated : (decimal?)null } }; }
    private async Task<decimal> CalculateBalanceAsync(ImportBatch b, int userId, CancellationToken ct) { var account = b.Account ?? await _context.Accounts.FirstAsync(a => a.Id == b.AccountId, ct); var end = (b.PeriodEnd ?? DateTime.UtcNow).Date.AddDays(1); var tx = await _context.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.AccountId == b.AccountId && t.Paid && t.Date < end).ToListAsync(ct); return account.IsCreditCard ? tx.Where(t => !t.IsTransfer).Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount) : account.InitialBalance + tx.Sum(t => t.Type == "Income" ? t.Amount : -t.Amount); }

    private static ParsedStatement ParseOfx(byte[] bytes) { var text = Decode(bytes); var list = new List<ImportCandidate>(); foreach (Match m in Regex.Matches(text, @"<STMTTRN>(.*?)(?=<STMTTRN>|</STMTTRN>|</BANKTRANLIST>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)) { var amount = Value(m.Value, "TRNAMT"); var date = Value(m.Value, "DTPOSTED"); if (!TryAmount(amount, out var signed) || !TryOfxDate(date, out var d) || signed == 0) continue; list.Add(new ImportCandidate(d, signed, Value(m.Value, "MEMO") ?? Value(m.Value, "NAME") ?? "Lancamento OFX", "", Value(m.Value, "FITID"), m.Value)); } if (list.Count == 0) throw new InvalidDataException("OFX sem transacoes reconheciveis."); decimal? balance = null; var balances = Regex.Matches(text, @"<BALAMT>\s*([^<\r\n]+)", RegexOptions.IgnoreCase); if (balances.Count > 0 && TryAmount(balances[^1].Groups[1].Value, out var b)) balance = b; return new ParsedStatement(list, balance, OptionalOfx(Value(text, "DTSTART")), OptionalOfx(Value(text, "DTEND"))); }
    private static string? Value(string text, string tag) { var m = Regex.Match(text, $@"<{tag}>\s*([^<\r\n]+)", RegexOptions.IgnoreCase); return m.Success ? m.Groups[1].Value.Trim() : null; }
    private static string Decode(byte[] bytes) { var head = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 512)); return head.Contains("ENCODING:1252", StringComparison.OrdinalIgnoreCase) ? Encoding.Latin1.GetString(bytes) : Encoding.UTF8.GetString(bytes); }
    private static bool TryOfxDate(string? raw, out DateTime date) { date = default; var d = new string((raw ?? "").TakeWhile(char.IsDigit).ToArray()); if (d.Length < 8 || !DateTime.TryParseExact(d[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false; date = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc); return true; }
    private static DateTime? OptionalOfx(string? value) => TryOfxDate(value, out var d) ? d : null;
    private static async Task<ParsedStatement> ParseTabularAsync(byte[] bytes, string ext, Account account, CancellationToken ct) { List<List<string>> rows; await using var s = new MemoryStream(bytes, false); rows = ext == ".csv" ? await Csv(s, ct) : Xlsx(s); if (rows.Count < 2) throw new InvalidDataException("Arquivo sem dados suficientes para importacao."); var l = Layout(rows) ?? throw new InvalidDataException("Nao foi possivel identificar as colunas do arquivo."); var list = rows.Skip(1).Select(r => TryCandidate(r, l, out var c) ? c : null).Where(c => c != null).Cast<ImportCandidate>().ToList(); return new ParsedStatement(list, null, null, null); }
    private static async Task<List<List<string>>> Csv(Stream s, CancellationToken ct) { using var r = new StreamReader(s, Encoding.UTF8, true, leaveOpen: true); var result = new List<List<string>>(); char? delim = null; while (await r.ReadLineAsync(ct) is { } line) { if (string.IsNullOrWhiteSpace(line)) continue; delim ??= line.Count(c => c == ';') > line.Count(c => c == ',') ? ';' : ','; result.Add(Split(line, delim.Value)); } return result; }
    private static List<List<string>> Xlsx(Stream s) { using var a = new ZipArchive(s, ZipArchiveMode.Read, true); var sh = a.GetEntry("xl/sharedStrings.xml"); var shared = sh == null ? [] : XDocument.Load(sh.Open()).Descendants(XName.Get("t", "http://schemas.openxmlformats.org/spreadsheetml/2006/main")).Select(x => x.Value).ToList(); var path = a.GetEntry("xl/worksheets/sheet1.xml") != null ? "xl/worksheets/sheet1.xml" : a.Entries.First(e => e.FullName.StartsWith("xl/worksheets/sheet")).FullName; var doc = XDocument.Load(a.GetEntry(path)!.Open()); XNamespace n = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; return doc.Descendants(n + "row").Select(row => row.Elements(n + "c").Select(c => c.Attribute("t")?.Value == "s" && int.TryParse(c.Element(n + "v")?.Value, out var i) && i < shared.Count ? shared[i] : c.Element(n + "v")?.Value ?? string.Empty).ToList()).Where(r => r.Any(x => x != "")).ToList(); }
    private static List<string> Split(string line, char d) { var result = new List<string>(); var current = new StringBuilder(); var q = false; foreach (var c in line) { if (c == '"') q = !q; else if (c == d && !q) { result.Add(current.ToString().Trim()); current.Clear(); } else current.Append(c); } result.Add(current.ToString().Trim()); return result; }
    private static ImportLayout? Layout(List<List<string>> rows) { var h = rows[0].Select(x => Normalize(x).Replace(" ", "")).ToList(); var d = Header(h, "data", "date"); var a = Header(h, "valor", "amount", "quantia"); var x = Header(h, "descricao", "title", "titulo", "historico", "nome"); var c = Header(h, "categoria", "category"); if (d >= 0 && a >= 0 && x >= 0) return new ImportLayout(d, a, x, c); var s = rows[1]; if (s.Count >= 4 && TryDate(s[0], out _) && TryAmount(s[1], out _)) return new ImportLayout(0, 1, 3, 2); if (s.Count >= 3 && TryDate(s[0], out _) && TryAmount(s[^1], out _)) return new ImportLayout(0, s.Count - 1, 1, s.Count > 3 ? 2 : -1); return null; }
    private static int Header(List<string> h, params string[] names) => h.FindIndex(x => names.Any(x.Contains));
    private static bool TryCandidate(List<string> row, ImportLayout l, out ImportCandidate? candidate) { candidate = null; if (!TryDate(Cell(row, l.Date), out var d) || !TryAmount(Cell(row, l.Amount), out var a) || a == 0 || string.IsNullOrWhiteSpace(Cell(row, l.Description))) return false; candidate = new ImportCandidate(DateTime.SpecifyKind(d.Date, DateTimeKind.Utc), a, Clean(Cell(row, l.Description)), l.Category >= 0 ? Clean(Cell(row, l.Category)) : "", null, string.Join(" | ", row)); return true; }
    private static string Cell(List<string> r, int i) => i >= 0 && i < r.Count ? r[i] : "";
    private static string Clean(string? s) => (s ?? "").Replace("\"", "").Trim();
    private static bool TryDate(string? s, out DateTime d) { s = Clean(s); if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial is > 20000 and < 80000) { d = DateTime.FromOADate(serial); return true; } return DateTime.TryParseExact(s, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d) || DateTime.TryParse(s, new CultureInfo("pt-BR"), DateTimeStyles.None, out d) || DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d); }
    private static bool TryAmount(string? s, out decimal a) { s = Regex.Replace(Clean(s).Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace("BRL", "", StringComparison.OrdinalIgnoreCase), @"\s+", ""); if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out a)) return true; if (decimal.TryParse(s, NumberStyles.Any, new CultureInfo("pt-BR"), out a)) return true; var n = s; if (n.Contains(',') && n.Contains('.')) n = n.LastIndexOf(',') > n.LastIndexOf('.') ? n.Replace(".", "").Replace(',', '.') : n.Replace(",", ""); else if (n.Count(c => c == ',') == 1) n = n.Replace(',', '.'); return decimal.TryParse(n, NumberStyles.Any, CultureInfo.InvariantCulture, out a); }
    private sealed record ImportLayout(int Date, int Amount, int Description, int Category);
    private sealed record ImportCandidate(DateTime Date, decimal SignedAmount, string Description, string RawCategory, string? ExternalId, string RawData);
    private sealed record ParsedStatement(List<ImportCandidate> Candidates, decimal? LedgerBalance, DateTime? PeriodStart, DateTime? PeriodEnd);
}

public sealed class ResolveImportItemRequest { public int? CategoryId { get; set; } public int? AccountId { get; set; } public int? TargetAccountId { get; set; } public string? Type { get; set; } public DateTime? Date { get; set; } public string? Description { get; set; } public decimal? Amount { get; set; } public string? ReportingKind { get; set; } public int? InstallmentNumber { get; set; } public int? TotalInstallments { get; set; } public string? Justification { get; set; } }
