-- Make product short description TEXT and keep sizes nullable.
ALTER TABLE products ALTER COLUMN shortdescription TYPE TEXT;
ALTER TABLE products ALTER COLUMN sizes DROP NOT NULL;
ALTER TABLE product_variants ALTER COLUMN sizes DROP NOT NULL;
ALTER TABLE addcart ADD COLUMN IF NOT EXISTS sizeid INT NULL;
