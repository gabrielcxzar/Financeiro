namespace MyFinance.API.Mcp;

public sealed class McpRateLimitException : Exception
{
    public McpRateLimitException() : base("RATE_LIMITED") { }
}

public sealed class McpDetailRateLimiter
{
    private readonly object gate = new();
    private readonly Dictionary<string, (DateTime Window, int Count)> counters = new(StringComparer.Ordinal);
    private readonly int limit;

    public McpDetailRateLimiter(IConfiguration configuration) => limit = Math.Max(1, configuration.GetValue("Mcp:DetailRateLimitPerMinute", 20));

    public bool TryAcquire(string subject)
    {
        var now = DateTime.UtcNow;
        lock (gate)
        {
            if (!counters.TryGetValue(subject, out var value) || now - value.Window >= TimeSpan.FromMinutes(1))
            {
                counters[subject] = (now, 1);
                return true;
            }
            if (value.Count >= limit) return false;
            counters[subject] = (value.Window, value.Count + 1);
            return true;
        }
    }
}
