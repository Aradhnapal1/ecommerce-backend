-- Security hardening: OTP/reset token expiry + longer token storage

ALTER TABLE user_register ALTER COLUMN otp TYPE VARCHAR(128);

ALTER TABLE user_register ADD COLUMN IF NOT EXISTS otp_expires_at TIMESTAMP;

CREATE INDEX IF NOT EXISTS ix_user_register_otp_expires_at ON user_register (otp_expires_at);
