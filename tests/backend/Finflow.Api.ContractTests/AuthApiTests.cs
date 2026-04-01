using System.Net;
using System.Net.Http.Json;

namespace Finflow.Api.ContractTests;

public class AuthApiTests
{
    [Fact]
    public async Task Register_WithNewUser_ReturnsOk()
    {
        var api = new FinflowApiClient();
        using var client = api.CreateAnonymousClient();
        var email = $"qa.auth.{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("auth/register", new
        {
            name = "QA Register",
            email,
            password = "SenhaForte123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = api.CreateAnonymousClient();
        var email = $"qa.dup.{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("auth/register", new { name = "QA", email, password = "SenhaForte123!" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync("auth/register", new { name = "QA", email, password = "SenhaForte123!" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var api = new FinflowApiClient();
        using var client = api.CreateAnonymousClient();
        var settings = TestSettings.Load();

        var response = await client.PostAsJsonAsync("auth/login", new
        {
            email = settings.Email,
            password = settings.Password
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await api.ReadObjectAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(payload["token"]?.GetValue<string>()));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = api.CreateAnonymousClient();
        var settings = TestSettings.Load();

        var response = await client.PostAsJsonAsync("auth/login", new
        {
            email = settings.Email,
            password = "senha-invalida"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

