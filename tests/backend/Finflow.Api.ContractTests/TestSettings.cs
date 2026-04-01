namespace Finflow.Api.ContractTests;

internal sealed class TestSettings
{
    public string BaseUrl { get; }
    public string Email { get; }
    public string Password { get; }
    public string Name { get; }

    private TestSettings(string baseUrl, string email, string password, string name)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        Email = email;
        Password = password;
        Name = name;
    }

    public static TestSettings Load()
    {
        string Read(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Variavel de ambiente obrigatoria ausente: {key}");
            }

            return value;
        }

        return new TestSettings(
            Read("FINFLOW_TEST_BASE_URL"),
            Read("FINFLOW_TEST_EMAIL"),
            Read("FINFLOW_TEST_PASSWORD"),
            Environment.GetEnvironmentVariable("FINFLOW_TEST_NAME") ?? "QA Finflow");
    }
}

