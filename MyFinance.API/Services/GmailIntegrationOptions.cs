namespace MyFinance.API.Services;

public sealed class GmailIntegrationOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string TokenEncryptionKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string FrontendBaseUrl { get; set; } = string.Empty;
    public string DefaultSearchQuery { get; set; } = "from:(todomundo@nubank.com.br) subject:\"Extrato da sua conta do Nubank\" has:attachment filename:ofx newer_than:90d";
}

public sealed record GmailMessageAttachment(string MessageId, string AttachmentId, string FileName, DateTime? Date, string? Subject, int? Size = null);
public sealed record GmailToken(string AccessToken, string? RefreshToken, int ExpiresIn);

public interface IGmailClient
{
    string BuildAuthorizationUrl(string state);
    Task<GmailToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken);
    Task<GmailToken> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<string?> GetEmailAsync(string accessToken, CancellationToken cancellationToken);
    Task<IReadOnlyList<GmailMessageAttachment>> FindOfxAttachmentsAsync(string accessToken, string query, CancellationToken cancellationToken);
    Task<byte[]> DownloadAttachmentAsync(string accessToken, GmailMessageAttachment attachment, CancellationToken cancellationToken);
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
