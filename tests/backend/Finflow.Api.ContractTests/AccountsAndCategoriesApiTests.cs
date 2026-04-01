using System.Net;
using System.Net.Http.Json;

namespace Finflow.Api.ContractTests;

public class AccountsAndCategoriesApiTests
{
    [Fact]
    public async Task GetAccounts_Authorized_ReturnsOk()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostAndDeleteAccount_ReturnsExpectedStatuses()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var accountId = await api.CreateAccountAsync(client, Guid.NewGuid().ToString("N"));

        var delete = await client.DeleteAsync($"accounts/{accountId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task AdjustBalance_WithInvalidAccount_ReturnsNotFound()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("accounts/adjust-balance", new
        {
            accountId = -1,
            newBalance = 500m
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCategories_Authorized_ReturnsOk()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("categories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostAndDeleteCategory_ReturnsExpectedStatuses()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var categoryId = await api.CreateCategoryAsync(client, Guid.NewGuid().ToString("N"));

        var delete = await client.DeleteAsync($"categories/{categoryId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_InUse_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var accountId = await api.CreateAccountAsync(client, suffix);
        var categoryId = await api.CreateCategoryAsync(client, suffix);
        var transactionId = await api.CreateTransactionAsync(client, accountId, categoryId, suffix);

        var delete = await client.DeleteAsync($"categories/{categoryId}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        await client.DeleteAsync($"transactions/{transactionId}?deleteAll=false");
        await client.DeleteAsync($"categories/{categoryId}");
        await client.DeleteAsync($"accounts/{accountId}");
    }
}

