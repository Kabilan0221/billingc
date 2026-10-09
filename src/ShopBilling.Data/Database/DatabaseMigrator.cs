using System;
using System.Data.SQLite;
using ShopBilling.Core.Services;

namespace ShopBilling.Data.Database
{
    public class DatabaseMigrator
    {
        public static void RunMigrations()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            {
                // Ensure migration table exists
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS schema_migrations (
                            version INTEGER PRIMARY KEY,
                            description TEXT NOT NULL,
                            applied_at DATETIME DEFAULT CURRENT_TIMESTAMP
                        );
                    ";
                    cmd.ExecuteNonQuery();
                }

                // Check and apply migrations in order
                ApplyMigration(conn, 1, "Initial relational schema (categories, subcategories, brands, products, settings, audit_logs)", GetMigrationV1Script());
                ApplyMigration(conn, 2, "High-performance search indexes for 50,000+ items and bilingual support", GetMigrationV2Script());
                ApplyMigration(conn, 3, "Default categories, units, and store settings seed", GetMigrationV3Script());
                ApplyMigration(conn, 4, "Suppliers, Purchase Inbound Goods, Stock Movements, Supplier Payments & Ledger", GetMigrationV4Script());
                ApplyMigration(conn, 5, "Stock Adjustments, Physical Stock Verification, and Ledger Indexes", GetMigrationV5Script());
            }
        }

        private static void ApplyMigration(SQLiteConnection conn, int version, string description, string sql)
        {
            bool alreadyApplied = false;
            using (var checkCmd = conn.CreateCommand())
            {
                checkCmd.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = @v";
                checkCmd.Parameters.AddWithValue("@v", version);
                alreadyApplied = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
            }

            if (alreadyApplied) return;

            using (var tx = conn.BeginTransaction())
            {
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.Transaction = tx;
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();

                        cmd.CommandText = "INSERT INTO schema_migrations (version, description, applied_at) VALUES (@v, @desc, CURRENT_TIMESTAMP)";
                        cmd.Parameters.Clear();
                        cmd.Parameters.AddWithValue("@v", version);
                        cmd.Parameters.AddWithValue("@desc", description);
                        cmd.ExecuteNonQuery();
                    }

                    tx.Commit();
                    AppLogger.Info($"Applied database migration #{version}: {description}");
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    AppLogger.Error($"Failed applying migration #{version}", ex);
                    throw;
                }
            }
        }

        private static string GetMigrationV1Script()
        {
            return @"
                -- Categories
                CREATE TABLE IF NOT EXISTS categories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    code TEXT NOT NULL UNIQUE,
                    name TEXT NOT NULL COLLATE NOCASE,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- Subcategories
                CREATE TABLE IF NOT EXISTS subcategories (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category_id INTEGER NOT NULL,
                    code TEXT NOT NULL,
                    name TEXT NOT NULL COLLATE NOCASE,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE CASCADE,
                    UNIQUE(category_id, code)
                );

                -- Brands
                CREATE TABLE IF NOT EXISTS brands (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    code TEXT NOT NULL UNIQUE,
                    name TEXT NOT NULL COLLATE NOCASE,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- Products Table
                CREATE TABLE IF NOT EXISTS products (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_code TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    barcode TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    name_en TEXT NOT NULL COLLATE NOCASE,
                    name_ta TEXT COLLATE NOCASE,
                    category_id INTEGER,
                    subcategory_id INTEGER,
                    brand_id INTEGER,
                    unit TEXT NOT NULL DEFAULT 'PCS',
                    hsn_code TEXT,
                    purchase_rate REAL NOT NULL DEFAULT 0.0,
                    sale_rate REAL NOT NULL DEFAULT 0.0,
                    wholesale_rate REAL NOT NULL DEFAULT 0.0,
                    mrp REAL NOT NULL DEFAULT 0.0,
                    tax_rate REAL NOT NULL DEFAULT 0.0,
                    opening_stock REAL NOT NULL DEFAULT 0.0,
                    current_stock REAL NOT NULL DEFAULT 0.0,
                    reorder_level REAL NOT NULL DEFAULT 5.0,
                    max_stock_level REAL NOT NULL DEFAULT 1000.0,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE SET NULL,
                    FOREIGN KEY(subcategory_id) REFERENCES subcategories(id) ON DELETE SET NULL,
                    FOREIGN KEY(brand_id) REFERENCES brands(id) ON DELETE SET NULL
                );

                -- App Settings
                CREATE TABLE IF NOT EXISTS settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    description TEXT,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- Audit Logs
                CREATE TABLE IF NOT EXISTS audit_logs (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    entity TEXT NOT NULL,
                    entity_id TEXT,
                    action TEXT NOT NULL,
                    details TEXT,
                    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
                );
            ";
        }

        private static string GetMigrationV2Script()
        {
            return @"
                -- Performance Indexes for 50,000+ products
                CREATE INDEX IF NOT EXISTS idx_products_barcode ON products(barcode);
                CREATE INDEX IF NOT EXISTS idx_products_code ON products(product_code);
                CREATE INDEX IF NOT EXISTS idx_products_name_en ON products(name_en);
                CREATE INDEX IF NOT EXISTS idx_products_name_ta ON products(name_ta);
                CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_id, is_active);
                CREATE INDEX IF NOT EXISTS idx_products_brand ON products(brand_id, is_active);
                CREATE INDEX IF NOT EXISTS idx_products_stock_alert ON products(current_stock, reorder_level, is_active);
                CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_logs(timestamp DESC);
            ";
        }

        private static string GetMigrationV3Script()
        {
            return @"
                -- Seed Standard Retail Categories
                INSERT OR IGNORE INTO categories (id, code, name) VALUES 
                    (1, 'CAT-GROC', 'Groceries & Staples'),
                    (2, 'CAT-DAIR', 'Dairy & Refrigerated'),
                    (3, 'CAT-BEV',  'Beverages & Soft Drinks'),
                    (4, 'CAT-SNK',  'Snacks & Confectionery'),
                    (5, 'CAT-PC',   'Personal Care & Hygiene'),
                    (6, 'CAT-HH',   'Household & Cleaning');

                -- Seed Standard Subcategories
                INSERT OR IGNORE INTO subcategories (id, category_id, code, name) VALUES
                    (1, 1, 'SUB-RICE', 'Rice, Dals & Pulses'),
                    (2, 1, 'SUB-OIL',  'Cooking Oils & Ghee'),
                    (3, 1, 'SUB-SPICE','Spices & Masalas'),
                    (4, 2, 'SUB-MILK', 'Milk & Curd'),
                    (5, 3, 'SUB-TEA',  'Tea & Coffee Powder'),
                    (6, 4, 'SUB-BIS',  'Biscuits & Cookies'),
                    (7, 5, 'SUB-SOAP', 'Soaps & Shampoos'),
                    (8, 6, 'SUB-DET',  'Detergents & Cleaners');

                -- Seed Popular Retail Brands
                INSERT OR IGNORE INTO brands (id, code, name) VALUES
                    (1, 'BR-TATA',    'Tata Consumer'),
                    (2, 'BR-AACHI',   'Aachi Masala'),
                    (3, 'BR-BRIT',    'Britannia'),
                    (4, 'BR-HUL',     'Hindustan Unilever'),
                    (5, 'BR-ITC',     'ITC Limited'),
                    (6, 'BR-PARLE',   'Parle'),
                    (7, 'BR-NESTLE',  'Nestle'),
                    (8, 'BR-AMUL',    'Amul');

                -- Store Settings
                INSERT OR IGNORE INTO settings (key, value, description) VALUES
                    ('StoreName', 'Sri Murugan Super Market', 'Retail Business Name'),
                    ('StoreAddress', '124 Bazaar Road, T. Nagar, Chennai - 600017', 'Store physical address'),
                    ('StorePhone', '044-24345678', 'Contact number'),
                    ('GstNumber', '33AAAAA0000A1Z5', 'GSTIN identification'),
                    ('DefaultTaxRate', '5', 'Default GST rate percentage'),
                    ('BarcodeFormat', 'EAN13', 'Default generated barcode type');
            ";
        }

        private static string GetMigrationV4Script()
        {
            return @"
                -- 1. Suppliers Table
                CREATE TABLE IF NOT EXISTS suppliers (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    code TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    name TEXT NOT NULL COLLATE NOCASE,
                    contact_person TEXT,
                    mobile TEXT,
                    alt_mobile TEXT,
                    whatsapp TEXT,
                    email TEXT,
                    gstin TEXT,
                    pan TEXT,
                    address TEXT,
                    city TEXT,
                    state TEXT,
                    state_code TEXT,
                    pin_code TEXT,
                    opening_balance REAL NOT NULL DEFAULT 0.0,
                    current_balance REAL NOT NULL DEFAULT 0.0,
                    payment_terms TEXT,
                    notes TEXT,
                    is_active INTEGER NOT NULL DEFAULT 1,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- 2. Purchases Header Table
                CREATE TABLE IF NOT EXISTS purchases (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    purchase_number TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    supplier_id INTEGER NOT NULL,
                    supplier_invoice_number TEXT,
                    supplier_invoice_date DATETIME,
                    goods_received_date DATETIME,
                    purchase_type TEXT NOT NULL DEFAULT 'Credit', -- 'Cash' or 'Credit'
                    payment_due_date DATETIME,
                    reference_number TEXT,
                    notes TEXT,
                    status TEXT NOT NULL DEFAULT 'Draft', -- 'Draft', 'Confirmed', 'Cancelled'
                    total_qty REAL NOT NULL DEFAULT 0.0,
                    total_free_qty REAL NOT NULL DEFAULT 0.0,
                    gross_amount REAL NOT NULL DEFAULT 0.0,
                    item_discount REAL NOT NULL DEFAULT 0.0,
                    additional_discount REAL NOT NULL DEFAULT 0.0,
                    taxable_amount REAL NOT NULL DEFAULT 0.0,
                    tax_amount REAL NOT NULL DEFAULT 0.0,
                    additional_charges REAL NOT NULL DEFAULT 0.0,
                    round_off REAL NOT NULL DEFAULT 0.0,
                    grand_total REAL NOT NULL DEFAULT 0.0,
                    amount_paid REAL NOT NULL DEFAULT 0.0,
                    balance_due REAL NOT NULL DEFAULT 0.0,
                    created_by TEXT DEFAULT 'Admin',
                    confirmed_at DATETIME,
                    cancelled_at DATETIME,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT
                );

                -- 3. Purchase Items Table
                CREATE TABLE IF NOT EXISTS purchase_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    purchase_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    quantity REAL NOT NULL,
                    free_quantity REAL NOT NULL DEFAULT 0.0,
                    purchase_rate REAL NOT NULL,
                    discount_percent REAL NOT NULL DEFAULT 0.0,
                    discount_amount REAL NOT NULL DEFAULT 0.0,
                    tax_rate REAL NOT NULL DEFAULT 0.0,
                    taxable_amount REAL NOT NULL DEFAULT 0.0,
                    tax_amount REAL NOT NULL DEFAULT 0.0,
                    line_total REAL NOT NULL DEFAULT 0.0,
                    batch_number TEXT,
                    expiry_date DATETIME,
                    FOREIGN KEY(purchase_id) REFERENCES purchases(id) ON DELETE CASCADE,
                    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
                );

                -- 4. Stock Movements Ledger Table
                CREATE TABLE IF NOT EXISTS stock_movements (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    product_id INTEGER NOT NULL,
                    reference_type TEXT NOT NULL, -- 'PURCHASE_CONFIRM', 'PURCHASE_CANCEL', 'OPENING_STOCK', 'ADJUSTMENT'
                    reference_id TEXT NOT NULL,
                    quantity REAL NOT NULL,
                    quantity_before REAL NOT NULL,
                    quantity_after REAL NOT NULL,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
                );

                -- 5. Supplier Payments Table
                CREATE TABLE IF NOT EXISTS supplier_payments (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    payment_number TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    supplier_id INTEGER NOT NULL,
                    purchase_id INTEGER,
                    payment_date DATETIME NOT NULL,
                    payment_method TEXT NOT NULL DEFAULT 'Cash', -- 'Cash', 'Bank Transfer', 'UPI', 'Cheque', 'Other'
                    amount REAL NOT NULL,
                    reference_number TEXT,
                    notes TEXT,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT,
                    FOREIGN KEY(purchase_id) REFERENCES purchases(id) ON DELETE SET NULL
                );

                -- 6. Supplier Ledger Table
                CREATE TABLE IF NOT EXISTS supplier_ledger (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    supplier_id INTEGER NOT NULL,
                    transaction_type TEXT NOT NULL, -- 'OPENING_BALANCE', 'PURCHASE', 'PAYMENT', 'PURCHASE_CANCEL'
                    reference_id TEXT NOT NULL,
                    debit REAL NOT NULL DEFAULT 0.0,  -- payments decrease payable
                    credit REAL NOT NULL DEFAULT 0.0, -- purchases increase payable
                    balance_after REAL NOT NULL DEFAULT 0.0,
                    notes TEXT,
                    transaction_date DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT
                );

                -- 7. High Performance Indexes for Supplier & Inbound Goods
                CREATE INDEX IF NOT EXISTS idx_suppliers_code ON suppliers(code);
                CREATE INDEX IF NOT EXISTS idx_suppliers_name ON suppliers(name);
                CREATE INDEX IF NOT EXISTS idx_suppliers_mobile ON suppliers(mobile);
                CREATE INDEX IF NOT EXISTS idx_suppliers_gstin ON suppliers(gstin);
                CREATE INDEX IF NOT EXISTS idx_purchases_number ON purchases(purchase_number);
                CREATE INDEX IF NOT EXISTS idx_purchases_supplier ON purchases(supplier_id, status);
                CREATE INDEX IF NOT EXISTS idx_purchases_invoice ON purchases(supplier_invoice_number);
                CREATE INDEX IF NOT EXISTS idx_purchases_date ON purchases(goods_received_date);
                CREATE INDEX IF NOT EXISTS idx_purchase_items_purchase ON purchase_items(purchase_id);
                CREATE INDEX IF NOT EXISTS idx_purchase_items_product ON purchase_items(product_id);
                CREATE INDEX IF NOT EXISTS idx_stock_movements_product ON stock_movements(product_id, created_at);
                CREATE INDEX IF NOT EXISTS idx_supplier_payments_supplier ON supplier_payments(supplier_id, payment_date);
                CREATE INDEX IF NOT EXISTS idx_supplier_ledger_supplier ON supplier_ledger(supplier_id, transaction_date);

                -- 8. Seed Standard Wholesale Suppliers
                INSERT OR IGNORE INTO suppliers (id, code, name, contact_person, mobile, email, gstin, city, state, payment_terms, opening_balance, current_balance) VALUES
                    (1, 'SUP001', 'Sri Meenakshi Trading Co', 'K. Ramasamy', '9840123456', 'sales@meenakshitrading.com', '33AABCS1429B1Z1', 'Madurai', 'Tamil Nadu', '30 Days Net', 0.0, 0.0),
                    (2, 'SUP002', 'Aachi Masala Foods Pvt Ltd', 'M. Saravanan', '9841234567', 'distributors@aachigroup.com', '33AAACA2355F1ZY', 'Chennai', 'Tamil Nadu', '15 Days Net', 0.0, 0.0),
                    (3, 'SUP003', 'Hindustan Consumer Supplies', 'P. Natarajan', '9842345678', 'orders@hcsdist.in', '33AABCH8872Q1ZS', 'Coimbatore', 'Tamil Nadu', 'Immediate Cash', 0.0, 0.0);
            ";
        }

        private static string GetMigrationV5Script()
        {
            return @"
                -- 1. Stock Adjustments Header Table
                CREATE TABLE IF NOT EXISTS stock_adjustments (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    adjustment_number TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    adjustment_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    reason TEXT NOT NULL,
                    notes TEXT,
                    status TEXT NOT NULL DEFAULT 'Draft', -- 'Draft', 'Confirmed', 'Cancelled'
                    total_items INTEGER NOT NULL DEFAULT 0,
                    total_difference_quantity REAL NOT NULL DEFAULT 0.0,
                    total_cost_impact REAL NOT NULL DEFAULT 0.0,
                    created_by TEXT DEFAULT 'Admin',
                    confirmed_at DATETIME,
                    cancelled_at DATETIME,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
                );

                -- 2. Stock Adjustment Items Table
                CREATE TABLE IF NOT EXISTS stock_adjustment_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    adjustment_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    current_stock REAL NOT NULL,
                    physical_count REAL NOT NULL,
                    difference_quantity REAL NOT NULL,
                    adjustment_type TEXT NOT NULL, -- 'Increase Stock', 'Decrease Stock', 'Damaged Goods', 'Expired Goods', 'Physical Count Correction', 'Other'
                    unit_cost REAL NOT NULL DEFAULT 0.0,
                    reason TEXT,
                    notes TEXT,
                    FOREIGN KEY(adjustment_id) REFERENCES stock_adjustments(id) ON DELETE CASCADE,
                    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
                );

                -- 3. Physical Stock Verification Header Table
                CREATE TABLE IF NOT EXISTS physical_verifications (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    verification_number TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    verification_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    category_id INTEGER,
                    status TEXT NOT NULL DEFAULT 'Draft', -- 'Draft', 'Confirmed', 'Cancelled'
                    total_counted_products INTEGER NOT NULL DEFAULT 0,
                    total_discrepancies INTEGER NOT NULL DEFAULT 0,
                    net_difference_quantity REAL NOT NULL DEFAULT 0.0,
                    notes TEXT,
                    created_by TEXT DEFAULT 'Admin',
                    confirmed_at DATETIME,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(category_id) REFERENCES categories(id) ON DELETE SET NULL
                );

                -- 4. Physical Stock Verification Items Table
                CREATE TABLE IF NOT EXISTS physical_verification_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    verification_id INTEGER NOT NULL,
                    product_id INTEGER NOT NULL,
                    system_stock_at_start REAL NOT NULL,
                    system_stock_at_confirm REAL NOT NULL DEFAULT 0.0,
                    physical_count REAL NOT NULL,
                    difference_quantity REAL NOT NULL,
                    discrepancy_resolved INTEGER NOT NULL DEFAULT 0,
                    notes TEXT,
                    FOREIGN KEY(verification_id) REFERENCES physical_verifications(id) ON DELETE CASCADE,
                    FOREIGN KEY(product_id) REFERENCES products(id) ON DELETE RESTRICT
                );

                -- 5. Additional Performance Indexes for Stock Audit Ledger
                CREATE INDEX IF NOT EXISTS idx_stock_adjustments_num ON stock_adjustments(adjustment_number);
                CREATE INDEX IF NOT EXISTS idx_stock_adjustments_date ON stock_adjustments(adjustment_date DESC);
                CREATE INDEX IF NOT EXISTS idx_stock_adjustment_items_adj ON stock_adjustment_items(adjustment_id);
                CREATE INDEX IF NOT EXISTS idx_stock_adjustment_items_prod ON stock_adjustment_items(product_id);
                CREATE INDEX IF NOT EXISTS idx_physical_verifications_num ON physical_verifications(verification_number);
                CREATE INDEX IF NOT EXISTS idx_physical_verification_items_ver ON physical_verification_items(verification_id);
                CREATE INDEX IF NOT EXISTS idx_stock_movements_date ON stock_movements(created_at DESC);
                CREATE INDEX IF NOT EXISTS idx_stock_movements_ref ON stock_movements(reference_type, reference_id);
            ";
        }
    }
}
