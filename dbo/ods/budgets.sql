-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero Budgets
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses BIGINT IDENTITY (PostgreSQL‑preferred).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: budget_id (UUID).
--    * Periods array (per‑account values) not expanded here – ask if a separate `budget_periods` table is needed.
-- =============================================================

/* -------------------------------------------------------------
   ods.budgets – Type‑2 Slowly Changing Dimensions table
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.budgets (
    /* --- Business / natural key ----------------------------- */
    budget_id            UUID            NOT NULL,   -- Xero BudgetID

    /* --- Tenant / organisation ------------------------------ */
    organisation_id      UUID            NOT NULL,

    /* --- Core scalar attributes ----------------------------- */
    type                 VARCHAR(10)     NOT NULL CHECK (type IN ('OVERALL','TRACKING')),
    description          VARCHAR(255),
    tracking_option_id   UUID,                   -- Present when type = 'TRACKING'
    start_date           DATE,                   -- Start of the budget period set
    total_months         INTEGER CHECK (total_months BETWEEN 1 AND 24),
    updated_date_utc     TIMESTAMPTZ,

    /* --- SCD‑2 infrastructure ------------------------------- */
    surrogate_key        BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from           TIMESTAMPTZ NOT NULL,
    valid_to             TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current           BOOLEAN     NOT NULL DEFAULT TRUE,

    /* --- Audit & status ------------------------------------- */
    fetched_at           TIMESTAMPTZ NOT NULL,
    row_created_at       TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at       TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status        ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    /* --- Batch / source tracking ---------------------------- */
    batch_id                     UUID         NOT NULL,
    landing_table_name           VARCHAR(255) NOT NULL,
    landing_record_identifier    VARCHAR(512) NOT NULL,
    raw_table_name               VARCHAR(255),
    api_call_id                  UUID,
    source_system_id             VARCHAR(100),
    source_record_modified_at    TIMESTAMPTZ,

    /* --- Constraints ---------------------------------------- */
    CONSTRAINT uq_budgets_business_key_current
        UNIQUE (budget_id, is_current) WHERE is_current,

    CONSTRAINT ck_budgets_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_budgets_valid_dates
        CHECK (valid_from < valid_to),

    /* --- Foreign Keys --------------------------------------- */
    CONSTRAINT fk_budgets_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    CONSTRAINT fk_budgets_tracking_option
        FOREIGN KEY (tracking_option_id)
        REFERENCES ods.tracking_options(tracking_option_id)
        DEFERRABLE INITIALLY DEFERRED
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_budgets_business_key_current
    ON ods.budgets(budget_id) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_budgets_valid_to
    ON ods.budgets(valid_to);

CREATE INDEX IF NOT EXISTS idx_budgets_is_current
    ON ods.budgets(is_current);

CREATE INDEX IF NOT EXISTS idx_budgets_fetched_at
    ON ods.budgets(fetched_at);

CREATE INDEX IF NOT EXISTS idx_budgets_batch_id
    ON ods.budgets(batch_id);

CREATE INDEX IF NOT EXISTS idx_budgets_organisation_id
    ON ods.budgets(organisation_id);

/* --- Trigger to maintain row_updated_at --------------------- */
CREATE TRIGGER trg_update_budgets_row_updated_at
BEFORE UPDATE ON ods.budgets
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ----------------------------------------- */
COMMENT ON TABLE ods.budgets IS 'Organisation‑level or tracking‑category budgets (up to 24 months), captured as Type‑2 slowly changing dimensions.';
COMMENT ON COLUMN ods.budgets.budget_id IS 'Natural identifier for the budget.';
COMMENT ON COLUMN ods.budgets.type IS 'OVERALL for org‑wide budgets, TRACKING for budgets against a tracking option.';
COMMENT ON COLUMN ods.budgets.total_months IS 'Number of months included in this budget (1–24).';
COMMENT ON COLUMN ods.budgets.is_current IS 'TRUE if this row represents the latest version of the budget.';
