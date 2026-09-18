using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    private readonly AppDbContext _db;
    private readonly GmailIntegrationOptions _options;
    private readonly IGmailClient _gmail;
    private readonly IGmailTokenProtector _protector;
    private readonly IStatementImportService _import;

    public GmailIntegrationController(AppDbContext db, GmailIntegrationOptions options, IGmailClient gmail, IGmailTokenProtector protector, IStatementImportService import)
    {
        _db = db;
        _options = options;
        _gmail = gmail;
        _protector = protector;
        _import = import;
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var integration = await _db.GmailIntegrations.AsNoTracking().Include(x => x.DefaultAccount).SingleOrDefaultAsync(x => x.UserId == UserId, ct);
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
            searchQuery = integration?.SearchQuery ?? _options.DefaultSearchQuery
        });
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
        if (!integration.DefaultAccountId.HasValue) return BadRequest("Escolha uma conta FinFlow de destino antes de sincronizar.");
        var access = await _gmail.RefreshAsync(_protector.Unprotect(integration.EncryptedRefreshToken), ct);
        var attachments = await _gmail.FindOfxAttachmentsAsync(access.AccessToken, integration.SearchQuery, ct);
        var result = new SyncResult();
        foreach (var attachment in attachments)
        {
            if (await _db.ExternalImportArtifacts.AnyAsync(x => x.UserId == UserId && x.Provider == Provider && x.ExternalMessageId == attachment.MessageId && x.ExternalAttachmentId == attachment.AttachmentId, ct)) { result.AlreadyProcessed++; continue; }
            var bytes = await _gmail.DownloadAttachmentAsync(access.AccessToken, attachment, ct);
            if (bytes.Length > 10 * 1024 * 1024) { result.Invalid++; continue; }
            try
            {
                var imported = await _import.ImportOfxAsync(UserId, integration.DefaultAccountId.Value, attachment.FileName, bytes, Provider, ct);
                var artifact = new ExternalImportArtifact { UserId = UserId, Provider = Provider, ExternalMessageId = attachment.MessageId, ExternalAttachmentId = attachment.AttachmentId, FileName = attachment.FileName, FileHash = imported.FileHash, ImportBatchId = imported.BatchId == 0 ? null : imported.BatchId, ImportedAt = imported.BatchId == 0 ? null : DateTime.UtcNow };
                _db.ExternalImportArtifacts.Add(artifact);
                await _db.SaveChangesAsync(ct);
                if (imported.BatchId == 0) result.AlreadyProcessed++; else { result.NewBatches++; result.ReviewItems += imported.ReviewCount; }
            }
            catch (InvalidDataException) { result.Invalid++; }
        }
        integration.LastSyncAt = DateTime.UtcNow;
        integration.LastSuccessfulSyncAt = DateTime.UtcNow;
        integration.LastErrorCode = null;
        await _db.SaveChangesAsync(ct);
        return Ok(new { found = attachments.Count, newBatches = result.NewBatches, alreadyProcessed = result.AlreadyProcessed, reviewItems = result.ReviewItems, invalid = result.Invalid });
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
    private async Task<GmailIntegration> GetOrCreateAsync(int userId, CancellationToken ct) => await _db.GmailIntegrations.SingleOrDefaultAsync(x => x.UserId == userId, ct) ?? new GmailIntegration { UserId = userId, SearchQuery = _options.DefaultSearchQuery }.AlsoAdd(_db);
    private string Frontend(string query) => $"{_options.FrontendBaseUrl.TrimEnd('/')}/?{query}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private sealed class SyncResult { public int NewBatches; public int AlreadyProcessed; public int ReviewItems; public int Invalid; }
}

public sealed record GmailConfigureRequest(int DefaultAccountId, string? SearchQuery);

internal static class GmailIntegrationExtensions
{
    public static GmailIntegration AlsoAdd(this GmailIntegration integration, AppDbContext db) { db.GmailIntegrations.Add(integration); return integration; }
}
