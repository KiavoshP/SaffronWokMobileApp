CREATE TABLE IF NOT EXISTS phone_verification_challenges (
    id uuid PRIMARY KEY,
    phone text NOT NULL UNIQUE,
    code_hash bytea NOT NULL,
    is_local_test boolean NOT NULL,
    issued_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    attempts_remaining integer NOT NULL CHECK (attempts_remaining BETWEEN 0 AND 5),
    consumed_at timestamptz
);

CREATE TABLE IF NOT EXISTS phone_verification_receipts (
    token_hash bytea PRIMARY KEY,
    phone text NOT NULL,
    is_local_test boolean NOT NULL,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz
);

CREATE TABLE IF NOT EXISTS phone_verification_sms_queue (
    id uuid PRIMARY KEY,
    encrypted_text bytea NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    expires_at timestamptz NOT NULL,
    lease_id uuid,
    leased_until timestamptz
);

CREATE INDEX IF NOT EXISTS ix_phone_verification_sms_queue_expiry
    ON phone_verification_sms_queue (expires_at);

CREATE TABLE IF NOT EXISTS customers (
    id uuid PRIMARY KEY,
    full_name text NOT NULL,
    phone text NOT NULL UNIQUE,
    password_hash text NOT NULL,
    is_local_test boolean NOT NULL
);

CREATE TABLE IF NOT EXISTS customer_sessions (
    token_hash bytea PRIMARY KEY,
    customer_id uuid NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
    expires_at timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_customer_sessions_expires_at ON customer_sessions (expires_at);
