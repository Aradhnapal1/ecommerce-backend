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




CREATE TABLE sizes (
    id SERIAL PRIMARY KEY,
    size_name VARCHAR(20) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE
)

CREATE TABLE colors (
    id SERIAL PRIMARY KEY,
    color_name VARCHAR(100) NOT NULL,
    color_code VARCHAR(20),
    status BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);