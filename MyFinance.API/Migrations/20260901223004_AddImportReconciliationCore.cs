using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFinance.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImportReconciliationCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "exclude_from_reports",
                table: "transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "import_batch_id",
                table: "transactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "imported_at",
                table: "transactions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "raw_memo",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporting_kind",
                table: "transactions",
                type: "text",
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_file",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categorization_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    text_pattern = table.Column<string>(type: "text", nullable: false),
                    value_operator = table.Column<string>(type: "text", nullable: true),
                    value_threshold = table.Column<decimal>(type: "numeric", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categorization_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_categorization_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_type = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    file_hash = table.Column<string>(type: "text", nullable: false),
                    ledger_balance = table.Column<decimal>(type: "numeric", nullable: true),
                    period_start = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    period_end = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    total_items = table.Column<int>(type: "integer", nullable: false),
                    imported_count = table.Column<int>(type: "integer", nullable: false),
                    duplicate_count = table.Column<int>(type: "integer", nullable: false),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    ignored_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_import_batches_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "imported_statement_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    import_batch_id = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    posted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    signed_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    memo = table.Column<string>(type: "text", nullable: false),
                    raw_data = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    transaction_id = table.Column<int>(type: "integer", nullable: true),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    recurring_rule_id = table.Column<int>(type: "integer", nullable: true),
                    target_account_id = table.Column<int>(type: "integer", nullable: true),
                    reporting_kind = table.Column<string>(type: "text", nullable: false),
                    resolved_description = table.Column<string>(type: "text", nullable: true),
                    resolved_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    resolved_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    resolved_type = table.Column<string>(type: "text", nullable: true),
                    installment_number = table.Column<int>(type: "integer", nullable: true),
                    total_installments = table.Column<int>(type: "integer", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_imported_statement_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_imported_statement_items_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_imported_statement_items_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_imported_statement_items_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_imported_statement_items_transactions_transaction_id",
                        column: x => x.transaction_id,
                        principalTable: "transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_import_batch_id",
                table: "transactions",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_user_id_accountid_source_external_id",
                table: "transactions",
                columns: new[] { "user_id", "accountid", "source", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_categorization_rules_category_id",
                table: "categorization_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_account_id",
                table: "import_batches",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_user_id_account_id_file_hash",
                table: "import_batches",
                columns: new[] { "user_id", "account_id", "file_hash" });

            migrationBuilder.CreateIndex(
                name: "IX_imported_statement_items_account_id",
                table: "imported_statement_items",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_imported_statement_items_category_id",
                table: "imported_statement_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_imported_statement_items_import_batch_id",
                table: "imported_statement_items",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_imported_statement_items_transaction_id",
                table: "imported_statement_items",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_imported_statement_items_user_id_account_id_source_external~",
                table: "imported_statement_items",
                columns: new[] { "user_id", "account_id", "source", "external_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_import_batches_import_batch_id",
                table: "transactions",
                column: "import_batch_id",
                principalTable: "import_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_import_batches_import_batch_id",
                table: "transactions");

            migrationBuilder.DropTable(
                name: "categorization_rules");

            migrationBuilder.DropTable(
                name: "imported_statement_items");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropIndex(
                name: "IX_transactions_import_batch_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_user_id_accountid_source_external_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "exclude_from_reports",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "imported_at",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "raw_memo",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "reporting_kind",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "source",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "source_file",
                table: "transactions");
        }
    }
}
