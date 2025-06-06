-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero OrganisationPaymentTerms
--  Generated: 28‑May‑2025
--  Notes:
--    * Surrogate key uses IDENTITY.
--    * Assumes ods.record_status_enum and fn_update_row_updated_at() already exist.
--    * Composite natural key: organisation_id + term_side (BILLS or SALES).
-- =============================================================

/* -------------------------------------------------------------
   ods.organisation_payment_terms – default payment terms for organisations
   ------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS ods.organisation_payment_terms (
    /* --- Business / natural key -------------------------------- */
    organisation_id UUID        NOT NULL,
    term_side       VARCHAR(5)  NOT NULL,   -- BILLS or SALES

    /* --- Scalar attributes (PaymentTerm schema) ---------------- */
    day        INTEGER,
    term_type  VARCHAR(25) NOT NULL,

    /* --- SCD‑2 infrastructure ---------------------------------- */
    surrogate_key BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    valid_from    TIMESTAMPTZ NOT NULL,
    valid_to      TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current    BOOLEAN     NOT NULL DEFAULT TRUE,

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
    CONSTRAINT uq_org_payment_terms_business_key_current
        UNIQUE (organisation_id, term_side, is_current) WHERE is_current,

    CONSTRAINT fk_org_payment_terms_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id),

    CONSTRAINT ck_org_payment_terms_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_org_payment_terms_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT ck_org_payment_terms_side
        CHECK (term_side IN ('BILLS','SALES')),

    CONSTRAINT ck_org_payment_terms_type
        CHECK (term_type IN ('DAYSAFTERBILLDATE','DAYSAFTERBILLMONTH','OFCURRENTMONTH','OFFOLLOWINGMONTH'))
);

/* --- Indexes -------------------------------------------------- */
CREATE INDEX IF NOT EXISTS idx_org_payment_terms_business_key_current
    ON ods.organisation_payment_terms(organisation_id, term_side) WHERE is_current;

CREATE INDEX IF NOT EXISTS idx_org_payment_terms_valid_to
    ON ods.organisation_payment_terms(valid_to);

CREATE INDEX IF NOT EXISTS idx_org_payment_terms_is_current
    ON ods.organisation_payment_terms(is_current);

CREATE INDEX IF NOT EXISTS idx_org_payment_terms_fetched_at
    ON ods.organisation_payment_terms(fetched_at);

CREATE INDEX IF NOT EXISTS idx_org_payment_terms_batch_id
    ON ods.organisation_payment_terms(batch_id);

/* --- Row‑updated‑at trigger ---------------------------------- */
CREATE TRIGGER trg_update_org_payment_terms_row_updated_at
BEFORE UPDATE ON ods.organisation_payment_terms
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* --- Documentation ------------------------------------------- */
COMMENT ON TABLE ods.organisation_payment_terms IS 'Default payment terms (Bills or Sales) for Xero organisations, stored as a Type‑2 SCD to track historical changes.';
COMMENT ON COLUMN ods.organisation_payment_terms.organisation_id IS 'Foreign key to ods.organisations (business key).';
COMMENT ON COLUMN ods.organisation_payment_terms.term_side IS 'Indicates which side of trading the term applies to: BILLS (Accounts Payable) or SALES (Accounts Receivable).';
COMMENT ON COLUMN ods.organisation_payment_terms.day IS 'Day of the month (0–31) used with the term type.'; 
COMMENT ON COLUMN ods.organisation_payment_terms.term_type IS 'Payment term type (DAYSAFTERBILLDATE, DAYSAFTERBILLMONTH, OFCURRENTMONTH, OFFOLLOWINGMONTH).';
COMMENT ON COLUMN ods.organisation_payment_terms.surrogate_key IS 'Surrogate key for each version of the organisation payment term record.';
COMMENT ON COLUMN ods.organisation_payment_terms.valid_from IS 'Timestamp at which this version becomes valid.';
COMMENT ON COLUMN ods.organisation_payment_terms.valid_to IS 'Timestamp at which this version ceases to be valid.';
COMMENT ON COLUMN ods.organisation_payment_terms.is_current IS 'TRUE if this row is the current version.';
