using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyFinance.API.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentPortfolioValuations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "current_price",
                table: "fii_holdings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "quote_as_of",
                table: "fii_holdings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "quote_as_of_date",
                table: "fii_holdings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quote_source",
                table: "fii_holdings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "fixed_income_holdings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    institution = table.Column<string>(type: "text", nullable: true),
                    product_type = table.Column<string>(type: "text", nullable: false),
                    benchmark = table.Column<string>(type: "text", nullable: true),
                    contracted_rate = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    contracted_rate_unit = table.Column<string>(type: "text", nullable: true),
                    maturity_date = table.Column<DateOnly>(type: "date", nullable: true),
                    liquidity = table.Column<string>(type: "text", nullable: true),
                    principal_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    known_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    balance_as_of_date = table.Column<DateOnly>(type: "date", nullable: true),
                    valuation_source = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixed_income_holdings", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixed_income_holdings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fixed_income_holdings_user_id",
                table: "fixed_income_holdings",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixed_income_holdings_user_id_name",
                table: "fixed_income_holdings",
                columns: new[] { "user_id", "name" },
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fixed_income_holdings");

            migrationBuilder.DropColumn(
                name: "current_price",
                table: "fii_holdings");

            migrationBuilder.DropColumn(
                name: "quote_as_of",
                table: "fii_holdings");

            migrationBuilder.DropColumn(
                name: "quote_as_of_date",
                table: "fii_holdings");

            migrationBuilder.DropColumn(
                name: "quote_source",
                table: "fii_holdings");

        }
    }
}
