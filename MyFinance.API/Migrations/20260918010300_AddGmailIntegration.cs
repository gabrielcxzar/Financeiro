using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFinance.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGmailIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_import_artifacts",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    external_message_id = table.Column<string>(type: "text", nullable: false),
                    external_attachment_id = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_hash = table.Column<string>(type: "text", nullable: true),
                    import_batch_id = table.Column<int>(type: "integer", nullable: true),
                    discovered_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_import_artifacts", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_import_artifacts_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gmail_integrations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    google_email = table.Column<string>(type: "text", nullable: true),
                    encrypted_refresh_token = table.Column<string>(type: "text", nullable: false),
                    connected_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    last_sync_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_successful_sync_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "text", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    default_account_id = table.Column<int>(type: "integer", nullable: true),
                    search_query = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_integrations", x => x.id);
                    table.ForeignKey(
                        name: "FK_gmail_integrations_accounts_default_account_id",
                        column: x => x.default_account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "gmail_oauth_states",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    state_hash = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gmail_oauth_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_external_import_artifacts_import_batch_id",
                table: "external_import_artifacts",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_external_import_artifacts_user_id_provider_external_message~",
                table: "external_import_artifacts",
                columns: new[] { "user_id", "provider", "external_message_id", "external_attachment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gmail_integrations_default_account_id",
                table: "gmail_integrations",
                column: "default_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_gmail_integrations_user_id",
                table: "gmail_integrations",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gmail_oauth_states_state_hash",
                table: "gmail_oauth_states",
                column: "state_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_import_artifacts");

            migrationBuilder.DropTable(
                name: "gmail_integrations");

            migrationBuilder.DropTable(
                name: "gmail_oauth_states");
        }
    }
}
