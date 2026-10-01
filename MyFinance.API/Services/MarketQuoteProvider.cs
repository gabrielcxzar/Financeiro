using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MyFinance.API.Services;

public sealed record MarketQuote(string Ticker, decimal Price, DateTimeOffset AsOf, string Source);
public sealed record MarketQuoteResult(bool Success, MarketQuote? Quote, string? ErrorCode = null);

public interface IMarketQuoteProvider
{
    bool IsConfigured { get; }
    Task<MarketQuoteResult> GetQuoteAsync(string ticker, CancellationToken cancellationToken);

    async Task<IReadOnlyDictionary<string, MarketQuoteResult>> GetQuotesAsync(IReadOnlyCollection<string> tickers, CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, MarketQuoteResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var ticker in tickers.Distinct(StringComparer.OrdinalIgnoreCase))
            results[ticker] = await GetQuoteAsync(ticker, cancellationToken);
        return results;
    }
}

public sealed class BrapiMarketQuoteProvider(
    HttpClient httpClient,
    IMemoryCache cache,
    IConfiguration configuration) : IMarketQuoteProvider
{
    private const string Source = "brapi";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private readonly string? apiKey = configuration["BRAPI_API_KEY"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey);

    public async Task<MarketQuoteResult> GetQuoteAsync(string ticker, CancellationToken cancellationToken)
    {
        var results = await GetQuotesAsync([ticker], cancellationToken);
        return results[ticker.Trim().ToUpperInvariant()];
    }

    public async Task<IReadOnlyDictionary<string, MarketQuoteResult>> GetQuotesAsync(IReadOnlyCollection<string> tickers, CancellationToken cancellationToken)
    {
        var normalizedTickers = tickers.Select(x => x.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var results = new Dictionary<string, MarketQuoteResult>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var ticker in normalizedTickers)
        {
            if (cache.TryGetValue<MarketQuote>(CacheKey(ticker), out var cached) && cached is not null)
                results[ticker] = new(true, cached);
            else
                missing.Add(ticker);
        }

        if (missing.Count == 0) return results;
        if (!IsConfigured)
        {
            foreach (var ticker in missing) results[ticker] = new(false, null, "provider_not_configured");
            return results;
        }

        try
        {
            var symbols = string.Join(',', missing.Select(Uri.EscapeDataString));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://brapi.dev/api/quote/{symbols}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                foreach (var ticker in missing) results[ticker] = new(false, null, $"provider_http_{(int)response.StatusCode}");
                return results;
            }

            var payload = await response.Content.ReadFromJsonAsync<BrapiQuoteResponse>(cancellationToken: cancellationToken);
            foreach (var ticker in missing)
            {
                var quote = payload?.Results?.FirstOrDefault(x => string.Equals(x.Symbol, ticker, StringComparison.OrdinalIgnoreCase));
                if (quote?.RegularMarketPrice is not > 0 || quote.RegularMarketTime is null || quote.Currency != "BRL")
                {
                    results[ticker] = new(false, null, "provider_quote_unavailable");
                    continue;
                }

                var result = new MarketQuote(ticker, quote.RegularMarketPrice.Value, quote.RegularMarketTime.Value, Source);
                cache.Set(CacheKey(ticker), result, CacheDuration);
                results[ticker] = new(true, result);
            }
            return results;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            foreach (var ticker in missing) results[ticker] = new(false, null, "provider_timeout");
            return results;
        }
        catch (HttpRequestException)
        {
            foreach (var ticker in missing) results[ticker] = new(false, null, "provider_unavailable");
            return results;
        }
        catch (System.Text.Json.JsonException)
        {
            foreach (var ticker in missing) results[ticker] = new(false, null, "provider_invalid_response");
            return results;
        }
    }

    private static string CacheKey(string ticker) => $"market-quote:{Source}:{ticker}";

    private sealed record BrapiQuoteResponse(List<BrapiQuote>? Results);
    private sealed record BrapiQuote(string? Symbol, string? Currency, decimal? RegularMarketPrice, DateTimeOffset? RegularMarketTime);
}
