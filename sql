CREATE TABLE user_register
(
    id           SERIAL PRIMARY KEY,
    first_name   VARCHAR(100) NOT NULL,
    last_name    VARCHAR(100) NOT NULL,
    email        VARCHAR(150) UNIQUE NOT NULL,
    phone_number VARCHAR(15)  NOT NULL,
    password     TEXT         NOT NULL,
    role         VARCHAR(50)  NOT NULL,
    otp          VARCHAR(10),
    is_verified  BOOLEAN      DEFAULT FALSE,
    created_at   TIMESTAMP    DEFAULT CURRENT_TIMESTAMP
);