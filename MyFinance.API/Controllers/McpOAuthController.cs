using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Net;

namespace MyFinance.API.Controllers;

[AllowAnonymous]
public sealed class McpOAuthController(AppDbContext db, IAntiforgery antiforgery, IConfiguration configuration) : Controller
{
    private string Resource => ((configuration["McpOAuth:Issuer"] ?? "https://localhost:10000/").TrimEnd('/') + "/mcp");
    [HttpGet("/oauth/authorize")]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext) ?? throw new InvalidOperationException("OAuth request ausente.");
        if (!request.HasResource(Resource)) return BadRequest(new { error = "invalid_target" });
        var auth = await HttpContext.AuthenticateAsync("McpOAuthCookie");
        if (!auth.Succeeded) return Utf8Html(LoginForm(request));
        return Utf8Html(ConsentForm(request));
    }

    [HttpPost("/oauth/authorize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuthorizePost(string email, string password, string consent, CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext) ?? throw new InvalidOperationException("OAuth request ausente.");
        if (!request.HasResource(Resource)) return BadRequest(new { error = "invalid_target" });
        var auth = await HttpContext.AuthenticateAsync("McpOAuthCookie");
        if (!auth.Succeeded)
        {
            var loginUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant(), cancellationToken);
            if (loginUser is null || !BCrypt.Net.BCrypt.Verify(password, loginUser.PasswordHash)) return Unauthorized("Credenciais inválidas.");
            var cookieIdentity = new ClaimsIdentity("McpOAuthCookie");
            cookieIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, loginUser.Id.ToString()));
            cookieIdentity.AddClaim(new Claim(ClaimTypes.Name, loginUser.Name));
            await HttpContext.SignInAsync("McpOAuthCookie", new ClaimsPrincipal(cookieIdentity));
            return Redirect(BuildAuthorizeUri(request));
        }
        if (!string.Equals(consent, "accept", StringComparison.Ordinal)) return BadRequest(new { error = "access_denied" });

        var userId = auth.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userId, out var authenticatedUserId)) return Unauthorized();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == authenticatedUserId, cancellationToken);
        if (user is null) return Unauthorized();

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, user.Name);
        var scopes = request.GetScopes()
            .Where(scope => scope is "finflow.read" or "offline_access")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        identity.SetScopes(scopes);
        identity.SetResources(Resource);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(scopes);
        principal.SetResources(Resource);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("/oauth/token")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext) ?? throw new InvalidOperationException("OAuth request ausente.");
        if (!request.HasResource(Resource)) return BadRequest(new { error = "invalid_target" });
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            return BadRequest(new { error = "unsupported_grant_type" });
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null) return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        var scopes = principal.GetScopes()
            .Where(scope => scope is "finflow.read" or "offline_access")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        principal.SetScopes(scopes);
        principal.SetResources(Resource);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("/oauth/revoke")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Revoke(CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext);
        if (request is null || string.IsNullOrWhiteSpace(request.Token)) return BadRequest(new { error = "invalid_request" });
        var manager = HttpContext.RequestServices.GetRequiredService<IOpenIddictTokenManager>();
        var token = await manager.FindByReferenceIdAsync(request.Token, cancellationToken);
        if (token is not null)
            await manager.TryRevokeAsync(token, cancellationToken);
        return Ok();
    }

    private string LoginForm(OpenIddictRequest request)
    {
        var token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken;
        return Page("Autorizar ChatGPT", $"""
            <h1>FinFlow</h1>
            <p class="subtitle">Autorizar ChatGPT</p>
            <p>Entre na sua conta para continuar com a conexão somente leitura.</p>
            <form method="post" action="/oauth/authorize">
                {Hidden(request)}
                <input type="hidden" name="__RequestVerificationToken" value="{WebUtility.HtmlEncode(token)}" />
                <label for="email">E-mail</label>
                <input id="email" name="email" type="email" autocomplete="username" required />
                <label for="password">Senha</label>
                <input id="password" name="password" type="password" autocomplete="current-password" required />
                <input type="hidden" name="consent" value="accept" />
                <button type="submit">Entrar e continuar</button>
            </form>
            """);
    }

    private string ConsentForm(OpenIddictRequest request)
    {
        var token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken;
        return Page("Confirmar acesso", $"""
            <h1>FinFlow</h1>
            <p class="subtitle">Confirmar acesso</p>
            <p>O ChatGPT poderá consultar seus dados financeiros em modo somente leitura. Ele não poderá criar, editar, importar ou excluir dados.</p>
            <form method="post" action="/oauth/authorize">
                {Hidden(request)}
                <input type="hidden" name="__RequestVerificationToken" value="{WebUtility.HtmlEncode(token)}" />
                <div class="actions">
                    <button type="submit" name="consent" value="accept">Permitir acesso</button>
                    <button class="secondary" type="submit" name="consent" value="deny">Negar</button>
                </div>
            </form>
            """);
    }

    private ContentResult Utf8Html(string html) => new()
    {
        Content = html,
        ContentType = "text/html; charset=utf-8",
        StatusCode = StatusCodes.Status200OK
    };

    private static string Page(string title, string content) => $$"""
        <!doctype html>
        <html lang="pt-BR">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>FinFlow — {{WebUtility.HtmlEncode(title)}}</title>
            <style>
                :root { color-scheme: light; font-family: Inter, system-ui, sans-serif; color: #0f172a; background: #f8fafc; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 24px; box-sizing: border-box; }
                main { width: min(100%, 420px); padding: 32px; border: 1px solid #e2e8f0; border-radius: 16px; background: white; box-shadow: 0 12px 36px rgb(15 23 42 / 8%); }
                h1 { margin: 0; font-size: 30px; letter-spacing: -0.03em; }
                .subtitle { margin: 4px 0 24px; color: #047857; font-weight: 700; }
                p { line-height: 1.55; }
                form { display: grid; gap: 10px; margin-top: 24px; }
                label { margin-top: 6px; font-weight: 650; }
                input { padding: 12px 14px; border: 1px solid #cbd5e1; border-radius: 10px; font: inherit; }
                button { margin-top: 10px; padding: 12px 16px; border: 0; border-radius: 10px; background: #0f172a; color: white; font: inherit; font-weight: 700; cursor: pointer; }
                button.secondary { background: #e2e8f0; color: #0f172a; }
                .actions { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
            </style>
        </head>
        <body><main>{{content}}</main></body>
        </html>
        """;

    private static string BuildAuthorizeUri(OpenIddictRequest request)
    {
        var values = new[] { ("client_id", request.ClientId), ("redirect_uri", request.RedirectUri), ("response_type", request.ResponseType), ("scope", request.Scope), ("resource", string.Join(" ", request.GetResources())), ("state", request.State), ("code_challenge", request.CodeChallenge), ("code_challenge_method", request.CodeChallengeMethod) };
        return "/oauth/authorize?" + string.Join("&", values.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => $"{x.Item1}={Uri.EscapeDataString(x.Item2!)}"));
    }

    private static string Hidden(OpenIddictRequest request) => string.Join("", new[] { ("client_id", request.ClientId), ("redirect_uri", request.RedirectUri), ("response_type", request.ResponseType), ("scope", request.Scope), ("resource", string.Join(" ", request.GetResources())), ("state", request.State), ("code_challenge", request.CodeChallenge), ("code_challenge_method", request.CodeChallengeMethod) }.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => $"<input type='hidden' name='{x.Item1}' value='{WebUtility.HtmlEncode(x.Item2)}'/>") );
}
