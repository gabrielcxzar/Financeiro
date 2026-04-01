using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Finflow.Api.ContractTests;

public class UsersBudgetsInvestmentsImportApiTests
{
    [Fact]
    public async Task GetMe_ReturnsUserPayload()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPostDeleteBudget_ReturnExpectedStatuses()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var categoryId = await api.CreateCategoryAsync(client, Guid.NewGuid().ToString("N"));
        var budgetId = await api.CreateBudgetAsync(client, categoryId);

        var get = await client.GetAsync("budgets");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        if (budgetId > 0)
        {
            var delete = await client.DeleteAsync($"budgets/{budgetId}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        await client.DeleteAsync($"categories/{categoryId}");
    }

    [Fact]
    public async Task DeleteBudget_NotFound_ReturnsNotFound()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("budgets/-1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TesouroLatest_ReturnsOk()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("tesouro/latest");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FiiHoldings_GetPostDelete_ReturnExpectedStatuses()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var get = await client.GetAsync("fiiholdings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var holdingId = await api.UpsertHoldingAsync(client, $"HGLG{Random.Shared.Next(10, 99)}");
        var delete = await client.DeleteAsync($"fiiholdings/{holdingId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DeleteHolding_NotFound_ReturnsNotFound()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync("fiiholdings/-1");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportUpload_WithoutFile_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var accountId = await api.CreateAccountAsync(client, Guid.NewGuid().ToString("N"));

        using var form = new MultipartFormDataContent();
        var response = await client.PostAsync($"import/upload?accountId={accountId}", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await client.DeleteAsync($"accounts/{accountId}");
    }

    [Fact]
    public async Task ImportUpload_WithCsv_ReturnsOk()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var accountId = await api.CreateAccountAsync(client, Guid.NewGuid().ToString("N"));

        using var form = new MultipartFormDataContent();
        var fileContent = api.BuildCsvContent();
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "file", "extrato.csv");

        var response = await client.PostAsync($"import/upload?accountId={accountId}", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await client.DeleteAsync($"accounts/{accountId}");
    }
}

