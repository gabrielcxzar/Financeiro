using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Finflow.Api.ContractTests;

internal sealed class FinflowApiClient
{
    private readonly TestSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private string? _cachedToken;

    public FinflowApiClient()
    {
        _settings = TestSettings.Load();
    }

    public HttpClient CreateAnonymousClient()
    {
        return new HttpClient
        {
            BaseAddress = new Uri($"{_settings.BaseUrl}/api/")
        };
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        if (string.IsNullOrWhiteSpace(_cachedToken))
        {
            _cachedToken = await EnsureTokenAsync();
        }

        var client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
        return client;
    }

    private async Task<string> EnsureTokenAsync()
    {
        var client = CreateAnonymousClient();

        var loginResponse = await client.PostAsJsonAsync("auth/login", new
        {
            email = _settings.Email,
            password = _settings.Password
        });

        if (loginResponse.IsSuccessStatusCode)
        {
            return await ExtractTokenAsync(loginResponse);
        }

        var registerResponse = await client.PostAsJsonAsync("auth/register", new
        {
            name = _settings.Name,
            email = _settings.Email,
            password = _settings.Password
        });

        if (registerResponse.StatusCode != HttpStatusCode.OK &&
            registerResponse.StatusCode != HttpStatusCode.BadRequest)
        {
            var body = await registerResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Falha ao registrar usuario de teste. Status: {registerResponse.StatusCode}. Body: {body}");
        }

        var secondLogin = await client.PostAsJsonAsync("auth/login", new
        {
            email = _settings.Email,
            password = _settings.Password
        });

        secondLogin.EnsureSuccessStatusCode();
        return await ExtractTokenAsync(secondLogin);
    }

    private static async Task<string> ExtractTokenAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonObject>();
        return json?["token"]?.GetValue<string>()
            ?? throw new InvalidOperationException("Token nao encontrado na resposta de login.");
    }

    public async Task<JsonObject> ReadObjectAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonObject>(_jsonOptions);
        return payload ?? new JsonObject();
    }

    public async Task<JsonArray> ReadArrayAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonArray>(_jsonOptions);
        return payload ?? new JsonArray();
    }

    public async Task<int> CreateAccountAsync(HttpClient client, string nameSuffix, bool isCreditCard = false)
    {
        var response = await client.PostAsJsonAsync("accounts", new
        {
            name = $"Conta Teste {nameSuffix}",
            initialBalance = 1000m,
            currentBalance = 1000m,
            type = "Checking",
            isCreditCard,
            closingDay = isCreditCard ? 10 : (int?)null,
            dueDay = isCreditCard ? 20 : (int?)null,
            creditLimit = isCreditCard ? 5000m : (decimal?)null
        });

        response.EnsureSuccessStatusCode();
        var obj = await ReadObjectAsync(response);
        return obj["id"]!.GetValue<int>();
    }

    public async Task<int> CreateCategoryAsync(HttpClient client, string nameSuffix, string type = "Expense")
    {
        var response = await client.PostAsJsonAsync("categories", new
        {
            name = $"Categoria Teste {nameSuffix}",
            type,
            icon = "tag",
            color = "#123456"
        });

        response.EnsureSuccessStatusCode();
        var obj = await ReadObjectAsync(response);
        return obj["id"]!.GetValue<int>();
    }

    public async Task<int> CreateTransactionAsync(HttpClient client, int accountId, int? categoryId, string nameSuffix)
    {
        var response = await client.PostAsJsonAsync("transactions", new
        {
            description = $"Transacao Teste {nameSuffix}",
            amount = 25.5m,
            date = DateTime.UtcNow,
            type = "Expense",
            paid = true,
            categoryId,
            accountId,
            installments = 1
        });

        response.EnsureSuccessStatusCode();
        var obj = await ReadObjectAsync(response);
        return obj["id"]!.GetValue<int>();
    }

    public async Task<int> CreateRecurringAsync(HttpClient client, int accountId, int? categoryId, string nameSuffix)
    {
        var response = await client.PostAsJsonAsync("recurring", new
        {
            description = $"Recorrencia Teste {nameSuffix}",
            amount = 89m,
            type = "Expense",
            dayOfMonth = 5,
            active = true,
            categoryId,
            accountId
        });

        response.EnsureSuccessStatusCode();
        var obj = await ReadObjectAsync(response);
        return obj["id"]!.GetValue<int>();
    }

    public async Task<int> CreateBudgetAsync(HttpClient client, int categoryId, decimal amount = 500m)
    {
        var response = await client.PostAsJsonAsync("budgets", new
        {
            categoryId,
            amount
        });

        response.EnsureSuccessStatusCode();
        var obj = await ReadObjectAsync(response);
        return obj["id"]?.GetValue<int>() ?? 0;
    }

    public async Task<int> UpsertHoldingAsync(HttpClient client, string ticker)
    {
        var response = await client.PostAsJsonAsync("fiiholdings", new
        {
            ticker,
            shares = 10m,
            avgPrice = 98.5m,
            notes = "Posicao de teste"
        });

        response.EnsureSuccessStatusCode();
        var obj = await ReadObjectAsync(response);
        return obj["id"]!.GetValue<int>();
    }

    public HttpContent BuildCsvContent()
    {
        var csv = new StringBuilder();
        csv.AppendLine("Data,Valor,Ignorar,Descricao");
        csv.AppendLine($"{DateTime.UtcNow:dd/MM/yyyy},-54.90,x,Compra QA");

        return new StringContent(csv.ToString(), Encoding.UTF8, "text/csv");
    }
}

