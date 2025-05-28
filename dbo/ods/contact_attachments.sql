-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero ContactAttachments
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
   2.  ODS.CONTACT_ATTACHMENTS  (child of ods.contacts)
       Stores attachments associated with a Contact and tracks history.
       Source fields per Xero Attachment schema: AttachmentID, FileName, Url, MimeType, ContentLength, IncludeOnline fileciteturn10file9
------------------------------------------------------------------ */
CREATE TABLE IF NOT EXISTS ods.contact_attachments (
    -- Business / natural key (one row per attachment per contact)
    contact_id       UUID           NOT NULL,  -- Parent ContactID
    attachment_id    UUID           NOT NULL,  -- Unique attachment GUID

    -- Core scalar attributes from Attachment object
    file_name        VARCHAR(255),             -- Attachment file name
    url              VARCHAR(1024),           -- Public URL to the file in Xero
    mime_type        VARCHAR(100),            -- MIME type, e.g. image/jpg
    content_length   INTEGER,                 -- File size in bytes
    include_online   BOOLEAN,                 -- Include with online invoice flag

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
    batch_id                  UUID         NOT NULL,
    landing_table_name        VARCHAR(255) NOT NULL,
    landing_record_identifier VARCHAR(512) NOT NULL,
    raw_table_name            VARCHAR(255),
    api_call_id               UUID,
    source_system_id          VARCHAR(100),
    source_record_modified_at TIMESTAMPTZ,

    -- Constraints
    CONSTRAINT uq_contact_attachments_business_key_current
        UNIQUE (contact_id, attachment_id, is_current)
        WHERE is_current,

    CONSTRAINT ck_contact_attachments_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_contact_attachments_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT fk_contact_attachments_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

-- Indexes for ods.contact_attachments
CREATE INDEX IF NOT EXISTS idx_contact_attachments_business_key_current
    ON ods.contact_attachments(contact_id, attachment_id) WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_contact_attachments_valid_to
    ON ods.contact_attachments(valid_to);
CREATE INDEX IF NOT EXISTS idx_contact_attachments_is_current
    ON ods.contact_attachments(is_current);
CREATE INDEX IF NOT EXISTS idx_contact_attachments_fetched_at
    ON ods.contact_attachments(fetched_at);
CREATE INDEX IF NOT EXISTS idx_contact_attachments_batch_id
    ON ods.contact_attachments(batch_id);
CREATE INDEX IF NOT EXISTS idx_contact_attachments_organisation_id
    ON ods.contact_attachments(organisation_id);

-- Row‑updated‑at trigger for ods.contact_attachments
CREATE TRIGGER trg_update_contact_attachments_row_updated_at
BEFORE UPDATE ON ods.contact_attachments
FOR EACH ROW EXECUTE FUNCTION fn_update_row_updated_at();

/* ----------------------------------------------------------------
   3.  Documentation                                                */
COMMENT ON TABLE ods.contact_attachments IS 'Type‑2 SCD table storing historical attachments linked to a Xero Contact.';
COMMENT ON COLUMN ods.contact_attachments.contact_id IS 'Parent Xero ContactID.';
COMMENT ON COLUMN ods.contact_attachments.attachment_id IS 'Unique identifier for the attachment file.';
COMMENT ON COLUMN ods.contact_attachments.file_name IS 'Name of the attachment file.';
COMMENT ON COLUMN ods.contact_attachments.url IS 'Public URL to download the attachment from Xero.';
COMMENT ON COLUMN ods.contact_attachments.mime_type IS 'MIME type of the attachment, e.g., image/jpg.';
COMMENT ON COLUMN ods.contact_attachments.content_length IS 'Size of the attachment in bytes.';
COMMENT ON COLUMN ods.contact_attachments.include_online IS 'Flag indicating whether the attachment is included with online invoice.';
