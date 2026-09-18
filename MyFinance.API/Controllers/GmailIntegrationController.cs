using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using MyFinance.API.Models;
using MyFinance.API.Services;

namespace MyFinance.API.Controllers;

[ApiController]
[Route("api/integrations/gmail")]
public sealed class GmailIntegrationController : ControllerBase
{
    private const string Provider = "gmail";
    private const string NubankAccountSearchQuery = "from:(todomundo@nubank.com.br) subject:\"Extrato da sua conta do Nubank\" has:attachment filename:ofx newer_than:90d";
    private readonly AppDbContext _db;
    private readonly GmailIntegrationOptions _options;
    private readonly IGmailClient _gmail;
    private readonly IGmailTokenProtector _protector;
    private readonly IStatementImportService _import;
    private readonly ILogger<GmailIntegrationController> _logger;

    public GmailIntegrationController(AppDbContext db, GmailIntegrationOptions options, IGmailClient gmail, IGmailTokenProtector protector, IStatementImportService import, ILogger<GmailIntegrationController> logger)
    {
        _db = db;
        _options = options;
        _gmail = gmail;
        _protector = protector;
        _import = import;
        _logger = logger;
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var integration = await _db.GmailIntegrations.AsNoTracking().Include(x => x.DefaultAccount).SingleOrDefaultAsync(x => x.UserId == UserId, ct);
        if (integration is not null) await EnsureRulesAsync(integration, ct);
        return Ok(new
        {
            enabled = _options.Enabled,
            connected = integration is { Enabled: true },
            googleEmail = integration?.GoogleEmail,
            defaultAccountId = integration?.DefaultAccountId,
            defaultAccountName = integration?.DefaultAccount?.Name,
            lastSyncAt = integration?.LastSyncAt,
            lastSuccessfulSyncAt = integration?.LastSuccessfulSyncAt,
            lastErrorCode = integration?.LastErrorCode,
            searchQuery = integration?.SearchQuery ?? _options.DefaultSearchQuery,
            rules = await RulesForUser(UserId, ct)
        });
    }

    [HttpGet("rules")]
    [Authorize]
    public async Task<IActionResult> Rules(CancellationToken ct) => Ok(await RulesForUser(UserId, ct));

    [HttpGet("batches")]
    [Authorize]
    public async Task<IActionResult> Batches(CancellationToken ct)
    {
        var rows = await _db.ExternalImportArtifacts.AsNoTracking()
            .Include(x => x.ImportBatch)
            .Where(x => x.UserId == UserId && x.Provider == Provider && x.ImportBatchId.HasValue)
            .OrderByDescending(x => x.ImportedAt ?? x.DiscoveredAt)
            .ToListAsync(ct);
        return Ok(rows.Where(x => x.ImportBatch is not null).GroupBy(x => x.ImportBatchId!.Value).Select(g =>
        {
            var batch = g.First().ImportBatch!;
            return new { batchId = batch.Id, batch.FileName, batch.CreatedAt, batch.Status, batch.ReviewCount, batch.DuplicateCount, batch.ImportedCount };
        }));
    }

    [HttpPost("rules")]
    [Authorize]
    public async Task<IActionResult> CreateRule([FromBody] GmailImportRuleRequest request, CancellationToken ct)
    {
        if (!await _db.Accounts.AnyAsync(x => x.Id == request.TargetAccountId && x.UserId == UserId, ct)) return BadRequest("Conta de destino inválida.");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SearchQuery)) return BadRequest("Nome e consulta são obrigatórios.");
        var rule = new GmailImportRule { UserId = UserId, Name = request.Name.Trim(), SearchQuery = request.SearchQuery.Trim(), TargetAccountId = request.TargetAccountId, Enabled = request.Enabled };
        _db.GmailImportRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return Ok(await RuleForUser(rule.Id, ct));
    }

    [HttpPut("rules/{id:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] GmailImportRuleRequest request, CancellationToken ct)
    {
        var rule = await _db.GmailImportRules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (rule is null) return NotFound();
        if (!await _db.Accounts.AnyAsync(x => x.Id == request.TargetAccountId && x.UserId == UserId, ct)) return BadRequest("Conta de destino inválida.");
        rule.Name = request.Name.Trim(); rule.SearchQuery = request.SearchQuery.Trim(); rule.TargetAccountId = request.TargetAccountId; rule.Enabled = request.Enabled;
        await _db.SaveChangesAsync(ct);
        return Ok(await RuleForUser(rule.Id, ct));
    }

    [HttpPost("configure")]
    [Authorize]
    public async Task<IActionResult> Configure([FromBody] GmailConfigureRequest request, CancellationToken ct)
    {
        if (!await _db.Accounts.AnyAsync(x => x.Id == request.DefaultAccountId && x.UserId == UserId, ct)) return BadRequest("Conta de destino inválida.");
        var integration = await GetOrCreateAsync(UserId, ct);
        integration.DefaultAccountId = request.DefaultAccountId;
        integration.SearchQuery = string.IsNullOrWhiteSpace(request.SearchQuery) ? _options.DefaultSearchQuery : request.SearchQuery.Trim();
        await _db.SaveChangesAsync(ct);
        await EnsureRulesAsync(integration, ct);
        return Ok(new { integration.DefaultAccountId, integration.SearchQuery });
    }

    [HttpGet("connect")]
    [Authorize]
    public async Task<IActionResult> Connect(CancellationToken ct)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret) || string.IsNullOrWhiteSpace(_options.RedirectUri))
            return BadRequest("Gmail não configurado neste ambiente.");
        var rawState = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        _db.GmailOAuthStates.Add(new GmailOAuthState { StateHash = Hash(rawState), UserId = UserId, ExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        await _db.SaveChangesAsync(ct);
        return Ok(new { authorizationUrl = _gmail.BuildAuthorizationUrl(rawState) });
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(error)) return Redirect(Frontend("gmail=denied"));
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state)) return BadRequest("Callback OAuth inválido.");
        var oauthState = await _db.GmailOAuthStates.SingleOrDefaultAsync(x => x.StateHash == Hash(state), ct);
        if (oauthState is null || oauthState.UsedAt.HasValue || oauthState.ExpiresAt <= DateTime.UtcNow) return BadRequest("Estado OAuth inválido ou expirado.");
        oauthState.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        try
        {
            var token = await _gmail.ExchangeCodeAsync(code, ct);
            if (string.IsNullOrWhiteSpace(token.RefreshToken)) return Redirect(Frontend("gmail=missing_offline_access"));
            var integration = await GetOrCreateAsync(oauthState.UserId, ct);
            integration.GoogleEmail = await _gmail.GetEmailAsync(token.AccessToken, ct);
            integration.EncryptedRefreshToken = _protector.Protect(token.RefreshToken);
            integration.ConnectedAt = DateTime.UtcNow;
            integration.Enabled = true;
            integration.LastErrorCode = null;
            await _db.SaveChangesAsync(ct);
            return Redirect(Frontend("gmail=connected"));
        }
        catch
        {
            return Redirect(Frontend("gmail=error"));
        }
    }

    [HttpPost("sync")]
    [Authorize]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var integration = await _db.GmailIntegrations.SingleOrDefaultAsync(x => x.UserId == UserId && x.Enabled, ct);
        if (integration is null) return BadRequest("Conecte o Gmail antes de sincronizar.");
        var rules = await EnsureRulesAsync(integration, ct);
        var enabledRules = rules.Where(x => x.Enabled).ToList();
        if (enabledRules.Count == 0) return BadRequest("Crie ou ative uma regra de importação antes de sincronizar.");
        integration.LastSyncAt = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        try
        {
            var access = await _gmail.RefreshAsync(_protector.Unprotect(integration.EncryptedRefreshToken), ct);
            var candidates = new List<RuleAttachment>();
            var ruleErrors = 0;
            foreach (var rule in enabledRules)
            {
                try
                {
                    var foundAttachments = await _gmail.FindOfxAttachmentsAsync(access.AccessToken, rule.SearchQuery, ct);
                    candidates.AddRange(foundAttachments.Select(attachment => new RuleAttachment(rule, attachment)));
                    _logger.LogInformation("Gmail sync user={UserId} rule={RuleId} provider=gmail attachments={Count}", UserId, rule.Id, foundAttachments.Count);
                }
                catch (Exception exception) when (exception is HttpRequestException or JsonException)
                {
                    ruleErrors++;
                    _logger.LogWarning(exception, "Gmail sync rule failed user={UserId} rule={RuleId} provider=gmail", UserId, rule.Id);
                }
            }
            var attachments = candidates.GroupBy(x => $"{x.Attachment.MessageId}\u001f{x.Attachment.AttachmentId}", StringComparer.Ordinal).ToList();
            var result = new SyncResult();
            foreach (var group in attachments)
            {
                var targets = group.Select(x => x.Rule.TargetAccountId).Distinct().ToList();
                if (targets.Count > 1) { result.Invalid++; continue; }
                var ruleAttachment = group.First();
                var attachment = ruleAttachment.Attachment;
                var targetAccountId = ruleAttachment.Rule.TargetAccountId;
                if (await _db.ExternalImportArtifacts.AnyAsync(x => x.UserId == UserId && x.Provider == Provider && x.ExternalMessageId == attachment.MessageId && x.ExternalAttachmentId == attachment.AttachmentId, ct)) { result.AlreadyProcessed++; continue; }
                if (attachment.Size > 10 * 1024 * 1024) { result.Invalid++; continue; }
                var bytes = await _gmail.DownloadAttachmentAsync(access.AccessToken, attachment, ct);
                if (bytes.Length > 10 * 1024 * 1024) { result.Invalid++; continue; }
                try
                {
                    var preview = await _import.PreviewAsync(new StatementImportRequest(UserId, targetAccountId, attachment.FileName, bytes), ct);
                    var artifact = new ExternalImportArtifact { UserId = UserId, Provider = Provider, ExternalMessageId = attachment.MessageId, ExternalAttachmentId = attachment.AttachmentId, FileName = attachment.FileName, FileHash = preview.Batch.FileHash, ImportBatchId = preview.Batch.Id == 0 ? null : preview.Batch.Id, ImportedAt = preview.Batch.Id == 0 ? null : DateTime.UtcNow };
                    _db.ExternalImportArtifacts.Add(artifact);
                    await _db.SaveChangesAsync(ct);
                    if (preview.ExistingFileHash) result.AlreadyProcessed++; else { result.NewBatches++; result.ReviewItems += preview.Batch.ReviewCount; }
                }
                catch (InvalidDataException) { result.Invalid++; }
            }
            integration.LastSuccessfulSyncAt = ruleErrors == 0 ? DateTime.UtcNow : integration.LastSuccessfulSyncAt;
            integration.LastErrorCode = ruleErrors == 0 ? null : "partial_rule_failure";
            await _db.SaveChangesAsync(ct);
            timer.Stop();
            _logger.LogInformation("Gmail sync finished user={UserId} provider=gmail attachments={Found} newBatches={NewBatches} alreadyProcessed={AlreadyProcessed} invalid={Invalid} durationMs={DurationMs}", UserId, attachments.Count, result.NewBatches, result.AlreadyProcessed, result.Invalid, timer.ElapsedMilliseconds);
            return Ok(new { found = attachments.Count, newBatches = result.NewBatches, alreadyProcessed = result.AlreadyProcessed, reviewItems = result.ReviewItems, invalid = result.Invalid, ruleErrors });
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or CryptographicException or JsonException or FormatException or InvalidDataException)
        {
            integration.LastErrorCode = exception switch { HttpRequestException => "gmail_api_unavailable", CryptographicException => "gmail_token_invalid", JsonException => "gmail_response_invalid", FormatException or InvalidDataException => "invalid_attachment", _ => "gmail_configuration" };
            await _db.SaveChangesAsync(ct);
            return StatusCode(StatusCodes.Status502BadGateway, new { code = integration.LastErrorCode, message = "Não foi possível sincronizar o Gmail agora." });
        }
    }

    [HttpPost("disconnect")]
    [Authorize]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        var integration = await _db.GmailIntegrations.SingleOrDefaultAsync(x => x.UserId == UserId, ct);
        if (integration is null) return NoContent();
        try { await _gmail.RevokeAsync(_protector.Unprotect(integration.EncryptedRefreshToken), ct); } catch { /* revocation best-effort; local token is still removed */ }
        integration.Enabled = false;
        integration.EncryptedRefreshToken = string.Empty;
        integration.GoogleEmail = null;
        integration.LastErrorCode = null;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<IReadOnlyList<object>> RulesForUser(int userId, CancellationToken ct) => (await _db.GmailImportRules.AsNoTracking().Include(x => x.TargetAccount).Where(x => x.UserId == userId).OrderBy(x => x.Id).ToListAsync(ct)).Select(x => (object)new { x.Id, x.Name, x.SearchQuery, x.TargetAccountId, targetAccountName = x.TargetAccount?.Name, x.Enabled, x.CreatedAt }).ToList();
    private async Task<object?> RuleForUser(int id, CancellationToken ct) => (await _db.GmailImportRules.AsNoTracking().Include(x => x.TargetAccount).FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct)) is { } x ? new { x.Id, x.Name, x.SearchQuery, x.TargetAccountId, targetAccountName = x.TargetAccount?.Name, x.Enabled, x.CreatedAt } : null;
    private async Task<List<GmailImportRule>> EnsureRulesAsync(GmailIntegration integration, CancellationToken ct)
    {
        var rules = await _db.GmailImportRules.Where(x => x.UserId == UserId).ToListAsync(ct);
        if (rules.Count == 0 && integration.DefaultAccountId.HasValue)
        {
            var defaultRule = new GmailImportRule { UserId = UserId, Name = "Nubank — Conta", SearchQuery = NubankAccountSearchQuery, TargetAccountId = integration.DefaultAccountId.Value, Enabled = true };
            _db.GmailImportRules.Add(defaultRule);
            try { await _db.SaveChangesAsync(ct); rules.Add(defaultRule); }
            catch (DbUpdateException)
            {
                _db.Entry(defaultRule).State = EntityState.Detached;
                rules = await _db.GmailImportRules.Where(x => x.UserId == UserId).ToListAsync(ct);
            }
        }
        return rules;
    }
    private async Task<GmailIntegration> GetOrCreateAsync(int userId, CancellationToken ct) => await _db.GmailIntegrations.SingleOrDefaultAsync(x => x.UserId == userId, ct) ?? new GmailIntegration { UserId = userId, SearchQuery = _options.DefaultSearchQuery }.AlsoAdd(_db);
    private string Frontend(string query) => $"{_options.FrontendBaseUrl.TrimEnd('/')}/?{query}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed record RuleAttachment(GmailImportRule Rule, GmailMessageAttachment Attachment);
    private sealed class SyncResult { public int NewBatches; public int AlreadyProcessed; public int ReviewItems; public int Invalid; }
}

public sealed record GmailConfigureRequest(int DefaultAccountId, string? SearchQuery);
public sealed record GmailImportRuleRequest(string Name, string SearchQuery, int TargetAccountId, bool Enabled = true);

internal static class GmailIntegrationExtensions
{
    public static GmailIntegration AlsoAdd(this GmailIntegration integration, AppDbContext db) { db.GmailIntegrations.Add(integration); return integration; }
}
