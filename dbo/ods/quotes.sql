-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero Quotes
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses BIGINT IDENTITY (PostgreSQL‑preferred).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: quote_id (UUID).
--    * LineItems, Attachments, Contact, and History arrays are NOT expanded here – ask if you need 
--      `quote_line_items` or `quote_attachments` tables.
-- =============================================================

/* -------------------------------------------------------------
   ods.quotes – Type‑2 Slowly Changing Dimensions
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.quotes (
    /* --- Business / natural key -------------------------------- */
    quote_id                    UUID            NOT NULL,   -- Xero QuoteID

    /* --- Parent / tenant linkage ------------------------------- */
    organisation_id             UUID            NOT NULL,
    contact_id                  UUID,
    branding_theme_id           UUID,

    /* --- Core scalar attributes -------------------------------- */
    quote_number                VARCHAR(50),
    reference                   VARCHAR(255),
    title                       VARCHAR(255),
    summary                     VARCHAR(4000),
    terms                       VARCHAR(4000),
    date                        DATE,
    expiry_date                 DATE,
    expected_payment_date       DATE,
    type                        VARCHAR(10) CHECK (type = 'ACCREC'),
    status                      VARCHAR(10) CHECK (status IN ('DRAFT','SENT','ACCEPTED','DECLINED','EXPIRED','INVOICED')),
    line_amount_types           VARCHAR(10) CHECK (line_amount_types IN ('EXCLUSIVE','INCLUSIVE','NOTAX')),
    currency_code               VARCHAR(3)  CHECK (currency_code ~ '^[A-Z]{3}$'),
    currency_rate               NUMERIC(18,6)  CHECK (currency_rate >= 0),
    sub_total                   NUMERIC(18,2)  CHECK (sub_total >= 0),
    total_tax                   NUMERIC(18,2)  CHECK (total_tax >= 0),
    total                       NUMERIC(18,2)  CHECK (total >= 0),
    total_discount              NUMERIC(18,2)  CHECK (total_discount >= 0),
    status_attribute_string     VARCHAR(50),
    has_attachments             BOOLEAN,
    updated_date_utc            TIMESTAMPTZ,

    /* --- SCD‑2 infrastructure ---------------------------------- */
    surrogate_key               BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from                  TIMESTAMPTZ NOT NULL,
    valid_to                    TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current                  BOOLEAN NOT NULL DEFAULT TRUE,

    /* --- Audit & status ---------------------------------------- */
    fetched_at                  TIMESTAMPTZ NOT NULL,
    row_created_at              TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at              TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status               ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    /* --- Batch / source tracking ------------------------------- */
    batch_id                    UUID         NOT NULL,
    landing_table_name          VARCHAR(255) NOT NULL,
    landing_record_identifier   VARCHAR(512) NOT NULL,
    raw_table_name              VARCHAR(255),
    api_call_id                 UUID,
    source_system_id            VARCHAR(100),
    source_record_modified_at   TIMESTAMPTZ,

    /* --- Constraints ------------------------------------------- */
    CONSTRAINT uq_quotes_business_key_current UNIQUE (quote_id, is_current) WHERE is_current,

    CONSTRAINT ck_quotes_record_status CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),
    CONSTRAINT ck_quotes_valid_dates   CHECK (valid_from < valid_to),

    /* --- Foreign Keys ------------------------------------------ */
    CONSTRAINT fk_quotes_org FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),
    CONSTRAINT fk_quotes_contact FOREIGN KEY (contact_id)
        REFERENCES ods.contacts(contact_id),
    CONSTRAINT fk_quotes_branding_theme FOREIGN KEY (branding_theme_id)
        REFERENCES ods.branding_themes(branding_theme_id)
);

/* --- Indexes --------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_quotes_business_key_current ON ods.quotes(quote_id) WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_quotes_organisation_id      ON ods.quotes(organisation_id);
CREATE INDEX IF NOT EXISTS idx_quotes_contact_id           ON ods.quotes(contact_id);
CREATE INDEX IF NOT EXISTS idx_quotes_branding_theme_id    ON ods.quotes(branding_theme_id);
CREATE INDEX IF NOT EXISTS idx_quotes_valid_to             ON ods.quotes(valid_to);
CREATE INDEX IF NOT EXISTS idx_quotes_is_current           ON ods.quotes(is_current);
CREATE INDEX IF NOT EXISTS idx_quotes_fetched_at           ON ods.quotes(fetched_at);
CREATE INDEX IF NOT EXISTS idx_quotes_batch_id             ON ods.quotes(batch_id);

/* --- Trigger to maintain row_updated_at ------------------------ */
CREATE TRIGGER trg_update_quotes_row_updated_at
BEFORE UPDATE ON ods.quotes
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation -------------------------------------------- */
COMMENT ON TABLE ods.quotes IS 'Stores Xero Quote headers as Type‑2 slowly changing dimensions (one row per quote version).';
COMMENT ON COLUMN ods.quotes.quote_id IS 'Natural business key (Xero QuoteID).';
COMMENT ON COLUMN ods.quotes.line_amount_types IS 'How line item amounts are treated for tax.';
