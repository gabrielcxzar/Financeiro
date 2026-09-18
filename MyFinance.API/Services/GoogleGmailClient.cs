using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace MyFinance.API.Services;

public sealed class GoogleGmailClient : IGmailClient
{
    private readonly HttpClient _http;
    private readonly GmailIntegrationOptions _options;

    public GoogleGmailClient(HttpClient http, GmailIntegrationOptions options)
    {
        _http = http;
        _options = options;
    }

    public string BuildAuthorizationUrl(string state)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["client_id"] = _options.ClientId;
        query["redirect_uri"] = _options.RedirectUri;
        query["response_type"] = "code";
        query["access_type"] = "offline";
        query["prompt"] = "consent";
        query["scope"] = "https://www.googleapis.com/auth/gmail.readonly";
        query["state"] = state;
        return $"https://accounts.google.com/o/oauth2/v2/auth?{query}";
    }

    public async Task<GmailToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
        await TokenRequestAsync(new Dictionary<string, string> { ["code"] = code, ["grant_type"] = "authorization_code" }, cancellationToken);

    public async Task<GmailToken> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        await TokenRequestAsync(new Dictionary<string, string> { ["refresh_token"] = refreshToken, ["grant_type"] = "refresh_token" }, cancellationToken);

    public async Task<string?> GetEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://gmail.googleapis.com/gmail/v1/users/me/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.TryGetProperty("emailAddress", out var email) ? email.GetString() : null;
    }

    public async Task<IReadOnlyList<GmailMessageAttachment>> FindOfxAttachmentsAsync(string accessToken, string query, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Get, $"https://gmail.googleapis.com/gmail/v1/users/me/messages?q={Uri.EscapeDataString(query)}&maxResults=50", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var results = new List<GmailMessageAttachment>();
        foreach (var message in document.RootElement.TryGetProperty("messages", out var messages) ? messages.EnumerateArray() : [])
        {
            var id = message.GetProperty("id").GetString()!;
            const string fields = "id,payload(headers(name,value),filename,body(attachmentId,size),parts(filename,mimeType,body(attachmentId,size),parts(filename,mimeType,body(attachmentId,size),parts(filename,mimeType,body(attachmentId,size))))";
            var detailUrl = $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{id}?format=full&fields={Uri.EscapeDataString(fields)}";
            using var detail = await SendJsonAsync(Authorized(HttpMethod.Get, detailUrl, accessToken), cancellationToken);
            var root = detail.RootElement;
            var headers = root.GetProperty("payload").TryGetProperty("headers", out var headerArray) ? headerArray.EnumerateArray().ToList() : [];
            var date = headers.FirstOrDefault(x => x.GetProperty("name").GetString() == "Date").GetPropertyOrNull("value");
            var subject = headers.FirstOrDefault(x => x.GetProperty("name").GetString() == "Subject").GetPropertyOrNull("value");
            WalkParts(root.GetProperty("payload"), id, date, subject, results);
        }
        return results;
    }

    public async Task<byte[]> DownloadAttachmentAsync(string accessToken, GmailMessageAttachment attachment, CancellationToken cancellationToken)
    {
        using var request = Authorized(HttpMethod.Get, $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{attachment.MessageId}/attachments/{attachment.AttachmentId}", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var data = document.RootElement.GetProperty("data").GetString() ?? string.Empty;
        return Convert.FromBase64String(data.Replace('-', '+').Replace('_', '/') + new string('=', (4 - data.Length % 4) % 4));
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync($"https://oauth2.googleapis.com/revoke?token={Uri.EscapeDataString(token)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<GmailToken> TokenRequestAsync(Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        values["client_id"] = _options.ClientId;
        values["client_secret"] = _options.ClientSecret;
        values["redirect_uri"] = _options.RedirectUri;
        using var response = await _http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(values), cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var root = document.RootElement;
        return new GmailToken(root.GetProperty("access_token").GetString()!, root.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null, root.GetProperty("expires_in").GetInt32());
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<JsonDocument> SendJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
    }

    private static void WalkParts(JsonElement part, string messageId, string? date, string? subject, ICollection<GmailMessageAttachment> results)
    {
        if (part.TryGetProperty("filename", out var filenameElement) && filenameElement.GetString() is { } filename && filename.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase) && part.TryGetProperty("body", out var body) && body.TryGetProperty("attachmentId", out var attachmentId) && !string.IsNullOrWhiteSpace(attachmentId.GetString()))
        {
            int? size = body.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt32(out var parsedSize) ? parsedSize : null;
            results.Add(new GmailMessageAttachment(messageId, attachmentId.GetString()!, Path.GetFileName(filename), DateTime.TryParse(date, out var parsed) ? parsed : null, subject, size));
        }
        if (part.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            foreach (var child in parts.EnumerateArray()) WalkParts(child, messageId, date, subject, results);
    }
}

internal static class GmailJsonExtensions
{
    public static string? GetPropertyOrNull(this JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
