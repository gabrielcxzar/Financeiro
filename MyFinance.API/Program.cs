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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFinancialInsightsService, FinancialInsightsService>();
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

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AppDbContext>())
    .AddServer(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["McpOAuth:Issuer"] ?? "https://localhost:10000/"));
        options.SetAuthorizationEndpointUris("/oauth/authorize");
        options.SetTokenEndpointUris("/oauth/token");
        options.SetRevocationEndpointUris("/oauth/revoke");
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowRefreshTokenFlow();
        options.AcceptAnonymousClients();
        options.RegisterScopes("finflow.read");
        options.UseReferenceAccessTokens().UseReferenceRefreshTokens();
        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();
        options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.SetIssuer(new Uri(builder.Configuration["McpOAuth:Issuer"] ?? "https://localhost:10000/"));
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

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var correlationId = context.TraceIdentifier;
    var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var code = feature?.Error is ArgumentException a ? a.Message : "INTERNAL_ERROR";
    if (code is not ("PERIOD_TOO_LARGE" or "INVALID_CURSOR" or "PAGE_LIMIT_EXCEEDED")) code = "INTERNAL_ERROR";
    context.Response.StatusCode = code == "INTERNAL_ERROR" ? 500 : 400;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new { error = new { code, message = code == "INTERNAL_ERROR" ? "Ocorreu um erro interno." : "Parâmetro inválido.", correlationId, retryable = false } });
}));

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp") &&
        !context.Request.Headers.ContainsKey("Authorization"))
    {
        var issuer = builder.Configuration["McpOAuth:Issuer"]?.TrimEnd('/') ?? "https://localhost:10000";
        context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{issuer}/.well-known/oauth-protected-resource\"";
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
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

