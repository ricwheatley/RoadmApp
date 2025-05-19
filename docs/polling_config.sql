-- Configuration table for tenant polling schedules
CREATE TYPE utils.poll_frequency AS ENUM ('OFF','DAILY','WEEKLY');

CREATE TABLE utils.polling_config (
    tenant_id     TEXT       NOT NULL,
    endpoint_key  TEXT       NOT NULL,
    frequency     utils.poll_frequency NOT NULL DEFAULT 'OFF',
    run_time      TIME NOT NULL DEFAULT '00:00',
    PRIMARY KEY (tenant_id, endpoint_key)
);

