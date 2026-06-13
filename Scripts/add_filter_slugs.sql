-- Run once on existing database to add slug columns + backfill unique slugs

ALTER TABLE categories ADD COLUMN IF NOT EXISTS slug VARCHAR(255);
ALTER TABLE brands ADD COLUMN IF NOT EXISTS slug VARCHAR(255);
ALTER TABLE colors ADD COLUMN IF NOT EXISTS slug VARCHAR(255);
ALTER TABLE sizes ADD COLUMN IF NOT EXISTS slug VARCHAR(255);

-- Backfill categories
WITH numbered AS (
    SELECT
        id,
        LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(category_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g')) AS base_slug,
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(category_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g'))
            ORDER BY id
        ) AS rn
    FROM categories
    WHERE slug IS NULL OR slug = ''
)
UPDATE categories c
SET slug = CASE
    WHEN n.base_slug IS NULL OR n.base_slug = '' THEN 'category-' || c.id
    WHEN n.rn = 1 THEN n.base_slug
    ELSE n.base_slug || '-' || (n.rn - 1)
END
FROM numbered n
WHERE c.id = n.id;

-- Backfill brands
WITH numbered AS (
    SELECT
        id,
        LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(brand_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g')) AS base_slug,
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(brand_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g'))
            ORDER BY id
        ) AS rn
    FROM brands
    WHERE slug IS NULL OR slug = ''
)
UPDATE brands b
SET slug = CASE
    WHEN n.base_slug IS NULL OR n.base_slug = '' THEN 'brand-' || b.id
    WHEN n.rn = 1 THEN n.base_slug
    ELSE n.base_slug || '-' || (n.rn - 1)
END
FROM numbered n
WHERE b.id = n.id;

-- Backfill colors
WITH numbered AS (
    SELECT
        id,
        LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(color_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g')) AS base_slug,
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(color_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g'))
            ORDER BY id
        ) AS rn
    FROM colors
    WHERE slug IS NULL OR slug = ''
)
UPDATE colors c
SET slug = CASE
    WHEN n.base_slug IS NULL OR n.base_slug = '' THEN 'color-' || c.id
    WHEN n.rn = 1 THEN n.base_slug
    ELSE n.base_slug || '-' || (n.rn - 1)
END
FROM numbered n
WHERE c.id = n.id;

-- Backfill sizes
WITH numbered AS (
    SELECT
        id,
        LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(size_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g')) AS base_slug,
        ROW_NUMBER() OVER (
            PARTITION BY LOWER(REGEXP_REPLACE(REGEXP_REPLACE(TRIM(size_name), '[^a-zA-Z0-9\s-]', '', 'g'), '\s+', '-', 'g'))
            ORDER BY id
        ) AS rn
    FROM sizes
    WHERE slug IS NULL OR slug = ''
)
UPDATE sizes s
SET slug = CASE
    WHEN n.base_slug IS NULL OR n.base_slug = '' THEN 'size-' || s.id
    WHEN n.rn = 1 THEN n.base_slug
    ELSE n.base_slug || '-' || (n.rn - 1)
END
FROM numbered n
WHERE s.id = n.id;

ALTER TABLE categories ALTER COLUMN slug SET NOT NULL;
ALTER TABLE brands ALTER COLUMN slug SET NOT NULL;
ALTER TABLE colors ALTER COLUMN slug SET NOT NULL;
ALTER TABLE sizes ALTER COLUMN slug SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_slug ON categories (slug);
CREATE UNIQUE INDEX IF NOT EXISTS ux_brands_slug ON brands (slug);
CREATE UNIQUE INDEX IF NOT EXISTS ux_colors_slug ON colors (slug);
CREATE UNIQUE INDEX IF NOT EXISTS ux_sizes_slug ON sizes (slug);
