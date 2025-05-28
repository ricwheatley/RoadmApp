-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero Contacts & ContactPersons
--  File generated: 28‑May‑2025
-- =============================================================

/* ----------------------------------------------------------------
   0.  ENUM TYPE (ensure exists)                                    */
CREATE TYPE IF NOT EXISTS ods.record_status_enum AS ENUM (
    'ACTIVE',
    'SUPERSEDED',
    'ARCHIVED',
    'REMOVED'
);

/* ----------------------------------------------------------------
   1.  Audit helper function (idempotent)                           */
CREATE OR REPLACE FUNCTION fn_update_row_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.row_updated_at := CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

/* ----------------------------------------------------------------
   2.  ODS.CONTACTS  (parent – unchanged)                           */
CREATE TABLE IF NOT EXISTS ods.contacts (
    -- Business / natural key
    contact_id                     UUID            NOT NULL,

    -- Core scalar attributes from xero_accounting.yaml Contact
    merged_to_contact_id           UUID,
    contact_number                 VARCHAR(50),
    account_number                 VARCHAR(50),
    contact_status                 VARCHAR(20),
    name                           VARCHAR(255),
    first_name                     VARCHAR(255),
    last_name                      VARCHAR(255),
    company_number                 VARCHAR(50),
    email_address                  VARCHAR(255),
    bank_account_details           VARCHAR(255),
    tax_number                     VARCHAR(50),
    accounts_receivable_tax_type   VARCHAR(50),
    accounts_payable_tax_type      VARCHAR(50),
    is_supplier                    BOOLEAN,
    is_customer                    BOOLEAN,
    sales_default_line_amount_type VARCHAR(10),
    purchases_default_line_amount_type VARCHAR(10),
    default_currency               VARCHAR(10),
    xero_network_key               VARCHAR(255),
    sales_default_account_code     VARCHAR(50),
    purchases_default_account_code VARCHAR(50),
    discount                       NUMERIC(18,2),
    website                        VARCHAR(255),
    has_attachments                BOOLEAN,
    has_validation_errors          BOOLEAN,
    status_attribute_string        VARCHAR(50),
    updated_date_utc               TIMESTAMPTZ,

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
    CONSTRAINT uq_contacts_business_key_current
        UNIQUE (contact_id, is_current) WHERE is_current,

    CONSTRAINT ck_contacts_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_contacts_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT fk_contacts_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

-- Indexes for ods.contacts
CREATE INDEX IF NOT EXISTS idx_contacts_business_key_current
        ON ods.contacts(contact_id) WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_contacts_valid_to
        ON ods.contacts(valid_to);
CREATE INDEX IF NOT EXISTS idx_contacts_is_current
        ON ods.contacts(is_current);
CREATE INDEX IF NOT EXISTS idx_contacts_fetched_at
        ON ods.contacts(fetched_at);
CREATE INDEX IF NOT EXISTS idx_contacts_batch_id
        ON ods.contacts(batch_id);
CREATE INDEX IF NOT EXISTS idx_contacts_organisation_id
        ON ods.contacts(organisation_id);

-- Row‑updated‑at trigger
CREATE TRIGGER trg_update_contacts_row_updated_at
BEFORE UPDATE ON ods.contacts
FOR EACH ROW EXECUTE FUNCTION fn_update_row_updated_at();

COMMENT ON TABLE ods.contacts IS 'Type‑2 SCD table storing historical versions of Xero Contact records.';
COMMENT ON COLUMN ods.contacts.contact_id IS 'Natural business key (Xero ContactID).';

/* ----------------------------------------------------------------
   3.  ODS.CONTACT_PERSONS  (child of contact)                      */
CREATE TABLE IF NOT EXISTS ods.contact_persons (
    -- Business / natural key (composite – Option A)
    contact_id     UUID            NOT NULL,  -- Parent ContactID
    first_name     VARCHAR(255)    NOT NULL,
    last_name      VARCHAR(255)    NOT NULL,
    email_address  VARCHAR(255)    NOT NULL,

    include_in_emails BOOLEAN,

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
    CONSTRAINT uq_contact_persons_business_key_current
        UNIQUE (contact_id, first_name, last_name, email_address, is_current)
        WHERE is_current,

    CONSTRAINT ck_contact_persons_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_contact_persons_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT fk_contact_persons_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

-- Indexes for ods.contact_persons
CREATE INDEX IF NOT EXISTS idx_contact_persons_business_key_current
    ON ods.contact_persons(contact_id, first_name, last_name, email_address)
    WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_contact_persons_valid_to
    ON ods.contact_persons(valid_to);
CREATE INDEX IF NOT EXISTS idx_contact_persons_is_current
    ON ods.contact_persons(is_current);
CREATE INDEX IF NOT EXISTS idx_contact_persons_fetched_at
    ON ods.contact_persons(fetched_at);
CREATE INDEX IF NOT EXISTS idx_contact_persons_batch_id
    ON ods.contact_persons(batch_id);
CREATE INDEX IF NOT EXISTS idx_contact_persons_organisation_id
    ON ods.contact_persons(organisation_id);

-- Row‑updated‑at trigger for child table
CREATE TRIGGER trg_update_contact_persons_row_updated_at
BEFORE UPDATE ON ods.contact_persons
FOR EACH ROW EXECUTE FUNCTION fn_update_row_updated_at();

COMMENT ON TABLE ods.contact_persons IS 'Type‑2 SCD table capturing historical details of individual ContactPersons linked to a Contact.';
COMMENT ON COLUMN ods.contact_persons.contact_id IS 'Parent Xero ContactID.';
COMMENT ON COLUMN ods.contact_persons.first_name IS 'First name of the contact person.';
COMMENT ON COLUMN ods.contact_persons.last_name  IS 'Last name of the contact person.';
COMMENT ON COLUMN ods.contact_persons.email_address IS 'Email address of the contact person.';
COMMENT ON COLUMN ods.contact_persons.include_in_emails IS 'Whether this person should be included on emails.';
