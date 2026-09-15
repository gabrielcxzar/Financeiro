using Microsoft.AspNetCore.Authentication.JwtBearer;
using MyFinance.API.Mcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyFinance.API.Data;
using MyFinance.API.Services;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System.Threading.RateLimiting;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
var renderPort = Environment.GetEnvironmentVariable("PORT");
var shouldRunSchemaBootstrap = builder.Configuration.GetValue<bool?>("RunSchemaBootstrap")
    ?? builder.Environment.IsDevelopment();

if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{renderPort}");
}
else
{
    // Local/containers sem PORT injetada.
    builder.WebHost.UseUrls("http://0.0.0.0:10000");
}

builder.Services.AddControllers();
builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFinancialInsightsService, FinancialInsightsService>();
builder.Services.AddSingleton<McpDetailRateLimiter>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<FinflowMcpTools>();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Finflow.API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT assim: Bearer {seu_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection nao configurada.");

void ConfigureDatabase(DbContextOptionsBuilder options) =>
    options.UseNpgsql(
        defaultConnection,
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            npgsql.CommandTimeout(30);
        }
    );

builder.Services.AddDbContext<AppDbContext>(ConfigureDatabase);
builder.Services.AddScoped<IFinancialSnapshotService, FinancialSnapshotService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("McpSubject", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Mcp:RateLimitPerMinute", 60), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var token = builder.Configuration["AppSettings:Token"]
    ?? throw new InvalidOperationException("AppSettings:Token nao configurada.");
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("AppSettings:Token deve ser fornecida por secret store ou variável de ambiente.");

var key = Encoding.ASCII.GetBytes(token);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
})
.AddCookie("McpOAuthCookie", options =>
{
    options.Cookie.Name = "finflow_mcp_auth";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    options.SlidingExpiration = false;
    options.LoginPath = "/oauth/authorize";
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpRead", policy =>
        policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx => ctx.User.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Contains("finflow.read", StringComparer.Ordinal)));
});

var mcpIssuer = (builder.Configuration["McpOAuth:Issuer"] ?? "https://localhost:10000/").TrimEnd('/') + "/";
var mcpResource = mcpIssuer.TrimEnd('/') + "/mcp";

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(mcpIssuer));
        options.SetAuthorizationEndpointUris("/oauth/authorize");
        options.SetTokenEndpointUris("/oauth/token");
        options.SetRevocationEndpointUris("/oauth/revoke");
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowRefreshTokenFlow();
        options.RegisterScopes("finflow.read");
        options.UseReferenceAccessTokens().UseReferenceRefreshTokens();
        // OpenIddict mantém rolling refresh tokens habilitado por padrão; não desabilitar.
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
        if (builder.Environment.IsDevelopment())
        {
            options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
        }
        else
        {
            var encryptionPath = builder.Configuration["McpOAuth:EncryptionCertificatePath"];
            var signingPath = builder.Configuration["McpOAuth:SigningCertificatePath"];
            var encryptionBase64 = builder.Configuration["McpOAuth:EncryptionCertificateBase64"];
            var signingBase64 = builder.Configuration["McpOAuth:SigningCertificateBase64"];
            var password = builder.Configuration["McpOAuth:CertificatePassword"];
            if (string.IsNullOrWhiteSpace(password) || (string.IsNullOrWhiteSpace(encryptionPath) && string.IsNullOrWhiteSpace(encryptionBase64)) || (string.IsNullOrWhiteSpace(signingPath) && string.IsNullOrWhiteSpace(signingBase64)))
                throw new InvalidOperationException("MCP OAuth production certificates are not configured.");
            options.AddEncryptionCertificate(LoadCertificate(encryptionPath, encryptionBase64, password, "encryption"));
            options.AddSigningCertificate(LoadCertificate(signingPath, signingBase64, password, "signing"));
        }
        options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.SetIssuer(new Uri(mcpIssuer));
        options.AddAudiences(mcpResource);
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", cors =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();

        if (allowedOrigins is { Length: > 0 })
        {
            cors
                .WithOrigins(allowedOrigins)
                .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                .WithHeaders("Content-Type", "Authorization");

            return;
        }

        cors
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception exception) when (context.Request.Path.StartsWithSegments("/mcp"))
    {
        if (context.Response.HasStarted) throw;
        var code = exception switch
        {
            McpRateLimitException => "RATE_LIMITED",
            UnauthorizedAccessException => "AUTHENTICATION_REQUIRED",
            ArgumentException argument => argument.Message,
            _ => "INTERNAL_ERROR"
        };
        if (code is not ("PERIOD_TOO_LARGE" or "INVALID_CURSOR" or "PAGE_LIMIT_EXCEEDED" or "INVALID_ARGUMENT" or "RATE_LIMITED" or "AUTHENTICATION_REQUIRED")) code = "INTERNAL_ERROR";
        context.Response.StatusCode = code == "INTERNAL_ERROR" ? 500 : code == "RATE_LIMITED" ? 429 : code == "AUTHENTICATION_REQUIRED" ? 401 : 400;
        if (code == "RATE_LIMITED") context.Response.Headers.RetryAfter = "60";
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = new { code, message = code switch { "INTERNAL_ERROR" => "Ocorreu um erro interno.", "AUTHENTICATION_REQUIRED" => "Autenticação necessária.", "RATE_LIMITED" => "Limite de chamadas excedido.", _ => "Parâmetro inválido." }, correlationId = context.TraceIdentifier, retryable = code == "RATE_LIMITED" } });
    }
});

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/mcp"))
    {
        await next();
        return;
    }

    var correlationId = context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    var subject = context.User.FindFirst("sub")?.Value ?? "anonymous";
    var pseudonym = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(subject)))[..12];
    var started = Stopwatch.GetTimestamp();
    try
    {
        await next();
        var elapsed = Stopwatch.GetElapsedTime(started);
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("McpRequest").LogInformation("MCP request correlationId={CorrelationId} status={StatusCode} durationMs={DurationMs} subject={Subject} responseBytes={ResponseBytes}", correlationId, context.Response.StatusCode, elapsed.TotalMilliseconds, pseudonym, context.Response.ContentLength ?? 0);
    }
    catch
    {
        var elapsed = Stopwatch.GetElapsedTime(started);
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("McpRequest").LogInformation("MCP request correlationId={CorrelationId} status=error durationMs={DurationMs} subject={Subject}", correlationId, elapsed.TotalMilliseconds, pseudonym);
        throw;
    }
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp"))
    {
        async Task RejectAsync(int statusCode, string code, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code,
                    message,
                    correlationId = context.TraceIdentifier,
                    retryable = false
                }
            });
        }

        var issuer = builder.Configuration["McpOAuth:Issuer"]?.TrimEnd('/') ?? "https://localhost:10000";
        var expectedHost = Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri) ? issuerUri.Host : string.Empty;
        var allowedHosts = builder.Configuration.GetSection("Mcp:AllowedHosts").Get<string[]>() ?? [];
        if (string.IsNullOrWhiteSpace(context.Request.Host.Host) ||
            (!string.Equals(context.Request.Host.Host, expectedHost, StringComparison.OrdinalIgnoreCase) &&
             !allowedHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase)))
        {
            await RejectAsync(StatusCodes.Status421MisdirectedRequest, "INVALID_HOST", "Host não permitido.");
            return;
        }

        var maxRequestBytes = builder.Configuration.GetValue("Mcp:MaxRequestBytes", 256 * 1024);
        var maxBodyFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
        if (maxBodyFeature is { IsReadOnly: false })
            maxBodyFeature.MaxRequestBodySize = maxRequestBytes;
        if (context.Request.ContentLength is > 0 and var contentLength && contentLength > maxRequestBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new { error = new { code = "REQUEST_TOO_LARGE", message = "Requisição acima do limite permitido.", correlationId = context.TraceIdentifier, retryable = false } });
            return;
        }

        var origin = context.Request.Headers.Origin.ToString();
        var allowedOrigins = builder.Configuration.GetSection("Mcp:AllowedOrigins").Get<string[]>() ?? [];
        if (!string.IsNullOrWhiteSpace(origin) && !allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            await RejectAsync(StatusCodes.Status403Forbidden, "ORIGIN_NOT_ALLOWED", "Origin não permitido.");
            return;
        }

        if (context.Request.Headers.ContainsKey("Authorization"))
        {
            await next();
            return;
        }

        context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{issuer}/.well-known/oauth-protected-resource\"";
        await RejectAsync(StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "Autenticação necessária.");
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapMcp("/mcp").RequireAuthorization("McpRead").RequireRateLimiting("McpSubject");

if (shouldRunSchemaBootstrap)
{
    await ApplyMigrationsAsync(app);
}
else
{
    app.Logger.LogInformation("Automatic database migration disabled for this environment.");
}

await EnsureMcpClientRegistrationAsync(app, mcpResource);

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");

    logger.LogInformation("Applying EF Core migrations...");
    await db.Database.MigrateAsync();
    logger.LogInformation("EF Core migrations applied.");
}

static async Task EnsureMcpClientRegistrationAsync(WebApplication app, string mcpResource)
{
    var configuration = app.Configuration;
    var clientId = configuration["McpOAuth:ClientId"];
    var redirectUris = configuration.GetSection("McpOAuth:RedirectUris").Get<string[]>() ?? [];
    if (string.IsNullOrWhiteSpace(clientId) || redirectUris.Length == 0)
    {
        app.Logger.LogDebug("MCP OAuth client registration is not configured. Set McpOAuth__ClientId and McpOAuth__RedirectUris before enabling ChatGPT.");
        return;
    }

    if (clientId.Length > 200 || clientId.Any(char.IsControl) || redirectUris.Any(uri =>
            !Uri.TryCreate(uri, UriKind.Absolute, out var redirect) ||
            redirect.Scheme != Uri.UriSchemeHttps))
        throw new InvalidOperationException("McpOAuth client id must be a safe opaque identifier and redirect URIs must use HTTPS.");

    using var scope = app.Services.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    if (await manager.FindByClientIdAsync(clientId) is not null) return;

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        ClientType = OpenIddictConstants.ClientTypes.Public,
        ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
        DisplayName = "ChatGPT MCP",
        ApplicationType = OpenIddictConstants.ApplicationTypes.Web
    };
    foreach (var uri in redirectUris) descriptor.RedirectUris.Add(new Uri(uri));
    descriptor.Permissions.UnionWith([
        OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Token,
        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
        OpenIddictConstants.Permissions.ResponseTypes.Code,
        OpenIddictConstants.Permissions.Prefixes.Scope + "finflow.read",
        OpenIddictConstants.Permissions.Prefixes.Resource + mcpResource
    ]);
    await manager.CreateAsync(descriptor);
    app.Logger.LogInformation("Registered configured MCP OAuth client {ClientId}.", clientId);
}

static X509Certificate2 LoadCertificate(string? path, string? base64, string password, string purpose)
{
    try
    {
        return !string.IsNullOrWhiteSpace(path)
            ? new X509Certificate2(path, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet)
            : new X509Certificate2(Convert.FromBase64String(base64!), password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
    }
    catch (Exception exception) when (exception is CryptographicException or FormatException or IOException)
    {
        throw new InvalidOperationException($"MCP OAuth {purpose} certificate could not be loaded.", exception);
    }
}

