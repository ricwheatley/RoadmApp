-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero ContactPhones
--  File generated: 28‑May‑2025
-- =============================================================

/* ----------------------------------------------------------------
   0.  ENUM TYPE (ensure exists – shared across ODS)                */
CREATE TYPE IF NOT EXISTS ods.record_status_enum AS ENUM (
    'ACTIVE',
    'SUPERSEDED',
    'ARCHIVED',
    'REMOVED'
);

/* ----------------------------------------------------------------
   1.  Audit helper function (idempotent – shared)                  */
CREATE OR REPLACE FUNCTION fn_update_row_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.row_updated_at := CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

/* ----------------------------------------------------------------
   2.  ODS.CONTACT_PHONES  (child of ods.contacts)                  */
CREATE TABLE IF NOT EXISTS ods.contact_phones (
    -- Business / natural key (one phone per type per contact)
    contact_id   UUID         NOT NULL,               -- Parent ContactID
    phone_type   VARCHAR(20)  NOT NULL,               -- DEFAULT, DDI, MOBILE, FAX, OFFICE

    -- Core scalar attributes mapped from Phone object
    phone_number        VARCHAR(50),
    phone_area_code     VARCHAR(10),
    phone_country_code  VARCHAR(20),

    -- SCD‑2 columns
    surrogate_key   SERIAL PRIMARY KEY,
    valid_from      TIMESTAMPTZ NOT NULL,
    valid_to        TIMESTAMPTZ NOT NULL DEFAULT '9999-12-31 23:59:59.999999+00',
    is_current      BOOLEAN     NOT NULL DEFAULT TRUE,

    -- Tenant / organisation
    organisation_id UUID        NOT NULL,

    -- Audit & status
    fetched_at       TIMESTAMPTZ NOT NULL,
    row_created_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    row_updated_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    record_status    ods.record_status_enum NOT NULL DEFAULT 'ACTIVE',

    -- Batch / source tracking
    batch_id                  UUID        NOT NULL,
    landing_table_name        VARCHAR(255) NOT NULL,
    landing_record_identifier VARCHAR(512) NOT NULL,
    raw_table_name            VARCHAR(255),
    api_call_id               UUID,
    source_system_id          VARCHAR(100),
    source_record_modified_at TIMESTAMPTZ,

    -- Constraints
    CONSTRAINT uq_contact_phones_business_key_current
        UNIQUE (contact_id, phone_type, is_current) WHERE is_current,

    CONSTRAINT ck_contact_phones_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_contact_phones_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT fk_contact_phones_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

-- Indexes for ods.contact_phones
CREATE INDEX IF NOT EXISTS idx_contact_phones_business_key_current
    ON ods.contact_phones(contact_id, phone_type) WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_contact_phones_valid_to
    ON ods.contact_phones(valid_to);
CREATE INDEX IF NOT EXISTS idx_contact_phones_is_current
    ON ods.contact_phones(is_current);
CREATE INDEX IF NOT EXISTS idx_contact_phones_fetched_at
    ON ods.contact_phones(fetched_at);
CREATE INDEX IF NOT EXISTS idx_contact_phones_batch_id
    ON ods.contact_phones(batch_id);
CREATE INDEX IF NOT EXISTS idx_contact_phones_organisation_id
    ON ods.contact_phones(organisation_id);

-- Row‑updated‑at trigger for ods.contact_phones
CREATE TRIGGER trg_update_contact_phones_row_updated_at
BEFORE UPDATE ON ods.contact_phones
FOR EACH ROW EXECUTE FUNCTION fn_update_row_updated_at();

/* ----------------------------------------------------------------
   3.  Documentation                                                */
COMMENT ON TABLE ods.contact_phones IS 'Type‑2 SCD table storing historical phone details for a Xero Contact.';
COMMENT ON COLUMN ods.contact_phones.contact_id IS 'Parent Xero ContactID.';
COMMENT ON COLUMN ods.contact_phones.phone_type IS 'Phone category (DEFAULT, DDI, MOBILE, FAX, OFFICE, etc.).';
COMMENT ON COLUMN ods.contact_phones.phone_number IS 'Subscriber phone number (max length 50).';
COMMENT ON COLUMN ods.contact_phones.phone_area_code IS 'Telephone area code (max length 10).';
COMMENT ON COLUMN ods.contact_phones.phone_country_code IS 'Country dialling code (max length 20).';
