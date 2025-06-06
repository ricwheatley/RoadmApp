-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero OverpaymentLineItems
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses BIGINT IDENTITY (PostgreSQL‑recommended).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: line_item_id (UUID).
--    * Parent linkage via overpayment_id + organisation_id (FKs).
-- =============================================================

/* -------------------------------------------------------------
   ods.overpayment_line_items – Type‑2 Slowly Changing Dimensions table
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.overpayment_line_items (
    /* --- Business / natural key ----------------------------- */
    line_item_id            UUID            NOT NULL,   -- Xero LineItemID

    /* --- Parent keys ---------------------------------------- */
    overpayment_id          UUID            NOT NULL,
    organisation_id         UUID            NOT NULL,

    /* --- Core scalar attributes ----------------------------- */
    item_code               VARCHAR(50),
    description             VARCHAR(4000),
    quantity                NUMERIC(18,2)            CHECK (quantity >= 0),
    unit_amount             NUMERIC(18,2)            CHECK (unit_amount >= 0),
    discount_rate           NUMERIC(5,2)             CHECK (discount_rate BETWEEN 0 AND 100),
    discount_amount         NUMERIC(18,2)            CHECK (discount_amount >= 0),
    tax_type                VARCHAR(50),
    tax_amount              NUMERIC(18,2)            CHECK (tax_amount >= 0),
    line_amount             NUMERIC(18,2)            CHECK (line_amount >= 0),
    account_code            VARCHAR(10)               CHECK (account_code ~ '^[A-Z0-9]{1,10}$'),

    /* --- Tracking reference IDs (not FK‑enforced) ----------- */
    tracking_category_id    UUID,
    tracking_option_id      UUID,

    /* --- SCD‑2 infrastructure ------------------------------- */
    surrogate_key           BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from              TIMESTAMPTZ NOT NULL,
    valid_to                TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current              BOOLEAN     NOT NULL DEFAULT TRUE,

    /* --- Audit & status ------------------------------------- */
    fetched_at              TIMESTAMPTZ NOT NULL,
    row_created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status           ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    /* --- Batch / source tracking ---------------------------- */
    batch_id                     UUID         NOT NULL,
    landing_table_name           VARCHAR(255) NOT NULL,
    landing_record_identifier    VARCHAR(512) NOT NULL,
    raw_table_name               VARCHAR(255),
    api_call_id                  UUID,
    source_system_id             VARCHAR(100),
    source_record_modified_at    TIMESTAMPTZ,

    /* --- Constraints ---------------------------------------- */
    CONSTRAINT uq_overpay_li_business_key_current
        UNIQUE (line_item_id, is_current) WHERE is_current,

    CONSTRAINT ck_overpay_li_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_overpay_li_valid_dates
        CHECK (valid_from < valid_to),

    /* --- Foreign Keys --------------------------------------- */
    CONSTRAINT fk_overpay_li_overpayment
        FOREIGN KEY (overpayment_id)
        REFERENCES ods.overpayments(overpayment_id),

    CONSTRAINT fk_overpay_li_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_overpay_li_business_key_current
    ON ods.overpayment_line_items(line_item_id) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_overpay_li_valid_to
    ON ods.overpayment_line_items(valid_to);

CREATE INDEX IF NOT EXISTS idx_overpay_li_is_current
    ON ods.overpayment_line_items(is_current);

CREATE INDEX IF NOT EXISTS idx_overpay_li_fetched_at
    ON ods.overpayment_line_items(fetched_at);

CREATE INDEX IF NOT EXISTS idx_overpay_li_batch_id
    ON ods.overpayment_line_items(batch_id);

CREATE INDEX IF NOT EXISTS idx_overpay_li_overpayment_id
    ON ods.overpayment_line_items(overpayment_id);

CREATE INDEX IF NOT EXISTS idx_overpay_li_organisation_id
    ON ods.overpayment_line_items(organisation_id);

/* --- Trigger to maintain row_updated_at ----------------------- */
CREATE TRIGGER trg_update_overpay_li_row_updated_at
BEFORE UPDATE ON ods.overpayment_line_items
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ------------------------------------------- */
COMMENT ON TABLE ods.overpayment_line_items IS 'Line items belonging to an overpayment, stored as Type‑2 slowly changing dimension rows.';
COMMENT ON COLUMN ods.overpayment_line_items.line_item_id IS 'Natural identifier for the line item.';
COMMENT ON COLUMN ods.overpayment_line_items.account_code IS 'Nominal code to which the line item is posted.';
COMMENT ON COLUMN ods.overpayment_line_items.is_current IS 'TRUE if this row represents the latest version of the line item.';
