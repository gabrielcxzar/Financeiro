using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFinance.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionReportingAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "reporting_amount",
                table: "transactions",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_reporting_amount",
                table: "transactions",
                sql: "reporting_amount IS NULL OR (reporting_amount >= 0 AND reporting_amount <= amount)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_reporting_amount",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "reporting_amount",
                table: "transactions");
        }
    }
}
