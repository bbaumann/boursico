BEGIN;

CREATE TABLE event (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    code TEXT NOT NULL,
    name TEXT NOT NULL,
    CONSTRAINT uq_event_code UNIQUE (code)
);

CREATE TABLE seller (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id BIGINT NOT NULL,
    seller_code TEXT NOT NULL,
    first_name TEXT NOT NULL,
    last_name TEXT NOT NULL,
    email TEXT,
    phone TEXT,
    notes TEXT,
    CONSTRAINT fk_seller_event
        FOREIGN KEY (event_id)
        REFERENCES event(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT uq_seller_event_code UNIQUE (event_id, seller_code)
);

CREATE TABLE item (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id BIGINT NOT NULL,
    seller_id BIGINT NOT NULL,
    reference TEXT NOT NULL,
    item_number INTEGER NOT NULL,
    title TEXT NOT NULL,
    seller_price NUMERIC(10,2) NOT NULL,
    sale_price NUMERIC(10,2) NOT NULL,
    status TEXT NOT NULL,
    CONSTRAINT fk_item_event
        FOREIGN KEY (event_id)
        REFERENCES event(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT fk_item_seller
        FOREIGN KEY (seller_id)
        REFERENCES seller(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT ck_item_status
        CHECK (status IN ('registered', 'in_stock', 'not_provided', 'sold', 'missing')),
    CONSTRAINT ck_item_seller_price
        CHECK (seller_price >= 0),
    CONSTRAINT ck_item_sale_price
        CHECK (sale_price >= 0),
    CONSTRAINT uq_item_event_reference UNIQUE (event_id, reference),
    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
);

CREATE TABLE sale (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    event_id BIGINT NOT NULL,
    sold_at TIMESTAMPTZ NOT NULL,
    total_amount NUMERIC(10,2) NOT NULL,
    receipt_details TEXT,
    CONSTRAINT fk_sale_event
        FOREIGN KEY (event_id)
        REFERENCES event(id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,
    CONSTRAINT ck_sale_total_amount
        CHECK (total_amount >= 0)
);

CREATE TABLE sale_line (
    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sale_id BIGINT NOT NULL,
    item_id BIGINT NOT NULL,
    seller_price_at_sale NUMERIC(10,2) NOT NULL,
    sale_price_at_sale NUMERIC(10,2) NOT NULL,
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
    CONSTRAINT ck_sale_line_seller_price
        CHECK (seller_price_at_sale >= 0),
    CONSTRAINT ck_sale_line_sale_price
        CHECK (sale_price_at_sale >= 0),
    CONSTRAINT uq_sale_line_item UNIQUE (item_id)
);

CREATE INDEX idx_item_title ON item(title);
CREATE INDEX idx_item_seller_id ON item(seller_id);
CREATE INDEX idx_item_status ON item(status);
CREATE INDEX idx_sale_event_sold_at ON sale(event_id, sold_at);
CREATE INDEX idx_sale_line_sale_id ON sale_line(sale_id);

COMMIT;
