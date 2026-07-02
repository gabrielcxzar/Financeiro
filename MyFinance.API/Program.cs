using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyFinance.API.Data;
using MyFinance.API.Services;
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

var token = builder.Configuration["AppSettings:Token"]
    ?? throw new InvalidOperationException("AppSettings:Token nao configurada.");

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

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

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

