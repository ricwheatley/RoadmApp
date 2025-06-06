-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero BankTransactions
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses BIGINT IDENTITY (PostgreSQL‑preferred).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: bank_transaction_id (UUID).
--    * Nested arrays/objects (LineItems, Payments, Attachments, Contact) are NOT expanded here – request child tables if required.
-- =============================================================

/* -------------------------------------------------------------
   ods.bank_transactions – Bank transactions stored as SCD‑2
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.bank_transactions (
    /* --- Business / natural key -------------------------------- */
    bank_transaction_id   UUID            NOT NULL,  -- Xero BankTransactionID

    /* --- Relationship keys ------------------------------------- */
    bank_account_id       UUID            NOT NULL,
    contact_id            UUID,
    organisation_id       UUID            NOT NULL,

    /* --- Core scalar attributes -------------------------------- */
    type                  VARCHAR(20)     NOT NULL,  -- SPEND | RECEIVE | RECEIVE-TRANSFER | SPEND-TRANSFER
    status                VARCHAR(15)     NOT NULL,  -- AUTHORISED | DELETED | VOIDED
    line_amount_types     VARCHAR(10),            -- EXCLUSIVE | INCLUSIVE | NOTAX
    date                  DATE,
    reference             VARCHAR(255),
    currency_code         VARCHAR(3),
    currency_rate         NUMERIC(18,6),
    sub_total             NUMERIC(18,2),
    total_tax             NUMERIC(18,2),
    total                 NUMERIC(18,2),
    is_reconciled         BOOLEAN,
    updated_date_utc      TIMESTAMPTZ,

    /* --- SCD‑2 infrastructure ---------------------------------- */
    surrogate_key         BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from            TIMESTAMPTZ     NOT NULL,
    valid_to              TIMESTAMPTZ     NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current            BOOLEAN         NOT NULL DEFAULT TRUE,

    /* --- Audit & status ---------------------------------------- */
    fetched_at                   TIMESTAMPTZ NOT NULL,
    row_created_at               TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at               TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status                ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    /* --- Batch / source tracking ------------------------------- */
    batch_id                     UUID         NOT NULL,
    landing_table_name           VARCHAR(255) NOT NULL,
    landing_record_identifier    VARCHAR(512) NOT NULL,
    raw_table_name               VARCHAR(255),
    api_call_id                  UUID,
    source_system_id             VARCHAR(100),
    source_record_modified_at    TIMESTAMPTZ,

    /* --- Constraints ------------------------------------------- */
    CONSTRAINT uq_bank_transactions_business_key_current
        UNIQUE (bank_transaction_id, is_current) WHERE is_current,

    CONSTRAINT fk_bank_transactions_bank_account
        FOREIGN KEY (bank_account_id)
        REFERENCES ods.accounts(account_id),

    CONSTRAINT fk_bank_transactions_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    CONSTRAINT ck_bank_transactions_type
        CHECK (type IN ('SPEND','RECEIVE','RECEIVETRANSFER','SPENDTRANSFER')),

    CONSTRAINT ck_bank_transactions_status
        CHECK (status IN ('AUTHORISED','DELETED','VOIDED')),

    CONSTRAINT ck_bank_transactions_line_amount_types
        CHECK (line_amount_types IS NULL OR line_amount_types IN ('EXCLUSIVE','INCLUSIVE','NOTAX')),

    CONSTRAINT ck_bank_transactions_currency_code
        CHECK (currency_code IS NULL OR currency_code ~ '^[A-Z]{3}$'),

    CONSTRAINT ck_bank_transactions_amounts_non_negative
        CHECK (
            (sub_total  IS NULL OR sub_total  >= 0) AND
            (total_tax  IS NULL OR total_tax  >= 0) AND
            (total      IS NULL OR total      >= 0)
        ),

    CONSTRAINT ck_bank_transactions_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_bank_transactions_valid_dates
        CHECK (valid_from < valid_to)
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_bt_business_key_current
    ON ods.bank_transactions(bank_transaction_id) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_bt_bank_account_id
    ON ods.bank_transactions(bank_account_id);

CREATE INDEX IF NOT EXISTS idx_bt_valid_to
    ON ods.bank_transactions(valid_to);

CREATE INDEX IF NOT EXISTS idx_bt_is_current
    ON ods.bank_transactions(is_current);

CREATE INDEX IF NOT EXISTS idx_bt_fetched_at
    ON ods.bank_transactions(fetched_at);

CREATE INDEX IF NOT EXISTS idx_bt_batch_id
    ON ods.bank_transactions(batch_id);

CREATE INDEX IF NOT EXISTS idx_bt_organisation_id
    ON ods.bank_transactions(organisation_id);

/* --- Trigger to maintain row_updated_at ----------------------- */
CREATE TRIGGER trg_update_bt_row_updated_at
BEFORE UPDATE ON ods.bank_transactions
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ------------------------------------------- */
COMMENT ON TABLE ods.bank_transactions IS 'Bank transactions captured as Type‑2 SCD records.';
COMMENT ON COLUMN ods.bank_transactions.bank_transaction_id IS 'Xero BankTransactionID (business key).';
COMMENT ON COLUMN ods.bank_transactions.type IS 'Transaction type: SPEND, RECEIVE, RECEIVETRANSFER, SPENDTRANSFER.';
COMMENT ON COLUMN ods.bank_transactions.status IS 'Workflow status: AUTHORISED, DELETED, or VOIDED.';
COMMENT ON COLUMN ods.bank_transactions.is_current IS 'TRUE if this row represents the current version of the transaction.';