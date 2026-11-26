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
