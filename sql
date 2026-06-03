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
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP

);



CREATE TABLE brands
(
    id SERIAL PRIMARY KEY,
    brand_name VARCHAR(200) NOT NULL,
    brand_img TEXT NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);


CREATE TABLE categories
(
    id SERIAL PRIMARY KEY,

    category_name VARCHAR(255) NOT NULL,

    parent_id INT NULL,

    category_image TEXT,

    is_active BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_parent_category
        FOREIGN KEY (parent_id)
        REFERENCES categories(id)
        ON DELETE CASCADE
);

CREATE TABLE blogs   (
    id SERIAL PRIMARY KEY,

    blog_name VARCHAR(255) NOT NULL,

    description TEXT NOT NULL,

    blog_image TEXT,

    status BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);


CREATE TABLE contacts
(
    id SERIAL PRIMARY KEY,

    first_name VARCHAR(100) NOT NULL,

    last_name VARCHAR(100) NOT NULL,

    email VARCHAR(255) NOT NULL,

    phone_number VARCHAR(20),

    message TEXT NOT NULL,


    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);



CREATE TABLE products (
    id SERIAL PRIMARY KEY,

    productname VARCHAR(255) NOT NULL,
    shortdescription VARCHAR(500),
    description TEXT,

    sku VARCHAR(100) UNIQUE,

    brandid INT,
    categoryid INT,
  

    baseprice NUMERIC(10,2) NOT NULL,
    mrp NUMERIC(10,2) NOT NULL,
    discountprice NUMERIC(10,2),
    saleprice NUMERIC(10,2) NOT NULL,
    gst NUMERIC(5,2) DEFAULT 0,

    stock INT DEFAULT 0,

    productimageurl TEXT,
    galleryimages TEXT[],

    sizes TEXT[],
    color VARCHAR(100),

    isactive BOOLEAN DEFAULT TRUE,

    createdat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updatedat TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_product_brand
        FOREIGN KEY (brandid)
        REFERENCES brands(id),

    CONSTRAINT fk_product_category
        FOREIGN KEY (categoryid)
        REFERENCES categories(id)

   
  
);
CREATE TABLE banners (
     id SERIAL PRIMARY KEY,

    banner_name VARCHAR(255) NOT NULL,
    banner_description TEXT,

    banner_image TEXT NOT NULL,

    banner_type VARCHAR(100) NOT NULL,

    banner_link TEXT,

    active BOOLEAN DEFAULT TRUE,

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);