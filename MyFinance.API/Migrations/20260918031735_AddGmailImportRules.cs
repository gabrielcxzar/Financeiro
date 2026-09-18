using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFinance.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailImportRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_import_batches_accounts_account_id",
                table: "import_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_imported_statement_items_accounts_account_id",
                table: "imported_statement_items");

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                table: "import_batches",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                table: "imported_statement_items",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_import_batches_accounts_account_id",
                table: "import_batches",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_imported_statement_items_accounts_account_id",
                table: "imported_statement_items",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateTable(
                name: "gmail_import_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    search_query = table.Column<string>(type: "text", nullable: false),
                    target_account_id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_import_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_gmail_import_rules_accounts_target_account_id",
                        column: x => x.target_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_gmail_import_rules_target_account_id",
                table: "gmail_import_rules",
                column: "target_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_gmail_import_rules_user_id_name",
                table: "gmail_import_rules",
                columns: new[] { "user_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gmail_import_rules");

            migrationBuilder.DropForeignKey(
                name: "FK_import_batches_accounts_account_id",
                table: "import_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_imported_statement_items_accounts_account_id",
                table: "imported_statement_items");

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                table: "import_batches",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "account_id",
                table: "imported_statement_items",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_import_batches_accounts_account_id",
                table: "import_batches",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_imported_statement_items_accounts_account_id",
                table: "imported_statement_items",
                column: "account_id",
                principalTable: "accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
