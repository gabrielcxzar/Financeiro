using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFinance.API.Migrations
{
    public partial class FormalizeFinancialCoreSchemaEf : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    email TEXT NOT NULL,
    password_hash TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    type TEXT NOT NULL,
    icon TEXT NOT NULL DEFAULT '',
    color TEXT NOT NULL DEFAULT '',
    user_id INT NOT NULL
);

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

CREATE TABLE IF NOT EXISTS transactions (
    id SERIAL PRIMARY KEY,
    description TEXT NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    date TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    type TEXT NOT NULL,
    paid BOOLEAN NOT NULL DEFAULT FALSE,
    categoryid INT NULL,
    accountid INT NOT NULL,
    user_id INT NOT NULL,
    installment_id TEXT NULL,
    is_transfer BOOLEAN NOT NULL DEFAULT FALSE,
    transfer_group_id TEXT NULL,
    recurring_rule_id INT NULL
);

CREATE TABLE IF NOT EXISTS budgets (
    id SERIAL PRIMARY KEY,
    amount NUMERIC(18,2) NOT NULL,
    category_id INT NOT NULL,
    month INT NOT NULL DEFAULT 1,
    year INT NOT NULL DEFAULT 2000,
    allow_rollover BOOLEAN NOT NULL DEFAULT FALSE,
    user_id INT NOT NULL
);

CREATE TABLE IF NOT EXISTS fii_holdings (
    id SERIAL PRIMARY KEY,
    ticker TEXT NOT NULL,
    shares NUMERIC(18,4) NOT NULL,
    avg_price NUMERIC(18,4) NOT NULL,
    notes TEXT NULL,
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    user_id INT NOT NULL
);

CREATE TABLE IF NOT EXISTS recurring_transactions (
    id SERIAL PRIMARY KEY,
    description TEXT NOT NULL,
    amount NUMERIC(18,2) NOT NULL,
    type TEXT NOT NULL,
    day_of_month INT NOT NULL DEFAULT 1,
    active BOOLEAN NOT NULL DEFAULT TRUE,
    category_id INT NULL,
    account_id INT NULL,
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    user_id INT NOT NULL DEFAULT 0
);

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
        ALTER TABLE recurring_transactions ADD COLUMN user_id INT NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'transactions' AND column_name = 'is_transfer'
    ) THEN
        ALTER TABLE transactions ADD COLUMN is_transfer BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'transactions' AND column_name = 'transfer_group_id'
    ) THEN
        ALTER TABLE transactions ADD COLUMN transfer_group_id TEXT NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'transactions' AND column_name = 'recurring_rule_id'
    ) THEN
        ALTER TABLE transactions ADD COLUMN recurring_rule_id INT NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'budgets' AND column_name = 'month'
    ) THEN
        ALTER TABLE budgets ADD COLUMN month INT NOT NULL DEFAULT 1;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'budgets' AND column_name = 'year'
    ) THEN
        ALTER TABLE budgets ADD COLUMN year INT NOT NULL DEFAULT 2000;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public' AND table_name = 'budgets' AND column_name = 'allow_rollover'
    ) THEN
        ALTER TABLE budgets ADD COLUMN allow_rollover BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
END $$;

UPDATE recurring_transactions
SET user_id = COALESCE(NULLIF(user_id, 0), (
    SELECT COALESCE(MIN(user_id), 0) FROM accounts
))
WHERE user_id = 0;

UPDATE transactions
SET is_transfer = TRUE
WHERE description IN (
    'Transferencia para conta/cartao',
    'Recebido de transferencia',
    'Pagamento de fatura',
    'Pagamento de fatura importado'
);

UPDATE budgets
SET
    month = EXTRACT(MONTH FROM CURRENT_DATE)::INT,
    year = EXTRACT(YEAR FROM CURRENT_DATE)::INT
WHERE year = 2000;

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users (email);
CREATE INDEX IF NOT EXISTS ix_categories_user_id ON categories (user_id);
CREATE INDEX IF NOT EXISTS ix_accounts_user_id ON accounts (user_id);
CREATE INDEX IF NOT EXISTS ix_transactions_user_id ON transactions (user_id);
CREATE INDEX IF NOT EXISTS ix_transactions_accountid ON transactions (accountid);
CREATE INDEX IF NOT EXISTS ix_transactions_categoryid ON transactions (categoryid);
CREATE INDEX IF NOT EXISTS ix_budgets_user_id ON budgets (user_id);
CREATE INDEX IF NOT EXISTS ix_budgets_category_id ON budgets (category_id);
CREATE INDEX IF NOT EXISTS ix_fii_holdings_user_id ON fii_holdings (user_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_fii_holdings_user_ticker ON fii_holdings (user_id, ticker);
CREATE INDEX IF NOT EXISTS ix_recurring_transactions_user_id ON recurring_transactions (user_id);
CREATE INDEX IF NOT EXISTS ix_recurring_transactions_user_active ON recurring_transactions (user_id, active);
CREATE INDEX IF NOT EXISTS ix_recurring_transactions_account_id ON recurring_transactions (account_id);
CREATE INDEX IF NOT EXISTS ix_recurring_transactions_category_id ON recurring_transactions (category_id);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op. This migration formalizes an existing schema and applies additive backfills.
        }
    }
}
