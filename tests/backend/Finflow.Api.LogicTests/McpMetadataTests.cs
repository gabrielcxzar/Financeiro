using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MyFinance.API.Controllers;

namespace Finflow.Api.LogicTests;

public sealed class McpMetadataTests
{
    [Fact]
    public void ProtectedResourceMetadata_UsesCanonicalMcpResource()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["McpOAuth:Issuer"] = "https://finflow.example/" }).Build();
        var result = (OkObjectResult)new McpMetadataController(configuration).ProtectedResource();
        var resource = result.Value!.GetType().GetProperty("resource")!.GetValue(result.Value)!.ToString();
        Assert.Equal("https://finflow.example/mcp", resource);
    }

    [Fact]
    public void AuthorizationServerMetadata_AdvertisesPkceAndOnlySupportedGrants()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["McpOAuth:Issuer"] = "https://finflow.example/" }).Build();
        var result = (OkObjectResult)new McpMetadataController(configuration).AuthorizationServer();
        var value = result.Value!;
        Assert.Contains("S256", (IEnumerable<string>)value.GetType().GetProperty("code_challenge_methods_supported")!.GetValue(value)!);
        Assert.Equal(new[] { "authorization_code", "refresh_token" }, (IEnumerable<string>)value.GetType().GetProperty("grant_types_supported")!.GetValue(value)!);
    }
}
