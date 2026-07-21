-- =============================================================================
-- AROMA BISTRO - FULL DATABASE INITIALIZATION SCRIPT (POSTGRESQL)
-- Includes Database Schema & Seed Data for Identity, Restaurant, and Order DBs
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. IDENTITY DATABASE (prm_identity_db)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Accounts" (
    "AccountId" SERIAL PRIMARY KEY,
    "Username" VARCHAR(100) NOT NULL UNIQUE,
    "Email" VARCHAR(150) NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "FullName" VARCHAR(150) NOT NULL,
    "PhoneNumber" VARCHAR(20),
    "Role" INT NOT NULL DEFAULT 0, -- 0=Customer, 1=Admin, 2=Staff
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "LastLoginAt" TIMESTAMP WITHOUT TIME ZONE
);

-- Seed Default Admin & Staff Accounts (BCrypt Hashed Passwords)
INSERT INTO "Accounts" ("Username", "Email", "PasswordHash", "FullName", "Role", "IsActive", "CreatedAt")
VALUES 
('admin', 'admin@aroma.com', '$2a$11$q9hK/J1N9X4x4Q4Q4Q4Q4uK/J1N9X4x4Q4Q4Q4Q4u', 'System Administrator', 1, TRUE, NOW()),
('staff', 'staff@aroma.com', '$2a$11$q9hK/J1N9X4x4Q4Q4Q4Q4uK/J1N9X4x4Q4Q4Q4Q4u', 'Restaurant Staff', 2, TRUE, NOW())
ON CONFLICT ("Username") DO NOTHING;

-- -----------------------------------------------------------------------------
-- 2. RESTAURANT DATABASE (prm_restaurant_db)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Categories" (
    "CategoryId" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL,
    "Description" TEXT
);

CREATE TABLE IF NOT EXISTS "MenuItems" (
    "MenuItemId" SERIAL PRIMARY KEY,
    "Name" VARCHAR(150) NOT NULL,
    "Description" TEXT,
    "Price" NUMERIC(18, 2) NOT NULL,
    "Category" INT NOT NULL, -- Enum: 1=Coffee, 2=Tea, 3=Cake, 4=Juice, 99=Other
    "ImageUrl" VARCHAR(500),
    "IsAvailable" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "UpdatedAt" TIMESTAMP WITHOUT TIME ZONE
);

CREATE TABLE IF NOT EXISTS "Tables" (
    "TableId" SERIAL PRIMARY KEY,
    "Capacity" INT NOT NULL DEFAULT 4,
    "Status" INT NOT NULL DEFAULT 1, -- 1=Available, 2=Occupied, 3=Reserved
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

-- Seed Sample Menu & Tables
INSERT INTO "MenuItems" ("Name", "Description", "Price", "Category", "ImageUrl", "IsAvailable")
VALUES 
('Cà Phê Muối', 'Sự kết hợp hoàn hảo giữa vị đắng cà phê và lớp kem muối béo ngậy.', 35000, 1, 'https://res.cloudinary.com/dkppq6bsj/image/upload/v1/menu/salt_coffee.jpg', TRUE),
('Cà Phê Sữa Đá', 'Cà phê phin đậm đà hòa quyện với sữa đặc ngọt ngào và đá lạnh.', 29000, 1, 'https://res.cloudinary.com/dkppq6bsj/image/upload/v1/menu/milk_coffee.jpg', TRUE),
('Matcha Latte Uji', 'Bột Matcha Uji Nhật Bản nguyên chất hòa cùng sữa tươi thanh trùng.', 42000, 2, 'https://res.cloudinary.com/dkppq6bsj/image/upload/v1/menu/matcha.jpg', TRUE),
('Trà Đào Cam Sả', 'Trà đào thơm nức hương sả tươi và vị cam chua nhẹ thanh mát.', 39000, 2, 'https://res.cloudinary.com/dkppq6bsj/image/upload/v1/menu/peach_tea.jpg', TRUE),
('Bánh Tiramisu Ý', 'Bánh Tiramisu Ý mềm mịn ngập tràn hương vị cà phê và cacao nguyên chất.', 45000, 3, 'https://res.cloudinary.com/dkppq6bsj/image/upload/v1/menu/tiramisu.jpg', TRUE);

INSERT INTO "Tables" ("Capacity", "Status")
VALUES (4, 1), (2, 1), (6, 1), (4, 1), (8, 1);

-- -----------------------------------------------------------------------------
-- 3. ORDER DATABASE (prm_order_db)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Orders" (
    "OrderId" SERIAL PRIMARY KEY,
    "TableId" INT NOT NULL, -- Logical Reference to Restaurant DB
    "HandledBy" INT,        -- Logical Reference to Identity DB
    "Status" INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Confirmed, 3=Serving, 4=Completed, 5=Cancelled
    "TotalAmount" NUMERIC(18, 2) NOT NULL DEFAULT 0.00,
    "Note" TEXT,
    "PublicToken" VARCHAR(64) NOT NULL UNIQUE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc'),
    "UpdatedAt" TIMESTAMP WITHOUT TIME ZONE
);

CREATE TABLE IF NOT EXISTS "OrderItems" (
    "OrderItemId" SERIAL PRIMARY KEY,
    "OrderId" INT NOT NULL REFERENCES "Orders"("OrderId") ON DELETE CASCADE,
    "MenuItemId" INT NOT NULL, -- Logical Reference to Restaurant DB
    "Quantity" INT NOT NULL DEFAULT 1,
    "UnitPrice" NUMERIC(18, 2) NOT NULL,
    "Note" TEXT,
    "Status" INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Preparing, 3=Ready, 4=Served
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS "Payments" (
    "PaymentId" SERIAL PRIMARY KEY,
    "OrderId" INT NOT NULL UNIQUE REFERENCES "Orders"("OrderId") ON DELETE CASCADE,
    "Amount" NUMERIC(18, 2) NOT NULL,
    "Method" INT NOT NULL, -- 1=Cash, 2=Transfer, 3=MoMo, 4=VNPay
    "Status" INT NOT NULL DEFAULT 1, -- 1=Pending, 2=Paid, 3=Refunded
    "PaidAt" TIMESTAMP WITHOUT TIME ZONE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

CREATE TABLE IF NOT EXISTS "Feedbacks" (
    "FeedbackId" SERIAL PRIMARY KEY,
    "OrderId" INT NOT NULL REFERENCES "Orders"("OrderId") ON DELETE CASCADE,
    "TableId" INT NOT NULL,
    "Rating" INT NOT NULL CHECK ("Rating" >= 1 AND "Rating" <= 5),
    "Comment" TEXT,
    "IsHidden" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (NOW() AT TIME ZONE 'utc')
);

-- Index optimization for real-time querying
CREATE INDEX IF NOT EXISTS "IX_Orders_TableId_Status" ON "Orders" ("TableId", "Status");
CREATE INDEX IF NOT EXISTS "IX_OrderItems_OrderId" ON "OrderItems" ("OrderId");
