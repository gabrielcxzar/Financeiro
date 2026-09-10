using Microsoft.AspNetCore.Mvc;

namespace MyFinance.API.Controllers;

[ApiController]
public sealed class McpMetadataController(IConfiguration configuration) : ControllerBase
{
    [HttpGet("/.well-known/oauth-protected-resource")]
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
