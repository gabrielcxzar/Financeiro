using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFinance.API.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

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
        if (!auth.Succeeded) return Content(LoginForm(request), "text/html");
        return Content(ConsentForm(request), "text/html");
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
        return $"<html><body><h1>FinFlow — Autorizar ChatGPT</h1><p>Entre para continuar.</p><form method='post' action='/oauth/authorize'>{Hidden(request)}<input type='hidden' name='__RequestVerificationToken' value='{token}'/><input name='email' type='email' required/><input name='password' type='password' required/><input type='hidden' name='consent' value='accept'/><button type='submit'>Continuar</button></form></body></html>";
    }

    private string ConsentForm(OpenIddictRequest request)
    {
        var token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken;
        return $"<html><body><h1>Permitir acesso?</h1><p>Permitir que o ChatGPT consulte seus dados financeiros do FinFlow em modo somente leitura.</p><form method='post' action='/oauth/authorize'>{Hidden(request)}<input type='hidden' name='__RequestVerificationToken' value='{token}'/><button type='submit' name='consent' value='accept'>Aceitar</button><button type='submit' name='consent' value='deny'>Negar</button></form></body></html>";
    }

    private static string BuildAuthorizeUri(OpenIddictRequest request)
    {
        var values = new[] { ("client_id", request.ClientId), ("redirect_uri", request.RedirectUri), ("response_type", request.ResponseType), ("scope", request.Scope), ("resource", string.Join(" ", request.GetResources())), ("state", request.State), ("code_challenge", request.CodeChallenge), ("code_challenge_method", request.CodeChallengeMethod) };
        return "/oauth/authorize?" + string.Join("&", values.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => $"{x.Item1}={Uri.EscapeDataString(x.Item2!)}"));
    }

    private static string Hidden(OpenIddictRequest request) => string.Join("", new[] { ("client_id", request.ClientId), ("redirect_uri", request.RedirectUri), ("response_type", request.ResponseType), ("scope", request.Scope), ("resource", string.Join(" ", request.GetResources())), ("state", request.State), ("code_challenge", request.CodeChallenge), ("code_challenge_method", request.CodeChallengeMethod) }.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => $"<input type='hidden' name='{x.Item1}' value='{System.Net.WebUtility.HtmlEncode(x.Item2)}'/>") );
}
