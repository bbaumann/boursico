PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

BEGIN;

CREATE TABLE seller (
    id INTEGER PRIMARY KEY,
    seller_code TEXT NOT NULL,
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    email TEXT,
    phone TEXT NOT NULL,
    postal_address TEXT NOT NULL,
    notes TEXT,
    CONSTRAINT uq_seller_code UNIQUE (seller_code)
);

CREATE TABLE item (
    id INTEGER PRIMARY KEY,
    seller_id INTEGER NOT NULL,
    reference TEXT NOT NULL,
    item_number INTEGER NOT NULL,
    title TEXT NOT NULL,
    seller_price_cents INTEGER NOT NULL,
    status TEXT NOT NULL,
    CONSTRAINT fk_item_seller
        FOREIGN KEY (seller_id)
        REFERENCES seller(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT ck_item_status
        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing')),
    CONSTRAINT ck_item_seller_price_cents
        CHECK (seller_price_cents >= 0),
    CONSTRAINT uq_item_reference UNIQUE (reference),
    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
);

CREATE TABLE sale (
    id INTEGER PRIMARY KEY,
    sold_at TEXT NOT NULL,
    subtotal_seller_price_cents INTEGER NOT NULL,
    checkout_fee_cents INTEGER NOT NULL,
    total_paid_cents INTEGER NOT NULL,
    receipt_details TEXT,
    CONSTRAINT ck_sale_subtotal_seller_price_cents
        CHECK (subtotal_seller_price_cents >= 0),
    CONSTRAINT ck_sale_checkout_fee_cents
        CHECK (checkout_fee_cents >= 0),
    CONSTRAINT ck_sale_total_paid_cents
        CHECK (total_paid_cents >= 0)
);

CREATE TABLE sale_line (
    id INTEGER PRIMARY KEY,
    sale_id INTEGER NOT NULL,
    item_id INTEGER NOT NULL,
    seller_price_at_sale_cents INTEGER NOT NULL,
    corrected_at TEXT,
    CONSTRAINT fk_sale_line_sale
        FOREIGN KEY (sale_id)
        REFERENCES sale(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT fk_sale_line_item
        FOREIGN KEY (item_id)
        REFERENCES item(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT ck_sale_line_seller_price_cents
        CHECK (seller_price_at_sale_cents >= 0)
);

CREATE TABLE event_state (
    id INTEGER PRIMARY KEY,
    sale_phase TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    CONSTRAINT ck_event_state_singleton
        CHECK (id = 1),
    CONSTRAINT ck_event_state_sale_phase
        CHECK (sale_phase IN ('presale', 'sale', 'post_sale'))
);

CREATE INDEX idx_item_title ON item(title);
CREATE INDEX idx_item_seller_id ON item(seller_id);
CREATE INDEX idx_item_status ON item(status);
CREATE INDEX idx_sale_sold_at ON sale(sold_at);
CREATE INDEX idx_sale_line_sale_id ON sale_line(sale_id);
CREATE UNIQUE INDEX idx_sale_line_active_item_id ON sale_line(item_id) WHERE corrected_at IS NULL;

INSERT INTO event_state (id, sale_phase, updated_at)
VALUES (1, 'presale', '2026-05-04T00:00:00Z');

COMMIT;
