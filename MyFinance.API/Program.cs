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

    const string usersTableSql = @"
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    password_hash TEXT NOT NULL
);
";

    const string categoriesTableSql = @"
CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    icon TEXT NOT NULL DEFAULT '',
    color TEXT NOT NULL DEFAULT '',
    user_id INT NOT NULL
);
";

    const string accountsTableSql = @"
CREATE TABLE IF NOT EXISTS accounts (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    initialbalance NUMERIC(18,2) NOT NULL DEFAULT 0,
    currentbalance NUMERIC(18,2) NOT NULL DEFAULT 0,
    type TEXT NOT NULL DEFAULT 'Checking',
    is_credit_card BOOLEAN NOT NULL DEFAULT FALSE,
    closing_day INT NULL,
    due_day INT NULL,
    credit_limit NUMERIC(18,2) NULL,
    user_id INT NOT NULL
);
";

    const string transactionsTableSql = @"
CREATE TABLE IF NOT EXISTS transactions (
    id SERIAL PRIMARY KEY,
    description TEXT NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    date TIMESTAMPTZ NOT NULL,
    type TEXT NOT NULL,
    paid BOOLEAN NOT NULL DEFAULT FALSE,
    categoryid INT NULL,
    accountid INT NOT NULL,
    user_id INT NOT NULL,
    installment_id TEXT NULL
);
";

    const string budgetsTableSql = @"
CREATE TABLE IF NOT EXISTS budgets (
    id SERIAL PRIMARY KEY,
    amount NUMERIC(18,2) NOT NULL,
    category_id INT NOT NULL,
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

    const string usersEmailIndexSql = @"
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_users_email
ON users (email);
";

    const string categoriesUserIndexSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_categories_user_id
ON categories (user_id);
";

    const string accountsUserIndexSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_accounts_user_id
ON accounts (user_id);
";

    const string transactionsUserIndexSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_transactions_user_id
ON transactions (user_id);
";

    const string transactionsAccountIndexSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_transactions_accountid
ON transactions (accountid);
";

    const string budgetsUserIndexSql = @"
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_budgets_user_id
ON budgets (user_id);
";

    const string accountsCompatSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'accounts' AND column_name = 'is_credit_card'
    ) THEN
        ALTER TABLE accounts ADD COLUMN is_credit_card BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'accounts' AND column_name = 'closing_day'
    ) THEN
        ALTER TABLE accounts ADD COLUMN closing_day INT NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'accounts' AND column_name = 'due_day'
    ) THEN
        ALTER TABLE accounts ADD COLUMN due_day INT NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'accounts' AND column_name = 'credit_limit'
    ) THEN
        ALTER TABLE accounts ADD COLUMN credit_limit NUMERIC(18,2) NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'accounts' AND column_name = 'user_id'
    ) THEN
        ALTER TABLE accounts ADD COLUMN user_id INT NOT NULL DEFAULT 0;
    END IF;
END $$;
";

    const string categoriesCompatSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'categories' AND column_name = 'icon'
    ) THEN
        ALTER TABLE categories ADD COLUMN icon TEXT NOT NULL DEFAULT '';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'categories' AND column_name = 'color'
    ) THEN
        ALTER TABLE categories ADD COLUMN color TEXT NOT NULL DEFAULT '';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'categories' AND column_name = 'user_id'
    ) THEN
        ALTER TABLE categories ADD COLUMN user_id INT NOT NULL DEFAULT 0;
    END IF;
END $$;
";

    const string transactionsCompatSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'transactions' AND column_name = 'user_id'
    ) THEN
        ALTER TABLE transactions ADD COLUMN user_id INT NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'transactions' AND column_name = 'installment_id'
    ) THEN
        ALTER TABLE transactions ADD COLUMN installment_id TEXT NULL;
    END IF;
END $$;
";

    const string budgetsCompatSql = @"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'budgets' AND column_name = 'user_id'
    ) THEN
        ALTER TABLE budgets ADD COLUMN user_id INT NOT NULL DEFAULT 0;
    END IF;
END $$;
";

    var originalTimeout = db.Database.GetCommandTimeout();
    db.Database.SetCommandTimeout(TimeSpan.FromMinutes(3));

    try
    {
        logger.LogInformation("Running schema bootstrap...");

        // Core schema needed for a first deploy on a clean Postgres database.
        await db.Database.ExecuteSqlRawAsync(usersTableSql);
        await db.Database.ExecuteSqlRawAsync(categoriesTableSql);
        await db.Database.ExecuteSqlRawAsync(accountsTableSql);
        await db.Database.ExecuteSqlRawAsync(transactionsTableSql);
        await db.Database.ExecuteSqlRawAsync(budgetsTableSql);
        await db.Database.ExecuteSqlRawAsync(fiiTableSql);
        await db.Database.ExecuteSqlRawAsync(recurringTableSql);

        // Compatibility upgrades for older databases that predate newer fields.
        await db.Database.ExecuteSqlRawAsync(accountsCompatSql);
        await db.Database.ExecuteSqlRawAsync(categoriesCompatSql);
        await db.Database.ExecuteSqlRawAsync(transactionsCompatSql);
        await db.Database.ExecuteSqlRawAsync(budgetsCompatSql);
        await db.Database.ExecuteSqlRawAsync(recurringCompatSql);

        // Optional/index steps should not crash the API on deploy.
        await ExecuteOptionalSqlAsync(db, usersEmailIndexSql, logger, "users email unique index");
        await ExecuteOptionalSqlAsync(db, categoriesUserIndexSql, logger, "categories user index");
        await ExecuteOptionalSqlAsync(db, accountsUserIndexSql, logger, "accounts user index");
        await ExecuteOptionalSqlAsync(db, transactionsUserIndexSql, logger, "transactions user index");
        await ExecuteOptionalSqlAsync(db, transactionsAccountIndexSql, logger, "transactions account index");
        await ExecuteOptionalSqlAsync(db, budgetsUserIndexSql, logger, "budgets user index");
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

