-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero ContactAddresses
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
   2.  ODS.CONTACT_ADDRESSES  (child of ods.contacts)               */
CREATE TABLE IF NOT EXISTS ods.contact_addresses (
    -- Business / natural key (one address per type per contact)
    contact_id   UUID         NOT NULL,               -- Parent ContactID
    address_type VARCHAR(20)  NOT NULL,               -- STREET, POBOX, DELIVERY

    -- Core scalar attributes mapped from Address object
    address_line1 VARCHAR(500),
    address_line2 VARCHAR(500),
    address_line3 VARCHAR(500),
    address_line4 VARCHAR(500),
    city          VARCHAR(255),
    region        VARCHAR(255),
    postal_code   VARCHAR(50),
    country       VARCHAR(50),
    attention_to  VARCHAR(255),

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
    CONSTRAINT uq_contact_addresses_business_key_current
        UNIQUE (contact_id, address_type, is_current) WHERE is_current,

    CONSTRAINT ck_contact_addresses_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_contact_addresses_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT fk_contact_addresses_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

-- Indexes for ods.contact_addresses
CREATE INDEX IF NOT EXISTS idx_contact_addresses_business_key_current
    ON ods.contact_addresses(contact_id, address_type)
    WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_contact_addresses_valid_to
    ON ods.contact_addresses(valid_to);
CREATE INDEX IF NOT EXISTS idx_contact_addresses_is_current
    ON ods.contact_addresses(is_current);
CREATE INDEX IF NOT EXISTS idx_contact_addresses_fetched_at
    ON ods.contact_addresses(fetched_at);
CREATE INDEX IF NOT EXISTS idx_contact_addresses_batch_id
    ON ods.contact_addresses(batch_id);
CREATE INDEX IF NOT EXISTS idx_contact_addresses_organisation_id
    ON ods.contact_addresses(organisation_id);

-- Row‑updated‑at trigger for ods.contact_addresses
CREATE TRIGGER trg_update_contact_addresses_row_updated_at
BEFORE UPDATE ON ods.contact_addresses
FOR EACH ROW EXECUTE FUNCTION fn_update_row_updated_at();

/* ----------------------------------------------------------------
   3.  Documentation                                                */
COMMENT ON TABLE ods.contact_addresses IS 'Type‑2 SCD table storing historical address details for a Xero Contact.';
COMMENT ON COLUMN ods.contact_addresses.contact_id IS 'Parent Xero ContactID.';
COMMENT ON COLUMN ods.contact_addresses.address_type IS 'Address category (STREET, POBOX, DELIVERY, etc.).';
COMMENT ON COLUMN ods.contact_addresses.address_line1 IS 'Address line 1.';
COMMENT ON COLUMN ods.contact_addresses.city        IS 'City or locality.';
COMMENT ON COLUMN ods.contact_addresses.country     IS 'Country name or ISO code.';
