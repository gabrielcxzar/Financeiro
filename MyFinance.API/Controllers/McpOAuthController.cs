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
public sealed class McpOAuthController(AppDbContext db, IAntiforgery antiforgery) : Controller
{
    [HttpGet("/oauth/authorize")]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext) ?? throw new InvalidOperationException("OAuth request ausente.");
        var auth = await HttpContext.AuthenticateAsync("McpOAuthCookie");
        if (!auth.Succeeded) return Content(LoginForm(request), "text/html");
        return Content(ConsentForm(request), "text/html");
    }

    [HttpPost("/oauth/authorize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AuthorizePost(string email, string password, string consent, CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext) ?? throw new InvalidOperationException("OAuth request ausente.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return Unauthorized("Credenciais inválidas.");
        if (!string.Equals(consent, "accept", StringComparison.Ordinal))
            return BadRequest("Acesso negado pelo usuário.");

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
        identity.AddClaim(OpenIddictConstants.Claims.Name, user.Name);
        identity.SetScopes("finflow.read");
        identity.SetResources("finflow");
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes("finflow.read");
        principal.SetResources("finflow");
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("/oauth/token")]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        var request = Microsoft.AspNetCore.OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest(HttpContext) ?? throw new InvalidOperationException("OAuth request ausente.");
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            return BadRequest(new { error = "unsupported_grant_type" });
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null) return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        principal.SetScopes("finflow.read");
        principal.SetResources("finflow");
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
        {
            var subject = await manager.GetSubjectAsync(token, cancellationToken);
            var client = await manager.GetApplicationIdAsync(token, cancellationToken);
            await manager.RevokeAsync(subject, client, null, null, cancellationToken);
        }
        return Ok();
    }

    private string LoginForm(OpenIddictRequest request)
    {
        var token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken;
        return $"<html><body><h1>FinFlow — Autorizar ChatGPT</h1><p>Entre para continuar.</p><form method='post' action='/oauth/authorize'>{Hidden(request)}<input type='hidden' name='__RequestVerificationToken' value='{token}'/><input name='email' type='email' required/><input name='password' type='password' required/><input type='hidden' name='consent' value='accept'/><button type='submit'>Continuar</button></form></body></html>";
    }

    private string ConsentForm(OpenIddictRequest request)
        => $"<html><body><h1>Permitir acesso?</h1><p>Permitir que o ChatGPT consulte seus dados financeiros do FinFlow em modo somente leitura.</p><form method='post' action='/oauth/authorize'>{Hidden(request)}<input type='hidden' name='consent' value='accept'/><button type='submit'>Aceitar</button></form></body></html>";

    private static string Hidden(OpenIddictRequest request) => string.Join("", new[] { ("client_id", request.ClientId), ("redirect_uri", request.RedirectUri), ("response_type", request.ResponseType), ("scope", request.Scope), ("state", request.State), ("code_challenge", request.CodeChallenge), ("code_challenge_method", request.CodeChallengeMethod) }.Where(x => !string.IsNullOrEmpty(x.Item2)).Select(x => $"<input type='hidden' name='{x.Item1}' value='{System.Net.WebUtility.HtmlEncode(x.Item2)}'/>") );
}
