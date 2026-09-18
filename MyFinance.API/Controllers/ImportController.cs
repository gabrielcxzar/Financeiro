using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using MyFinance.API.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace MyFinance.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ImportController : ControllerBase
{
    private static readonly Regex InstallmentPattern = new(@"(?:parcela\s*)?(?<current>\d{1,2})\s*/\s*(?<total>\d{1,2})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppDbContext _context;
    private readonly IFinancialSnapshotService _financialSnapshotService;
    private readonly IStatementImportService _statementImportService;
    public ImportController(AppDbContext context, IFinancialSnapshotService financialSnapshotService, IStatementImportService statementImportService) { _context = context; _financialSnapshotService = financialSnapshotService; _statementImportService = statementImportService; }
    public ImportController(AppDbContext context, IFinancialSnapshotService financialSnapshotService) : this(context, financialSnapshotService, new StatementImportService(context, financialSnapshotService)) { }
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
        byte[] bytes;
        await using (var input = file.OpenReadStream()) await using (var memory = new MemoryStream()) { await input.CopyToAsync(memory, cancellationToken); bytes = memory.ToArray(); }
        StatementImportPreview preview;
        var userId = GetUserId();
        try { preview = await _statementImportService.PreviewAsync(new StatementImportRequest(userId, accountId, Path.GetFileName(file.FileName), bytes), cancellationToken); }
        catch (InvalidDataException e) { return BadRequest(e.Message); }
        var batch = preview.Batch;
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
        if (kind == ReportingKinds.Normal && !(request.CategoryId ?? item.CategoryId).HasValue) return BadRequest("Compra de consumo exige categoria.");
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

    private Transaction CreateImported(ImportBatch b, ImportedStatementItem i, int userId) {
        if (!i.AccountId.HasValue) throw new InvalidOperationException("Item sem conta de origem.");
        var account = _context.Accounts.Local.FirstOrDefault(a => a.Id == i.AccountId.Value) ?? _context.Accounts.First(a => a.Id == i.AccountId.Value);
        var total = Math.Max(i.TotalInstallments ?? 1, 1); var current = Math.Clamp(i.InstallmentNumber ?? 1, 1, total); var totalCents = decimal.ToInt64(decimal.Round((i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) * 100m, 0, MidpointRounding.AwayFromZero)); var remaining = total - current + 1; var baseCents = totalCents / remaining; var remainder = totalCents % remaining; var date = i.ResolvedDate ?? i.PostedAt; var series = total > 1 ? Guid.NewGuid().ToString("N") : null; Transaction? first = null;
        for (var index = 0; index < remaining; index++) {
            var number = current + index; var tx = new Transaction { UserId = userId, AccountId = i.AccountId.Value, CategoryId = i.CategoryId, Date = date, Description = total > 1 ? $"{StripInstallment(i.ResolvedDescription ?? i.Memo)} ({number}/{total})" : i.ResolvedDescription ?? i.Memo, Amount = (baseCents + (index < remainder ? 1 : 0)) / 100m, Type = i.ResolvedType ?? "Expense", Paid = true, RecurringRuleId = i.RecurringRuleId, Source = b.Source, SourceFile = b.FileName, ExternalId = index == 0 ? i.ExternalId : null, RawMemo = i.Memo, ImportedAt = DateTime.UtcNow, ImportBatchId = b.Id, ReportingKind = i.ReportingKind, ExcludeFromReports = i.ReportingKind != ReportingKinds.Normal, InstallmentId = series }; _context.Transactions.Add(tx); first ??= tx; if (number < total) date = NextInstallmentDate(account, date); }
        return first!;
    }
    private DateTime NextInstallmentDate(Account account, DateTime current) { if (!account.IsCreditCard) return current.AddMonths(1); foreach (var offset in new[] { -1, 0, 1 }) { var reference = new DateTime(current.Year, current.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(offset); var window = _financialSnapshotService.GetInvoiceWindow(account, reference.Month, reference.Year); if (current >= window.StartDate && current < window.CloseDate) { var next = reference.AddMonths(1); return _financialSnapshotService.GetInvoiceWindow(account, next.Month, next.Year).StartDate.Add(current.TimeOfDay == TimeSpan.Zero ? TimeSpan.FromHours(12) : current.TimeOfDay); } } return current.AddMonths(1); }
    private static string StripInstallment(string value) => InstallmentPattern.Replace(value, string.Empty).Trim(' ', '-', '(', ')');
    private async Task<Transaction> CreateInvoicePayment(ImportBatch b, ImportedStatementItem i, int userId, CancellationToken ct) { if (!i.TargetAccountId.HasValue) throw new InvalidOperationException("Pagamento sem cartao."); var group = Guid.NewGuid().ToString("N"); var tx = CreateImported(b, i, userId); tx.Type = "Expense"; tx.IsTransfer = true; tx.TransferGroupId = group; tx.ReportingKind = ReportingKinds.InvoicePayment; tx.ExcludeFromReports = true; var card = new Transaction { UserId = userId, AccountId = i.TargetAccountId.Value, Date = tx.Date, Description = "Pagamento de fatura importado", Amount = tx.Amount, Type = "Income", Paid = true, IsTransfer = true, TransferGroupId = group, Source = b.Source, SourceFile = b.FileName, RawMemo = i.Memo, ImportedAt = DateTime.UtcNow, ImportBatchId = b.Id, ReportingKind = ReportingKinds.InvoicePayment, ExcludeFromReports = true }; _context.Transactions.Add(card); await Task.CompletedTask; return tx; }
    private async Task<bool> DuplicateOnConfirm(ImportedStatementItem i, CancellationToken ct) { if (!string.IsNullOrWhiteSpace(i.ExternalId)) return await _context.Transactions.AnyAsync(t => t.UserId == i.UserId && t.AccountId == i.AccountId && t.Source == i.Source && t.ExternalId == i.ExternalId, ct); var d = i.ResolvedDate ?? i.PostedAt; return await _context.Transactions.AnyAsync(t => t.UserId == i.UserId && t.AccountId == i.AccountId && t.Date.Date == d.Date && t.Amount == (i.ResolvedAmount ?? Math.Abs(i.SignedAmount)) && t.Type == (i.ResolvedType ?? "Expense") && t.Description == (i.ResolvedDescription ?? i.Memo) && !t.IsTransfer, ct); }

    private static void RefreshCounts(ImportBatch b) { b.TotalItems = b.Items.Count; b.ImportedCount = b.Items.Count(i => i.Status is ImportItemStatuses.Ready or ImportItemStatuses.Committed); b.DuplicateCount = b.Items.Count(i => i.Status == ImportItemStatuses.Duplicate); b.ReviewCount = b.Items.Count(i => i.Status == ImportItemStatuses.NeedsReview); b.IgnoredCount = b.Items.Count(i => i.Status == ImportItemStatuses.Ignored); }
    private async Task RefreshCountsStored(int id, CancellationToken ct) { var b = await _context.ImportBatches.Include(x => x.Items).FirstAsync(x => x.Id == id, ct); RefreshCounts(b); await _context.SaveChangesAsync(ct); }
    private static object Summary(ImportBatch b) => new { b.Id, b.AccountId, accountName = b.Account?.Name, b.FileName, b.FileType, b.Source, b.FileHash, b.LedgerBalance, b.PeriodStart, b.PeriodEnd, b.Status, b.TotalItems, b.ImportedCount, b.DuplicateCount, b.ReviewCount, b.IgnoredCount, b.CreatedAt, b.ConfirmedAt };
    private static object ItemResponse(ImportedStatementItem i) => new { i.Id, i.ImportBatchId, i.AccountId, i.ExternalId, i.PostedAt, i.SignedAmount, i.Memo, i.Status, i.Reason, i.TransactionId, i.CategoryId, categoryName = i.Category?.Name, i.TargetAccountId, i.ReportingKind, i.ResolvedDescription, i.ResolvedDate, i.ResolvedAmount, i.ResolvedType, i.InstallmentNumber, i.TotalInstallments };
    private async Task<object?> BuildBatchResponseAsync(int id, int userId, CancellationToken ct) { var b = await _context.ImportBatches.AsNoTracking().Include(x => x.Account).Include(x => x.Items).ThenInclude(x => x.Category).FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct); if (b == null) return null; var calculated = await CalculateBalanceAsync(b, userId, ct); return new { batch = Summary(b), items = b.Items.OrderBy(x => x.PostedAt).Select(ItemResponse), reconciliation = new { ledgerBalance = b.LedgerBalance, calculatedBalance = calculated, difference = b.LedgerBalance.HasValue ? b.LedgerBalance.Value - calculated : (decimal?)null } }; }
    private async Task<decimal> CalculateBalanceAsync(ImportBatch b, int userId, CancellationToken ct) { if (!b.AccountId.HasValue) return 0m; var account = b.Account ?? await _context.Accounts.FirstAsync(a => a.Id == b.AccountId.Value, ct); var end = (b.PeriodEnd ?? DateTime.UtcNow).Date.AddDays(1); var tx = await _context.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.AccountId == b.AccountId.Value && t.Paid && t.Date < end).ToListAsync(ct); return account.IsCreditCard ? tx.Where(t => !t.IsTransfer).Sum(t => t.Type == "Expense" ? t.Amount : -t.Amount) : account.InitialBalance + tx.Sum(t => t.Type == "Income" ? t.Amount : -t.Amount); }

}

public sealed class ResolveImportItemRequest { public int? CategoryId { get; set; } public int? AccountId { get; set; } public int? TargetAccountId { get; set; } public string? Type { get; set; } public DateTime? Date { get; set; } public string? Description { get; set; } public decimal? Amount { get; set; } public string? ReportingKind { get; set; } public int? InstallmentNumber { get; set; } public int? TotalInstallments { get; set; } public string? Justification { get; set; } }
