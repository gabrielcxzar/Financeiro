using Microsoft.AspNetCore.Mvc;

namespace MyFinance.API.Controllers;

[ApiController]
public sealed class McpMetadataController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("/.well-known/oauth-authorization-server")]
    public IActionResult AuthorizationServer()
    {
        var issuer = (configuration["McpOAuth:Issuer"] ?? throw new InvalidOperationException("McpOAuth:Issuer não configurado.")).TrimEnd('/') + "/";
        return Ok(new
        {
            issuer,
            authorization_endpoint = new Uri(new Uri(issuer), "oauth/authorize").ToString(),
            token_endpoint = new Uri(new Uri(issuer), "oauth/token").ToString(),
            revocation_endpoint = new Uri(new Uri(issuer), "oauth/revoke").ToString(),
            response_types_supported = new[] { "code" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            code_challenge_methods_supported = new[] { "S256" },
            // offline_access é o escopo OAuth padrão que habilita refresh token;
            // não concede acesso adicional aos dados financeiros.
            scopes_supported = new[] { "finflow.read", "offline_access" },
            token_endpoint_auth_methods_supported = new[] { "none" }
        });
    }

    [HttpGet("/.well-known/oauth-protected-resource")]
    [HttpGet("/.well-known/oauth-protected-resource/mcp")]
    public IActionResult ProtectedResource()
    {
        var issuer = configuration["McpOAuth:Issuer"] ?? throw new InvalidOperationException("McpOAuth:Issuer não configurado.");
        return Ok(new
        {
            resource = new Uri(new Uri(issuer), "mcp").ToString().TrimEnd('/'),
            authorization_servers = new[] { issuer.TrimEnd('/') },
            scopes_supported = new[] { "finflow.read" },
            bearer_methods_supported = new[] { "header" }
        });
    }
}
