-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero ManualJournalLines
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses BIGINT IDENTITY (PostgreSQL‑recommended).
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Business key: journal_line_id (UUID).
--    * Parent linkage via manual_journal_id + organisation_id; FK to ods.manual_journals.
--    * Tracking categories/options not expanded here – request a `manual_journal_line_tracking` table if required.
-- =============================================================

/* -------------------------------------------------------------
   ods.manual_journal_lines – Manual‑journal lines captured as SCD‑2 records
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.manual_journal_lines (
    /* --- Business / natural key -------------------------------- */
    journal_line_id      UUID            NOT NULL,  -- Xero ManualJournalLineID

    /* --- Relationship keys ------------------------------------- */
    manual_journal_id    UUID            NOT NULL,
    organisation_id      UUID            NOT NULL,

    /* --- Core scalar attributes -------------------------------- */
    account_code         VARCHAR(10),            -- GL code
    account_id           UUID,
    account_type         VARCHAR(10),            -- ASSET | EQUITY | EXPENSE | LIABILITY | REVENUE | BANK
    description          VARCHAR(4000),
    net_amount           NUMERIC(18,2),
    gross_amount         NUMERIC(18,2),
    tax_amount           NUMERIC(18,2),
    tax_type             VARCHAR(25),
    tax_name             VARCHAR(50),

    /* --- SCD‑2 infrastructure ---------------------------------- */
    surrogate_key     BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from        TIMESTAMPTZ     NOT NULL,
    valid_to          TIMESTAMPTZ     NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current        BOOLEAN         NOT NULL DEFAULT TRUE,

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
    CONSTRAINT uq_manual_journal_lines_business_key_current
        UNIQUE (journal_line_id, is_current) WHERE is_current,

    CONSTRAINT fk_mj_lines_manual_journal
        FOREIGN KEY (manual_journal_id)
        REFERENCES ods.manual_journals(manual_journal_id),

    CONSTRAINT fk_mj_lines_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    CONSTRAINT ck_mj_lines_account_code_format
        CHECK (account_code IS NULL OR account_code ~ '^[A-Z0-9]{1,10}$'),

    CONSTRAINT ck_mj_lines_account_type
        CHECK (account_type IS NULL OR account_type IN ('ASSET','EQUITY','EXPENSE','LIABILITY','REVENUE','BANK')),

    CONSTRAINT ck_mj_lines_amounts_non_negative
        CHECK (
            (net_amount     IS NULL OR net_amount     >= 0) AND
            (gross_amount   IS NULL OR gross_amount   >= 0) AND
            (tax_amount     IS NULL OR tax_amount     >= 0)
        ),

    CONSTRAINT ck_mj_lines_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_mj_lines_valid_dates
        CHECK (valid_from < valid_to)
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_mj_lines_business_key_current
    ON ods.manual_journal_lines(journal_line_id) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_mj_lines_manual_journal_id
    ON ods.manual_journal_lines(manual_journal_id);

CREATE INDEX IF NOT EXISTS idx_mj_lines_valid_to
    ON ods.manual_journal_lines(valid_to);

CREATE INDEX IF NOT EXISTS idx_mj_lines_is_current
    ON ods.manual_journal_lines(is_current);

CREATE INDEX IF NOT EXISTS idx_mj_lines_fetched_at
    ON ods.manual_journal_lines(fetched_at);

CREATE INDEX IF NOT EXISTS idx_mj_lines_batch_id
    ON ods.manual_journal_lines(batch_id);

CREATE INDEX IF NOT EXISTS idx_mj_lines_organisation_id
    ON ods.manual_journal_lines(organisation_id);

/* --- Trigger to maintain row_updated_at ----------------------- */
CREATE TRIGGER trg_update_mj_lines_row_updated_at
BEFORE UPDATE ON ods.manual_journal_lines
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ------------------------------------------- */
COMMENT ON TABLE ods.manual_journal_lines IS 'Manual‑journal line items stored as Type‑2 SCD records.';
COMMENT ON COLUMN ods.manual_journal_lines.journal_line_id IS 'Xero ManualJournalLineID (business key).';
COMMENT ON COLUMN ods.manual_journal_lines.account_type IS 'GL account class: ASSET | EQUITY | EXPENSE | LIABILITY | REVENUE | BANK.';
COMMENT ON COLUMN ods.manual_journal_lines.is_current IS 'TRUE if this row is the current version of the line item.';
