CREATE TABLE IF NOT EXISTS orders (
    order_id TEXT PRIMARY KEY,
    order_name TEXT NOT NULL,
    product_name TEXT NOT NULL,
    quantity INTEGER NOT NULL CHECK (quantity > 0)
);
