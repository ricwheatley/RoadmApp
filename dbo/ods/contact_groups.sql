-- =============================================================
--  ODS SCD‑2 DDL SCRIPT – Xero ContactGroups (membership)
--  File generated: 28‑May‑2025
-- =============================================================

/* ----------------------------------------------------------------
   2.  ODS.CONTACT_GROUPS  (child of ods.contacts)
       Captures the membership of a Contact in one or more Contact Groups.
       Source fields per Xero schema: ContactGroups.ContactGroupID, Name, Status, HasValidationErrors
------------------------------------------------------------------ */
CREATE TABLE IF NOT EXISTS ods.contact_groups (
    -- Business / natural key (one row per group per contact)
    contact_id         UUID           NOT NULL,              -- Parent ContactID
    contact_group_id   UUID           NOT NULL,              -- Group GUID

    -- Core scalar attributes from ContactGroup object
    name               VARCHAR(255),                         -- Group name
    group_status       VARCHAR(20),                          -- ACTIVE / DELETED
    has_validation_errors BOOLEAN,

    -- SCD‑2 columns
    surrogate_key   GENERATED BY DEFAULT AS IDENTITY,
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
    CONSTRAINT uq_contact_groups_business_key_current
        UNIQUE (contact_id, contact_group_id, is_current) WHERE is_current,

    CONSTRAINT ck_contact_groups_record_status
        CHECK (record_status IN ('ACTIVE','SUPERSEDED','ARCHIVED','REMOVED')),

    CONSTRAINT ck_contact_groups_valid_dates
        CHECK (valid_from < valid_to),

    CONSTRAINT fk_contact_groups_organisation
        FOREIGN KEY (organisation_id)
        REFERENCES ods.organisations(organisation_id)
);

-- Indexes for ods.contact_groups
CREATE INDEX IF NOT EXISTS idx_contact_groups_business_key_current
    ON ods.contact_groups(contact_id, contact_group_id) WHERE is_current;
CREATE INDEX IF NOT EXISTS idx_contact_groups_valid_to
    ON ods.contact_groups(valid_to);
CREATE INDEX IF NOT EXISTS idx_contact_groups_is_current
    ON ods.contact_groups(is_current);
CREATE INDEX IF NOT EXISTS idx_contact_groups_fetched_at
    ON ods.contact_groups(fetched_at);
CREATE INDEX IF NOT EXISTS idx_contact_groups_batch_id
    ON ods.contact_groups(batch_id);
CREATE INDEX IF NOT EXISTS idx_contact_groups_organisation_id
    ON ods.contact_groups(organisation_id);

-- Row‑updated‑at trigger for ods.contact_groups
CREATE TRIGGER trg_update_contact_groups_row_updated_at
BEFORE UPDATE ON ods.contact_groups
FOR EACH ROW EXECUTE FUNCTION ods.fn_update_row_updated_at();

/* ----------------------------------------------------------------
   3.  Documentation                                                */
COMMENT ON TABLE ods.contact_groups IS 'Type‑2 SCD table storing historical membership of a Xero Contact in Contact Groups.';
COMMENT ON COLUMN ods.contact_groups.contact_id IS 'Parent Xero ContactID.';
COMMENT ON COLUMN ods.contact_groups.contact_group_id IS 'Unique identifier for the Contact Group.';
COMMENT ON COLUMN ods.contact_groups.name IS 'Name of the Contact Group.';
COMMENT ON COLUMN ods.contact_groups.group_status IS 'Status of the group (ACTIVE / DELETED).';
COMMENT ON COLUMN ods.contact_groups.has_validation_errors IS 'Indicates whether Xero reported validation errors for the group object.';
