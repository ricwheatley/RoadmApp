
    -- FUNCTION: etl._upsert_payment_invoice_payment_object(uuid, uuid, jsonb, uuid, uuid, timestamp with time zone, timestamp with time zone)

    DROP FUNCTION IF EXISTS etl._upsert_payment_invoice_payment_object(uuid, uuid, jsonb, uuid, uuid, timestamp with time zone, timestamp with time zone);

    CREATE OR REPLACE FUNCTION etl._upsert_payment_invoice_payment_object(
        p_tenant_id uuid,
        p_payment_id uuid,
        p_child_data jsonb,
        p_batch_id uuid,
        p_parent_api_call_id uuid,
        p_parent_raw_fetched_at timestamp with time zone,
        p_parent_source_record_modified_at timestamp with time zone
    )
    RETURNS jsonb
    LANGUAGE plpgsql
    COST 100
    VOLATILE SECURITY DEFINER PARALLEL UNSAFE
    SET search_path = etl, public
AS $$
DECLARE
    v_elem jsonb;
    v_child_inserted INT := 0;
    v_child_updated INT := 0;
    v_child_skipped INT := 0;
    v_child_errors INT := 0;
    v_child_upsert_id bigint;
    v_is_insert boolean;
    v_sqlstate TEXT;
    v_errmsg TEXT;
    v_context TEXT;
    v_source_identifier TEXT;
    v_current_timestamp timestamptz;

    -- extracted business fields
    v_invoice_id uuid;
    v_invoice_number text;
    v_credit_note_number text;
    v_code text;
    v_date date;
    v_currency_rate double precision;
    v_amount double precision;
    v_bank_amount double precision;
    v_reference text;
    v_is_reconciled boolean;
    v_status text;
    v_payment_type text;
    v_updated_date_utc timestamptz;
    v_batch_payment_id uuid;
    v_bank_account_number text;
    v_particulars text;
    v_details text;
    v_has_account boolean;
    v_has_validation_errors boolean;
    v_status_attribute_string text;

BEGIN
    IF p_child_data IS NULL OR jsonb_typeof(p_child_data) IS DISTINCT FROM 'object' THEN
        RETURN jsonb_build_object(
            'inserted', 0,
            'updated', 0,
            'skipped', 0,
            'errors', 0
        );
    END IF;

    v_elem := p_child_data;
    v_current_timestamp := clock_timestamp();

    -- Extract business fields
    v_invoice_id := (v_elem->'Invoice'->>'InvoiceID')::uuid;
    v_invoice_number := v_elem->'Invoice'->>'InvoiceNumber';
    v_credit_note_number := v_elem->'CreditNote'->>'CreditNoteNumber';
    v_code := v_elem->>'Code';
    v_date := etl.parse_xero_timestamp(v_elem->>'Date');
    v_currency_rate := (v_elem->>'CurrencyRate')::double precision;
    v_amount := (v_elem->>'Amount')::double precision;
    v_bank_amount := (v_elem->>'BankAmount')::double precision;
    v_reference := v_elem->>'Reference';
    v_is_reconciled := (v_elem->>'IsReconciled')::boolean;
    v_status := v_elem->>'Status';
    v_payment_type := v_elem->>'PaymentType';
    v_updated_date_utc := etl.parse_xero_timestamp(v_elem->>'UpdatedDateUTC');
    v_batch_payment_id := (v_elem->'BatchPayment'->>'BatchPaymentID')::uuid;
    v_bank_account_number := v_elem->'BankAccount'->>'AccountNumber';
    v_particulars := v_elem->>'Particulars';
    v_details := v_elem->>'Details';
    v_has_account := (v_elem->>'HasAccount')::boolean;
    v_has_validation_errors := (v_elem->>'HasValidationErrors')::boolean;
    v_status_attribute_string := v_elem->>'StatusAttributeString';

    v_source_identifier := p_payment_id::text || '_' || coalesce(v_invoice_id::text, 'no-invoice');

    IF v_invoice_id IS NULL OR v_date IS NULL OR v_amount IS NULL THEN
        v_child_skipped := v_child_skipped + 1;
        RETURN jsonb_build_object(
            'inserted', 0,
            'updated', 0,
            'skipped', v_child_skipped,
            'errors', 0
        );
    END IF;

    BEGIN
        INSERT INTO landing.invoice_payments (
            organisation_id,
            invoice_id,
            payment_id,
            invoice_number,
            credit_note_number,
            code,
            date,
            currency_rate,
            amount,
            bank_amount,
            reference,
            is_reconciled,
            status,
            payment_type,
            updated_date_utc,
            batch_payment_id,
            bank_account_number,
            particulars,
            details,
            has_account,
            has_validation_errors,
            status_attribute_string,
            batch_id,
            api_call_id,
            fetched_at,
            processed_at,
            row_created_at,
            row_updated_at,
            record_status,
            raw_table_name,
            source_system_id,
            source_record_modified_at,
            is_deleted,
            upsert_status,
            upsert_message
        )
        VALUES (
            p_tenant_id,
            v_invoice_id,
            p_payment_id,
            v_invoice_number,
            v_credit_note_number,
            v_code,
            v_date,
            v_currency_rate,
            v_amount,
            v_bank_amount,
            v_reference,
            v_is_reconciled,
            v_status,
            v_payment_type,
            v_updated_date_utc,
            v_batch_payment_id,
            v_bank_account_number,
            v_particulars,
            v_details,
            v_has_account,
            v_has_validation_errors,
            v_status_attribute_string,
            p_batch_id,
            p_parent_api_call_id,
            p_parent_raw_fetched_at,
            v_current_timestamp,
            v_current_timestamp,
            v_current_timestamp,
            'ACTIVE',
            'raw.payments',
            'XERO',
            p_parent_source_record_modified_at,
            FALSE,
            'PENDING',
            'Child inserted'
        )
        ON CONFLICT (organisation_id, invoice_id, payment_id) DO UPDATE
        SET
            invoice_number = EXCLUDED.invoice_number,
            credit_note_number = EXCLUDED.credit_note_number,
            code = EXCLUDED.code,
            date = EXCLUDED.date,
            currency_rate = EXCLUDED.currency_rate,
            amount = EXCLUDED.amount,
            bank_amount = EXCLUDED.bank_amount,
            reference = EXCLUDED.reference,
            is_reconciled = EXCLUDED.is_reconciled,
            status = EXCLUDED.status,
            payment_type = EXCLUDED.payment_type,
            updated_date_utc = EXCLUDED.updated_date_utc,
            batch_payment_id = EXCLUDED.batch_payment_id,
            bank_account_number = EXCLUDED.bank_account_number,
            particulars = EXCLUDED.particulars,
            details = EXCLUDED.details,
            has_account = EXCLUDED.has_account,
            has_validation_errors = EXCLUDED.has_validation_errors,
            status_attribute_string = EXCLUDED.status_attribute_string,
            batch_id = EXCLUDED.batch_id,
            api_call_id = EXCLUDED.api_call_id,
            fetched_at = EXCLUDED.fetched_at,
            processed_at = v_current_timestamp,
            row_updated_at = v_current_timestamp,
            record_status = EXCLUDED.record_status,
            raw_table_name = EXCLUDED.raw_table_name,
            source_system_id = EXCLUDED.source_system_id,
            source_record_modified_at = EXCLUDED.source_record_modified_at,
            is_deleted = EXCLUDED.is_deleted,
            upsert_status = 'PENDING',
            upsert_message = 'Child updated'
        WHERE EXCLUDED.fetched_at > landing.invoice_payments.fetched_at
        AND (
            landing.invoice_payments.invoice_number IS DISTINCT FROM EXCLUDED.invoice_number OR
            landing.invoice_payments.credit_note_number IS DISTINCT FROM EXCLUDED.credit_note_number OR
            landing.invoice_payments.code IS DISTINCT FROM EXCLUDED.code OR
            landing.invoice_payments.date IS DISTINCT FROM EXCLUDED.date OR
            landing.invoice_payments.currency_rate IS DISTINCT FROM EXCLUDED.currency_rate OR
            landing.invoice_payments.amount IS DISTINCT FROM EXCLUDED.amount OR
            landing.invoice_payments.bank_amount IS DISTINCT FROM EXCLUDED.bank_amount OR
            landing.invoice_payments.reference IS DISTINCT FROM EXCLUDED.reference OR
            landing.invoice_payments.is_reconciled IS DISTINCT FROM EXCLUDED.is_reconciled OR
            landing.invoice_payments.status IS DISTINCT FROM EXCLUDED.status OR
            landing.invoice_payments.payment_type IS DISTINCT FROM EXCLUDED.payment_type OR
            landing.invoice_payments.updated_date_utc IS DISTINCT FROM EXCLUDED.updated_date_utc OR
            landing.invoice_payments.batch_payment_id IS DISTINCT FROM EXCLUDED.batch_payment_id OR
            landing.invoice_payments.bank_account_number IS DISTINCT FROM EXCLUDED.bank_account_number OR
            landing.invoice_payments.particulars IS DISTINCT FROM EXCLUDED.particulars OR
            landing.invoice_payments.details IS DISTINCT FROM EXCLUDED.details OR
            landing.invoice_payments.has_account IS DISTINCT FROM EXCLUDED.has_account OR
            landing.invoice_payments.has_validation_errors IS DISTINCT FROM EXCLUDED.has_validation_errors OR
            landing.invoice_payments.status_attribute_string IS DISTINCT FROM EXCLUDED.status_attribute_string OR
            landing.invoice_payments.source_record_modified_at IS DISTINCT FROM EXCLUDED.source_record_modified_at
        )
        RETURNING (xmax = 0) AS is_insert, landing_invoice_payments_key INTO v_is_insert, v_child_upsert_id;

        IF v_is_insert THEN
            v_child_inserted := v_child_inserted + 1;
        ELSIF v_child_upsert_id IS NOT NULL THEN
            v_child_updated := v_child_updated + 1;
        END IF;

        PERFORM etl.add_log_entry(
            p_process_name := 'etl._upsert_payment_invoice_payment_object',
            p_target_table_name := 'landing.invoice_payments',
            p_source_identifier := v_source_identifier,
            p_log_level := 'INFO',
            p_error_message := format('Upsert OK: inserted=%, updated=%, id=%', v_is_insert, NOT v_is_insert, v_child_upsert_id),
            p_error_context := NULL,
            p_sql_state_code := NULL,
            p_tenant_id := p_tenant_id,
            p_batch_id := p_batch_id
        );

    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS v_sqlstate = RETURNED_SQLSTATE, v_errmsg = MESSAGE_TEXT, v_context = PG_EXCEPTION_CONTEXT;

        v_child_errors := v_child_errors + 1;

        PERFORM etl.add_log_entry(
            p_process_name := 'etl._upsert_payment_invoice_payment_object',
            p_target_table_name := 'landing.invoice_payments',
            p_source_identifier := v_source_identifier,
            p_log_level := 'ERROR',
            p_error_message := format('Upsert error for invoice_id %, payment_id %: %', v_invoice_id, p_payment_id, v_errmsg),
            p_error_context := v_context,
            p_sql_state_code := v_sqlstate,
            p_tenant_id := p_tenant_id,
            p_batch_id := p_batch_id
        );
    END;

    RETURN jsonb_build_object(
        'inserted', v_child_inserted,
        'updated', v_child_updated,
        'skipped', v_child_skipped,
        'errors', v_child_errors
    );
END;
$$;

ALTER FUNCTION etl._upsert_payment_invoice_payment_object(uuid, uuid, jsonb, uuid, uuid, timestamp with time zone, timestamp with time zone)
OWNER TO postgres;
