using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;

namespace MyFinance.API.Services;

public interface IStatementImportService
{
    Task<StatementImportResult> ImportOfxAsync(int userId, int accountId, string fileName, byte[] bytes, string source, CancellationToken cancellationToken);
}

public sealed record StatementImportResult(int BatchId, int TotalItems, int ReviewCount, int DuplicateCount, string FileHash);

public sealed class StatementImportService : IStatementImportService
{
    private const int MaxBytes = 10 * 1024 * 1024;
    private readonly AppDbContext _db;

    public StatementImportService(AppDbContext db) => _db = db;

    public async Task<StatementImportResult> ImportOfxAsync(int userId, int accountId, string fileName, byte[] bytes, string source, CancellationToken cancellationToken)
    {
        if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new InvalidDataException("Arquivo OFX inválido ou maior que 10MB.");
        if (!Path.GetExtension(fileName).Equals(".ofx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O Gmail aceita apenas anexos OFX.");
        if (!await _db.Accounts.AnyAsync(x => x.Id == accountId && x.UserId == userId, cancellationToken)) throw new UnauthorizedAccessException("Conta de destino inválida.");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (await _db.ImportBatches.AnyAsync(x => x.UserId == userId && x.AccountId == accountId && x.FileHash == hash, cancellationToken))
            return new StatementImportResult(0, 0, 0, 0, hash);

        var text = Encoding.UTF8.GetString(bytes);
        var matches = Regex.Matches(text, @"<STMTTRN>(.*?)(?=<STMTTRN>|</STMTTRN>|</BANKTRANLIST>)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var candidates = matches.Select(match =>
        {
            var value = Read(match.Value, "TRNAMT");
            var date = Read(match.Value, "DTPOSTED");
            var id = Read(match.Value, "FITID");
            return decimal.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount)
                && DateTime.TryParseExact(new string((date ?? string.Empty).Take(8).ToArray()), "yyyyMMdd", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var posted)
                && amount != 0
                ? new { Amount = amount, Date = DateTime.SpecifyKind(posted.Date, DateTimeKind.Utc), Id = id, Memo = Read(match.Value, "MEMO") ?? Read(match.Value, "NAME") ?? "Lançamento OFX" }
                : null;
        }).Where(x => x is not null).Select(x => x!).ToList();
        if (candidates.Count == 0) throw new InvalidDataException("OFX sem transações reconhecíveis.");

        var existingIds = (await _db.Transactions.AsNoTracking().Where(x => x.UserId == userId && x.AccountId == accountId && x.Source == source && x.ExternalId != null).Select(x => x.ExternalId!).ToListAsync(cancellationToken)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var batch = new ImportBatch { UserId = userId, AccountId = accountId, FileName = Path.GetFileName(fileName), FileType = ".ofx", Source = source, FileHash = hash, PeriodStart = candidates.Min(x => x.Date), PeriodEnd = candidates.Max(x => x.Date) };
        foreach (var candidate in candidates)
        {
            var duplicate = !string.IsNullOrWhiteSpace(candidate.Id) && existingIds.Contains(candidate.Id);
            batch.Items.Add(new ImportedStatementItem
            {
                UserId = userId, AccountId = accountId, Source = source, ExternalId = candidate.Id, PostedAt = candidate.Date,
                SignedAmount = candidate.Amount, Memo = candidate.Memo, ResolvedDescription = candidate.Memo, ResolvedDate = candidate.Date,
                ResolvedAmount = Math.Abs(candidate.Amount), ResolvedType = candidate.Amount < 0 ? "Expense" : "Income",
                Status = duplicate ? ImportItemStatuses.Duplicate : ImportItemStatuses.NeedsReview,
                Reason = duplicate ? "Identificador externo já importado." : "Revisão necessária antes da confirmação."
            });
        }
        batch.TotalItems = batch.Items.Count;
        batch.DuplicateCount = batch.Items.Count(x => x.Status == ImportItemStatuses.Duplicate);
        batch.ReviewCount = batch.Items.Count(x => x.Status == ImportItemStatuses.NeedsReview);
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);
        return new StatementImportResult(batch.Id, batch.TotalItems, batch.ReviewCount, batch.DuplicateCount, hash);
    }

    private static string? Read(string text, string tag)
    {
        var match = Regex.Match(text, $@"<{tag}>\s*([^<\r\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
