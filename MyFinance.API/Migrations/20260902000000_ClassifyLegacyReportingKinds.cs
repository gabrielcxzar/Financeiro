using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFinance.API.Migrations;

public partial class ClassifyLegacyReportingKinds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent backfill: an already resolved classification is never overwritten.
        migrationBuilder.Sql(@"
UPDATE transactions
SET reporting_kind = 'normal'
WHERE reporting_kind IS NULL OR btrim(reporting_kind) = '';

UPDATE transactions AS t
SET reporting_kind = CASE
        WHEN EXISTS (
            SELECT 1 FROM accounts a
            WHERE a.id = t.accountid AND a.is_credit_card = TRUE
        ) OR EXISTS (
            SELECT 1 FROM transactions p
            JOIN accounts pa ON pa.id = p.accountid
            WHERE p.transfer_group_id = t.transfer_group_id
              AND p.id <> t.id AND pa.is_credit_card = TRUE
        ) THEN 'invoice_payment'
        ELSE 'internal_transfer'
    END,
    exclude_from_reports = TRUE
WHERE t.is_transfer = TRUE
  AND t.transfer_group_id IS NOT NULL
  AND EXISTS (
      SELECT 1 FROM transactions p
      WHERE p.user_id = t.user_id
        AND p.transfer_group_id = t.transfer_group_id
        AND p.id <> t.id
  );

UPDATE transactions
SET exclude_from_reports = TRUE
WHERE reporting_kind IN ('invoice_payment', 'internal_transfer', 'pass_through', 'technical_adjustment');
" );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately a no-op: the migration is a conservative, auditable data backfill.
    }
}
