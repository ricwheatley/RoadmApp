-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero TrackingCategories
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses IDENTITY (PostgreSQL‑preferred).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: tracking_category_id (UUID from Xero).
--    * Nested array `Options` not expanded here – ask if an `organisation_tracking_options` table is required.
-- =============================================================

/* -------------------------------------------------------------
   ods.tracking_categories – high‑level tracking categories
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.tracking_categories (
    /* --- Business / natural key ----------------------------- */
    tracking_category_id UUID         NOT NULL,  -- Xero TrackingCategoryID

    /* --- Core attributes from TrackingCategory schema ------- */
    organisation_id      UUID         NOT NULL,  -- Tenant / organisation FK
    name                 VARCHAR(100) NOT NULL,  -- Category name (≤100 chars)
    status               VARCHAR(20)  NOT NULL,  -- ACTIVE | ARCHIVED | DELETED
    has_validation_errors BOOLEAN     NOT NULL DEFAULT FALSE,
    option_count         INTEGER,                 -- Number of options (metadata)

    /* --- SCD‑2 infrastructure ------------------------------ */
    surrogate_key BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from    TIMESTAMPTZ NOT NULL,
    valid_to      TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current    BOOLEAN     NOT NULL DEFAULT TRUE,

    /* --- Audit & status ------------------------------------ */
    fetched_at     TIMESTAMPTZ NOT NULL,
    row_created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status  ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    /* --- Batch / source tracking --------------------------- */
    batch_id                  UUID         NOT NULL,
    landing_table_name        VARCHAR(255) NOT NULL,
    landing_record_identifier VARCHAR(512) NOT NULL,
    raw_table_name            VARCHAR(255),
    api_call_id               UUID,
    source_system_id          VARCHAR(100),
    source_record_modified_at TIMESTAMPTZ,

    /* --- Constraints --------------------------------------- */
    CONSTRAINT uq_tracking_categories_business_key_current
        UNIQUE (tracking_category_id, is_current)
        WHERE is_current,

    CONSTRAINT fk_tracking_categories_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    CONSTRAINT ck_tracking_categories_status_allowed
        CHECK (status IN ('ACTIVE','ARCHIVED','DELETED')),

    CONSTRAINT ck_tracking_categories_name_not_blank
        CHECK (char_length(trim(name)) > 0),

    CONSTRAINT ck_tracking_categories_option_count_non_negative
        CHECK (option_count IS NULL OR option_count >= 0),

    -- Generic SCD & status checks
    CONSTRAINT ck_tracking_categories_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_tracking_categories_valid_dates
        CHECK (valid_from < valid_to)
);

/* --- Indexes ------------------------------------------------ */
CREATE INDEX IF NOT EXISTS idx_tracking_categories_business_key_current
    ON ods.tracking_categories(tracking_category_id)
    WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_tracking_categories_valid_to
    ON ods.tracking_categories(valid_to);

CREATE INDEX IF NOT EXISTS idx_tracking_categories_is_current
    ON ods.tracking_categories(is_current);

CREATE INDEX IF NOT EXISTS idx_tracking_categories_fetched_at
    ON ods.tracking_categories(fetched_at);

CREATE INDEX IF NOT EXISTS idx_tracking_categories_batch_id
    ON ods.tracking_categories(batch_id);

/* --- Trigger to maintain row_updated_at -------------------- */
CREATE TRIGGER trg_update_tracking_categories_row_updated_at
BEFORE UPDATE ON ods.tracking_categories
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ---------------------------------------- */
COMMENT ON TABLE ods.tracking_categories IS 'High‑level tracking categories for reporting dimensions; stored as Type‑2 SCD.';
COMMENT ON COLUMN ods.tracking_categories.tracking_category_id IS 'Natural business key from Xero (TrackingCategoryID).';
COMMENT ON COLUMN ods.tracking_categories.name IS 'Name of the tracking category (≤100 characters).';
COMMENT ON COLUMN ods.tracking_categories.status IS 'Status of the category – ACTIVE, ARCHIVED, or DELETED.';
COMMENT ON COLUMN ods.tracking_categories.option_count IS 'Optional count of child options (metadata only).';
COMMENT ON COLUMN ods.tracking_categories.surrogate_key IS 'Surrogate key for each version of the tracking category.';
COMMENT ON COLUMN ods.tracking_categories.valid_from IS 'Timestamp when this version becomes valid.';
COMMENT ON COLUMN ods.tracking_categories.valid_to IS 'Timestamp when this version ceases to be valid.';
COMMENT ON COLUMN ods.tracking_categories.is_current IS 'TRUE if this row represents the current version.';
