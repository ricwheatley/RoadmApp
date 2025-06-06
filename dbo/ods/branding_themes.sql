-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero BrandingThemes
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses IDENTITY (preferred over SERIAL).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: branding_theme_id (UUID).
--    * Type is always 'INVOICE' – enforced with CHECK constraint.
--    * Arrays / nested objects (PaymentServices, etc.) are not expanded here.
-- =============================================================

/* -------------------------------------------------------------
   ods.branding_themes – organisation‑level branding themes
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.branding_themes (
    /* --- Business / natural key -------------------------------- */
    branding_theme_id UUID        NOT NULL,              -- Xero BrandingThemeID

    /* --- Core scalar attributes from BrandingTheme schema ------ */
    name              VARCHAR(255),
    logo_url          VARCHAR(255),
    type              VARCHAR(20)  NOT NULL,
    sort_order        INTEGER,
    created_date_utc  TIMESTAMPTZ,

    /* --- SCD‑2 infrastructure ---------------------------------- */
    surrogate_key BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from    TIMESTAMPTZ NOT NULL,
    valid_to      TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current    BOOLEAN     NOT NULL DEFAULT TRUE,

    /* --- Tenant / organisation --------------------------------- */
    organisation_id UUID      NOT NULL,

    /* --- Audit & status ---------------------------------------- */
    fetched_at     TIMESTAMPTZ NOT NULL,
    row_created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status  ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    /* --- Batch / source tracking ------------------------------- */
    batch_id                  UUID         NOT NULL,
    landing_table_name        VARCHAR(255) NOT NULL,
    landing_record_identifier VARCHAR(512) NOT NULL,
    raw_table_name            VARCHAR(255),
    api_call_id               UUID,
    source_system_id          VARCHAR(100),
    source_record_modified_at TIMESTAMPTZ,

    /* --- Constraints ------------------------------------------- */
    CONSTRAINT uq_branding_themes_business_key_current
        UNIQUE (branding_theme_id, is_current) WHERE is_current,

    CONSTRAINT fk_branding_themes_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    /* enumerations ---------------------------------------------- */
    CONSTRAINT ck_branding_themes_type
        CHECK (type = 'INVOICE'),

    /* generic checks -------------------------------------------- */
    CONSTRAINT ck_branding_themes_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_branding_themes_valid_dates
        CHECK (valid_from < valid_to)
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_branding_themes_business_key_current
    ON ods.branding_themes(branding_theme_id) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_branding_themes_valid_to
    ON ods.branding_themes(valid_to);

CREATE INDEX IF NOT EXISTS idx_branding_themes_is_current
    ON ods.branding_themes(is_current);

CREATE INDEX IF NOT EXISTS idx_branding_themes_fetched_at
    ON ods.branding_themes(fetched_at);

CREATE INDEX IF NOT EXISTS idx_branding_themes_batch_id
    ON ods.branding_themes(batch_id);

CREATE INDEX IF NOT EXISTS idx_branding_themes_organisation_id
    ON ods.branding_themes(organisation_id);

/* --- Trigger to maintain row_updated_at ----------------------- */
CREATE TRIGGER trg_update_branding_themes_row_updated_at
BEFORE UPDATE ON ods.branding_themes
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ------------------------------------------- */
COMMENT ON TABLE ods.branding_themes IS 'Branding theme definitions used on invoices and other documents, stored as a Type‑2 Slowly Changing Dimension.';
COMMENT ON COLUMN ods.branding_themes.branding_theme_id IS 'Natural business key – Xero BrandingThemeID.';
COMMENT ON COLUMN ods.branding_themes.type IS 'Branding theme type – always INVOICE.';
COMMENT ON COLUMN ods.branding_themes.surrogate_key IS 'Surrogate key for each version of the branding theme record.';
COMMENT ON COLUMN ods.branding_themes.valid_from IS 'Timestamp at which this version becomes valid.';
COMMENT ON COLUMN ods.branding_themes.valid_to IS 'Timestamp at which this version ceases to be valid.';
COMMENT ON COLUMN ods.branding_themes.is_current IS 'TRUE if this row is the current version.';
