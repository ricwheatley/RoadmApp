-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero BankTransactionLineItems
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses BIGINT IDENTITY (PostgreSQL‑preferred).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: line_item_id (UUID).
--    * Parent linkage via bank_transaction_id + organisation_id; FK to ods.bank_transactions.
--    * Tracking array not expanded here – request a `bank_transaction_line_item_tracking` table if required.
-- =============================================================

/* -------------------------------------------------------------
   ods.bank_transaction_line_items – Bank transaction line items as SCD‑2
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.bank_transaction_line_items (
    /* --- Business / natural key -------------------------------- */
    line_item_id         UUID            NOT NULL,  -- Xero LineItemID

    /* --- Relationship keys ------------------------------------- */
    bank_transaction_id  UUID            NOT NULL,
    organisation_id      UUID            NOT NULL,

    /* --- Core scalar attributes -------------------------------- */
    item_code            VARCHAR(50),
    description          VARCHAR(4000),
    quantity             NUMERIC(18,2),
    unit_amount          NUMERIC(18,2),
    discount_rate        NUMERIC(5,2),
    discount_amount      NUMERIC(18,2),
    tax_type             VARCHAR(50),
    tax_amount           NUMERIC(18,2),
    line_amount          NUMERIC(18,2),
    account_code         VARCHAR(10),

    /* --- SCD‑2 infrastructure ---------------------------------- */
    surrogate_key        BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from           TIMESTAMPTZ     NOT NULL,
    valid_to             TIMESTAMPTZ     NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current           BOOLEAN         NOT NULL DEFAULT TRUE,

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
    CONSTRAINT uq_btl_items_business_key_current
        UNIQUE (line_item_id, is_current) WHERE is_current,

    CONSTRAINT fk_btl_bank_transaction
        FOREIGN KEY (bank_transaction_id)
        REFERENCES ods.bank_transactions(bank_transaction_id),

    CONSTRAINT fk_btl_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    CONSTRAINT ck_btl_amounts_non_negative
        CHECK (
            (quantity        IS NULL OR quantity        >= 0) AND
            (unit_amount     IS NULL OR unit_amount     >= 0) AND
            (discount_rate   IS NULL OR (discount_rate >= 0 AND discount_rate <= 100)) AND
            (discount_amount IS NULL OR discount_amount >= 0) AND
            (tax_amount      IS NULL OR tax_amount      >= 0) AND
            (line_amount     IS NULL OR line_amount     >= 0)
        ),

    CONSTRAINT ck_btl_account_code_format
        CHECK (account_code IS NULL OR account_code ~ '^[A-Z0-9]{1,10}$'),

    CONSTRAINT ck_btl_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_btl_valid_dates
        CHECK (valid_from < valid_to)
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_btl_line_item_id_current
    ON ods.bank_transaction_line_items(line_item_id) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_btl_bank_transaction_id
    ON ods.bank_transaction_line_items(bank_transaction_id);

CREATE INDEX IF NOT EXISTS idx_btl_valid_to
    ON ods.bank_transaction_line_items(valid_to);

CREATE INDEX IF NOT EXISTS idx_btl_is_current
    ON ods.bank_transaction_line_items(is_current);

CREATE INDEX IF NOT EXISTS idx_btl_fetched_at
    ON ods.bank_transaction_line_items(fetched_at);

CREATE INDEX IF NOT EXISTS idx_btl_batch_id
    ON ods.bank_transaction_line_items(batch_id);

CREATE INDEX IF NOT EXISTS idx_btl_organisation_id
    ON ods.bank_transaction_line_items(organisation_id);

/* --- Trigger to maintain row_updated_at ----------------------- */
CREATE TRIGGER trg_update_btl_row_updated_at
BEFORE UPDATE ON ods.bank_transaction_line_items
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ------------------------------------------- */
COMMENT ON TABLE ods.bank_transaction_line_items IS 'Line items for bank transactions, captured as Type‑2 SCD records.';
COMMENT ON COLUMN ods.bank_transaction_line_items.line_item_id IS 'Xero LineItemID (business key).';
COMMENT ON COLUMN ods.bank_transaction_line_items.bank_transaction_id IS 'Parent Xero BankTransactionID.';
COMMENT ON COLUMN ods.bank_transaction_line_items.account_code IS 'General ledger account code for the line item.';
COMMENT ON COLUMN ods.bank_transaction_line_items.is_current IS 'TRUE if this row represents the current version of the line item.';
