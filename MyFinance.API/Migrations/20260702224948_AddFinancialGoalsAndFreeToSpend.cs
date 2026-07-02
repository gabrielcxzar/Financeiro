using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFinance.API.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialGoalsAndFreeToSpend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_essential",
                table: "budgets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "financial_goals",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    goal_type = table.Column<string>(type: "text", nullable: false),
                    target_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    current_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    target_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    monthly_contribution = table.Column<decimal>(type: "numeric", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    linked_account_id = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_goals", x => x.id);
                    table.ForeignKey(
                        name: "FK_financial_goals_accounts_linked_account_id",
                        column: x => x.linked_account_id,
                        principalTable: "accounts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_goals_linked_account_id",
                table: "financial_goals",
                column: "linked_account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_goals");

            migrationBuilder.DropColumn(
                name: "is_essential",
                table: "budgets");
        }
    }
}
