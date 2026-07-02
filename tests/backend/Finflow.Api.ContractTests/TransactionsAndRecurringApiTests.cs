using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Finflow.Api.ContractTests;

public class TransactionsAndRecurringApiTests
{
    [Fact]
    public async Task GetTransactions_WithMonthFilter_ReturnsOk()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var now = DateTime.UtcNow;
        var response = await client.GetAsync($"transactions?month={now.Month}&year={now.Year}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostPutDeleteTransaction_ReturnsExpectedStatuses()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var accountId = await api.CreateAccountAsync(client, suffix);
        var categoryId = await api.CreateCategoryAsync(client, suffix);
        var transactionId = await api.CreateTransactionAsync(client, accountId, categoryId, suffix);

        var put = await client.PutAsJsonAsync($"transactions/{transactionId}", new
        {
            id = transactionId,
            description = $"Transacao Atualizada {suffix}",
            amount = 33m,
            date = DateTime.UtcNow,
            type = "Expense",
            paid = true,
            categoryId,
            accountId
        });

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var delete = await client.DeleteAsync($"transactions/{transactionId}?deleteAll=false");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await client.DeleteAsync($"categories/{categoryId}");
        await client.DeleteAsync($"accounts/{accountId}");
    }

    [Fact]
    public async Task PostTransaction_WithCurrentInstallment_CreatesRemainingInstallmentsOnly()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var suffix = Guid.NewGuid().ToString("N");
        var accountId = await api.CreateAccountAsync(client, suffix);
        var categoryId = await api.CreateCategoryAsync(client, suffix);
        var baseDate = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

        var post = await client.PostAsJsonAsync("transactions", new
        {
            description = $"Compra Parcelada Teste {suffix}",
            amount = 71.73m,
            date = baseDate,
            type = "Expense",
            paid = true,
            categoryId,
            accountId,
            installmentNumber = 3,
            totalInstallments = 6
        });

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var month6 = await GetTransactionsForMonthAsync(client, 6, 2026);
        var month7 = await GetTransactionsForMonthAsync(client, 7, 2026);
        var month8 = await GetTransactionsForMonthAsync(client, 8, 2026);
        var month9 = await GetTransactionsForMonthAsync(client, 9, 2026);

        Assert.Contains(month6, t => t.Description == $"Compra Parcelada Teste {suffix} (3/6)");
        Assert.Contains(month7, t => t.Description == $"Compra Parcelada Teste {suffix} (4/6)");
        Assert.Contains(month8, t => t.Description == $"Compra Parcelada Teste {suffix} (5/6)");
        Assert.Contains(month9, t => t.Description == $"Compra Parcelada Teste {suffix} (6/6)");

        var installmentIds = month6.Concat(month7).Concat(month8).Concat(month9)
            .Where(t => t.Description.Contains(suffix, StringComparison.Ordinal))
            .Select(t => t.InstallmentId)
            .Distinct()
            .ToList();

        Assert.Single(installmentIds);

        var deleteAll = month6.First(t => t.Description == $"Compra Parcelada Teste {suffix} (3/6)");
        var delete = await client.DeleteAsync($"transactions/{deleteAll.Id}?deleteAll=true");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await client.DeleteAsync($"categories/{categoryId}");
        await client.DeleteAsync($"accounts/{accountId}");
    }

    [Fact]
    public async Task PutTransaction_WithIdMismatch_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("transactions/9999", new
        {
            id = 8888,
            description = "Invalida",
            amount = 1m,
            date = DateTime.UtcNow,
            type = "Expense",
            paid = true,
            accountId = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_WithInvalidAccounts_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("transactions/transfer", new
        {
            fromAccountId = -1,
            toAccountId = -2,
            amount = 10m,
            date = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invoice_WithInvalidAccount_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var now = DateTime.UtcNow;

        var response = await client.GetAsync($"transactions/invoice?accountId=-1&month={now.Month}&year={now.Year}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetRecurringAndProjection_ReturnOk()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var now = DateTime.UtcNow;

        var recurring = await client.GetAsync("recurring");
        var projection = await client.GetAsync($"recurring/projection?months=3&startMonth={now.Month}&startYear={now.Year}");

        Assert.Equal(HttpStatusCode.OK, recurring.StatusCode);
        Assert.Equal(HttpStatusCode.OK, projection.StatusCode);
    }

    [Fact]
    public async Task PostRecurring_WithoutAccount_ReturnsBadRequest()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("recurring", new
        {
            description = "Sem conta",
            amount = 50m,
            type = "Expense",
            dayOfMonth = 1,
            active = true,
            categoryId = (int?)null,
            accountId = (int?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostGenerateAndDeleteRecurring_ReturnExpectedStatuses()
    {
        var api = new FinflowApiClient();
        using var client = await api.CreateAuthenticatedClientAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        var accountId = await api.CreateAccountAsync(client, suffix);
        var categoryId = await api.CreateCategoryAsync(client, suffix);
        var recurringId = await api.CreateRecurringAsync(client, accountId, categoryId, suffix);

        var generate = await client.PostAsync($"recurring/generate?month={now.Month}&year={now.Year}", null);
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        var delete = await client.DeleteAsync($"recurring/{recurringId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await client.DeleteAsync($"categories/{categoryId}");
        await client.DeleteAsync($"accounts/{accountId}");
    }

    private static async Task<List<TransactionListItem>> GetTransactionsForMonthAsync(HttpClient client, int month, int year)
    {
        var response = await client.GetAsync($"transactions?month={month}&year={year}");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        var transactions = await JsonSerializer.DeserializeAsync<List<TransactionListItem>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return transactions ?? new List<TransactionListItem>();
    }

    private sealed class TransactionListItem
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? InstallmentId { get; set; }
    }
}

