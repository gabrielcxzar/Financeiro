-- Reparação idempotente dos lançamentos legados confirmados em 2026-09-17.
-- Uso:
--   dry-run (padrão): psql "$DATABASE_URL" -f RepairLegacyNonOperationalTransactions.sql
--   aplicar:           psql "$DATABASE_URL" \
--                        -c "SET finflow.apply_legacy_reporting = '1'" \
--                        -f RepairLegacyNonOperationalTransactions.sql
--
-- O script aborta se qualquer identidade/invariante contábil divergir. Ele não
-- altera valor, data, conta, categoria, tipo, descrição, status pago ou saldos.

BEGIN;

CREATE TEMP TABLE repair_targets (
    id integer PRIMARY KEY,
    proposed_is_transfer boolean NOT NULL,
    proposed_reporting_kind text NOT NULL,
    proposed_exclude_from_reports boolean NOT NULL,
    proposed_transfer_group_id text NULL
) ON COMMIT DROP;

INSERT INTO repair_targets VALUES
    (3214, TRUE,  'invoice_payment',     TRUE, 'legacy-invoice-payment-3214-3240'),
    (3240, TRUE,  'invoice_payment',     TRUE, 'legacy-invoice-payment-3214-3240'),
    (3276, FALSE, 'technical_adjustment', TRUE, NULL);

DO $validation$
DECLARE
    invoice_user_id integer;
    matching_rows integer;
BEGIN
    SELECT count(*), min(t.user_id)
      INTO matching_rows, invoice_user_id
      FROM transactions t
     WHERE t.id IN (3214, 3240);

    IF matching_rows <> 2 OR invoice_user_id IS NULL THEN
        RAISE EXCEPTION 'Invoice-payment repair aborted: expected transaction ids 3214 and 3240.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM transactions expense
          JOIN accounts bank
            ON bank.id = expense.accountid
           AND bank.user_id = expense.user_id
          JOIN transactions income
            ON income.id = 3240
           AND income.user_id = expense.user_id
           AND income.amount = expense.amount
           AND income.date::date = expense.date::date
          JOIN accounts card
            ON card.id = income.accountid
           AND card.user_id = income.user_id
         WHERE expense.id = 3214
           AND expense.description = 'Pagamento fatura Nubank 07/09'
           AND expense.type = 'Expense'
           AND expense.amount = 936.31
           AND expense.date::date = DATE '2026-09-01'
           AND bank.name = 'Nubank'
           AND bank.is_credit_card = FALSE
           AND income.description = 'Pagamento recebido'
           AND income.type = 'Income'
           AND card.name = 'Cartao Nubank'
           AND card.is_credit_card = TRUE
           AND expense.accountid <> income.accountid
    ) THEN
        RAISE EXCEPTION 'Invoice-payment repair aborted: pair invariants no longer match.';
    END IF;

    SELECT count(*)
      INTO matching_rows
      FROM transactions t
      JOIN accounts a ON a.id = t.accountid AND a.user_id = t.user_id
     WHERE t.user_id = invoice_user_id
       AND t.date::date = DATE '2026-09-01'
       AND t.amount = 936.31
       AND ((t.description = 'Pagamento fatura Nubank 07/09' AND t.type = 'Expense' AND a.is_credit_card = FALSE)
         OR (t.description = 'Pagamento recebido' AND t.type = 'Income' AND a.is_credit_card = TRUE));

    IF matching_rows <> 2 THEN
        RAISE EXCEPTION 'Invoice-payment repair aborted: candidate pair is ambiguous (% matching rows).', matching_rows;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM transactions t
         WHERE t.id IN (3214, 3240)
           AND NOT (
               (t.is_transfer = FALSE AND t.reporting_kind = 'normal' AND t.exclude_from_reports = FALSE AND t.transfer_group_id IS NULL)
               OR
               (t.is_transfer = TRUE AND t.reporting_kind = 'invoice_payment' AND t.exclude_from_reports = TRUE AND t.transfer_group_id = 'legacy-invoice-payment-3214-3240')
           )
    ) THEN
        RAISE EXCEPTION 'Invoice-payment repair aborted: current flags are neither legacy nor already repaired.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
          FROM transactions adjustment
          JOIN accounts bank
            ON bank.id = adjustment.accountid
           AND bank.user_id = adjustment.user_id
         WHERE adjustment.id = 3276
           AND adjustment.user_id = invoice_user_id
           AND adjustment.date::date = DATE '2026-09-01'
           AND adjustment.description = 'Ajuste tecnico de saldo Nubank'
           AND adjustment.type = 'Expense'
           AND adjustment.amount = 0.60
           AND bank.name = 'Nubank'
           AND bank.is_credit_card = FALSE
           AND adjustment.is_transfer = FALSE
           AND adjustment.transfer_group_id IS NULL
           AND ((adjustment.reporting_kind = 'normal' AND adjustment.exclude_from_reports = FALSE)
             OR (adjustment.reporting_kind = 'technical_adjustment' AND adjustment.exclude_from_reports = TRUE))
    ) THEN
        RAISE EXCEPTION 'Technical-adjustment repair aborted: transaction 3276 invariants no longer match.';
    END IF;
END
$validation$;

-- Saída intencionalmente minimizada para auditoria do dry-run.
SELECT
    t.id,
    t.date::date AS date,
    t.description,
    a.name AS account,
    t.type,
    t.amount,
    concat('isTransfer=', t.is_transfer,
           ', reportingKind=', t.reporting_kind,
           ', excludeFromReports=', t.exclude_from_reports,
           ', transferGroupId=', coalesce(t.transfer_group_id, '')) AS current_flags,
    concat('isTransfer=', r.proposed_is_transfer,
           ', reportingKind=', r.proposed_reporting_kind,
           ', excludeFromReports=', r.proposed_exclude_from_reports,
           ', transferGroupId=', coalesce(r.proposed_transfer_group_id, '')) AS proposed_flags
FROM repair_targets r
JOIN transactions t ON t.id = r.id
JOIN accounts a ON a.id = t.accountid AND a.user_id = t.user_id
ORDER BY t.id;

WITH updated AS (
    UPDATE transactions t
       SET is_transfer = r.proposed_is_transfer,
           reporting_kind = r.proposed_reporting_kind,
           exclude_from_reports = r.proposed_exclude_from_reports,
           transfer_group_id = r.proposed_transfer_group_id
      FROM repair_targets r
     WHERE current_setting('finflow.apply_legacy_reporting', TRUE) = '1'
       AND t.id = r.id
       AND (t.is_transfer, t.reporting_kind, t.exclude_from_reports, t.transfer_group_id)
           IS DISTINCT FROM
           (r.proposed_is_transfer, r.proposed_reporting_kind, r.proposed_exclude_from_reports, r.proposed_transfer_group_id)
    RETURNING t.id
)
SELECT
    CASE WHEN current_setting('finflow.apply_legacy_reporting', TRUE) = '1' THEN 'APPLY' ELSE 'DRY_RUN' END AS mode,
    count(*) AS affected_rows
FROM updated;

-- Qualquer erro anterior deixa a transação abortada e impede este COMMIT.
COMMIT;
