using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyFinance.API.Data;
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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        defaultConnection,
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            npgsql.CommandTimeout(30);
        }
    ));

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
    StartSchemaBootstrapInBackground(app);
}
else
{
    app.Logger.LogInformation("Schema bootstrap disabled for this environment.");
}

app.Run();

static void StartSchemaBootstrapInBackground(WebApplication app)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureDatabaseSchemaAsync(app);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Schema bootstrap failed in background.");
            }
        });
    });
}

static async Task EnsureDatabaseSchemaAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("SchemaBootstrap");

    const string fiiTableSql = @"
CREATE TABLE IF NOT EXISTS fii_holdings (
    id SERIAL PRIMARY KEY,
    ticker TEXT NOT NULL,
    shares NUMERIC(18,4) NOT NULL,
    avg_price NUMERIC(18,4) NOT NULL,
    notes TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    user_id INT NOT NULL
);
";

    const string fiiIndexSql = @"
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_fii_holdings_user_ticker
ON fii_holdings (user_id, ticker);
";

    const string recurringTableSql = @"
CREATE TABLE IF NOT EXISTS recurring_transactions (
    id SERIAL PRIMARY KEY,
    description VARCHAR(100) NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    type VARCHAR(10) NOT NULL,
    day_of_month INT NOT NULL,
    category_id INT NULL,
    account_id INT NULL,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    user_id INT NULL
);
";

    const string recurringCompatSql = @"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'categoryid'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'category_id'
    ) THEN
        ALTER TABLE recurring_transactions RENAME COLUMN categoryid TO category_id;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'accountid'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'account_id'
    ) THEN
        ALTER TABLE recurring_transactions RENAME COLUMN accountid TO account_id;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'day_of_month'
    ) THEN
        ALTER TABLE recurring_transactions ADD COLUMN day_of_month INT NOT NULL DEFAULT 1;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'active'
    ) THEN
        ALTER TABLE recurring_transactions ADD COLUMN active BOOLEAN NOT NULL DEFAULT TRUE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'recurring_transactions' AND column_name = 'user_id'
    ) THEN
        ALTER TABLE recurring_transactions ADD COLUMN user_id INT NULL;
    END IF;
END $$;
";

    const string recurringIndexUserSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_recurring_transactions_user_id
ON recurring_transactions (user_id);
";

    const string recurringIndexUserActiveSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_recurring_transactions_user_active
ON recurring_transactions (user_id, active);
";

    var originalTimeout = db.Database.GetCommandTimeout();
    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

    try
    {
        logger.LogInformation("Running schema bootstrap...");

        // Required schema pieces for core endpoints.
        await db.Database.ExecuteSqlRawAsync(fiiTableSql);
        await db.Database.ExecuteSqlRawAsync(recurringTableSql);
        await db.Database.ExecuteSqlRawAsync(recurringCompatSql);

        // Optional/index steps should not crash the API on deploy.
        await ExecuteOptionalSqlAsync(db, fiiIndexSql, logger, "fii unique index");
        await ExecuteOptionalSqlAsync(db, recurringIndexUserSql, logger, "recurring user index");
        await ExecuteOptionalSqlAsync(db, recurringIndexUserActiveSql, logger, "recurring user-active index");

        logger.LogInformation("Schema bootstrap finished.");
    }
    finally
    {
        db.Database.SetCommandTimeout(originalTimeout);
    }
}

static async Task ExecuteOptionalSqlAsync(
    AppDbContext db,
    string sql,
    ILogger logger,
    string operationName)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync(sql);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Schema bootstrap skipped optional step: {Operation}", operationName);
    }
}

