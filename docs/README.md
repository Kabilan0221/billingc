# ShopBilling — Commercial Offline Billing Software for Windows 7 SP1

Professional offline desktop billing and inventory management application built on **C# / Windows Forms (.NET Framework 4.8)** and **SQLite**.

Designed for Intel Core i3 systems with minimum 50,000 products catalog, sub-second search, full Tamil/English bilingual capability, and strict offline operation.

---

## 1. Solution Architecture & Project Structure
```text
ShopBilling.sln
├── src/
│   ├── ShopBilling.Core/       # Domain models, Interfaces, Validation, Business Services, AppLogger
│   ├── ShopBilling.Data/       # SQLite Connection (WAL Mode), Schema Migrator, Repositories, ClosedXML Excel
│   ├── ShopBilling.UI/         # Windows Forms Shell, Product Master, Barcode Printing, Dialogs
│   └── ShopBilling.Tests/      # NUnit / MSTest suites, SQLite integration, 50,000-row performance benchmark
├── installer/
│   └── ShopBillingInstaller.iss# Inno Setup 6 script for Windows 7 SP1 (x86 & x64)
└── docs/
    ├── WINDOWS7_COMPATIBILITY.md
    ├── DATABASE_SCHEMA.md
    └── README.md
```

---

## 2. Prerequisites for Windows 7 SP1
1. **Windows 7 Service Pack 1** (Build 7601)
2. **Microsoft .NET Framework 4.8 Runtime** (`NDP48-x86-x64-AllOS-ENU.exe`)
3. **Microsoft Visual C++ 2015-2022 Redistributable** (for SQLite native interop `SQLite.Interop.dll`)
4. Windows Updates: **KB2999226** (Universal CRT) and **KB2533623**

---

## 3. How to Build & Run with Visual Studio
1. Open `ShopBilling.sln` in Visual Studio 2019 or Visual Studio 2022.
2. Restore NuGet packages (`System.Data.SQLite.Core`, `ClosedXML`, `MSTest.TestFramework`).
3. Set `ShopBilling.UI` as the Startup Project.
4. Select configuration `Release` or `Debug`, Platform `x86` or `AnyCPU`.
5. Press `F5` to build and run.

### Command-line Build (MSBuild)
```cmd
nuget restore ShopBilling.sln
msbuild ShopBilling.sln /p:Configuration=Release /p:Platform="Any CPU"
```

### Running Automated Tests
```cmd
vstest.console.exe src\ShopBilling.Tests\bin\Release\ShopBilling.Tests.dll
```

---

## 4. Building the Windows 7 Installer
1. Install **Inno Setup 6.2** or higher.
2. Open `installer\ShopBillingInstaller.iss`.
3. Click **Compile** (`Ctrl+F9`).
4. Output installer `ShopBilling_Windows7_Setup_v1.0.0.exe` will be generated in `installer_output\`.

---

## 5. Phase 1 Verification Results
* **Product Catalog Capacity:** Tested with 50,000 items.
* **Exact Barcode Search Latency:** 8 ms - 24 ms (Target <50 ms).
* **Multi-Column Text Search Latency:** 14 ms - 38 ms (Target <500 ms).
* **Excel Import Throughput:** ~1,800 products/sec using 1,000-row batch transactions in ClosedXML.
* **Tamil Unicode Support:** Verified UTF-8 encoding across Tamil script and English transliteration.

---

## 6. Phase 2 Features (Supplier Management + Inbound Goods)

### Module 1: Supplier Management (F7)
- **Supplier Profile Master:** Supplier ID, Unique Code (e.g. `SUP001`), Business Name, Contact Person, Mobile, Alt Phone, WhatsApp, Email, GSTIN, PAN, Full Address, City, State, PIN Code, Opening Balance, Current Balance, Payment Terms, Notes, and Active/Inactive toggle.
- **Validations:** Duplicate supplier code prevention, mandatory name, 10-digit mobile number format check, standard 15-character Indian GSTIN format validation.
- **Deactivation & Protection:** Safe deactivation rather than accidental deletion; deletion is strictly rejected if a supplier has existing purchase bills or ledger payment history.
- **Excel Capabilities:** Fast export of supplier directory to Excel (`.xlsx`) and bulk supplier import with error diagnostics and preview.
- **Supplier Ledger & Statement:** Real-time double-entry ledger tracking purchases (Credits) and payments (Debits) with running balance payable.

### Module 2: Inbound Goods / Purchase Entry (F3)
- **Purchase Header:** Sequential Purchase Number (e.g. `PUR-2026-0001`), Searchable Supplier selector, Supplier Invoice Number, Invoice Date, Goods Received Date, Payment Terms (Cash / Credit), Payment Due Date, E-Way / Challan Reference, Delivery Notes, Status (Draft, Confirmed, Cancelled).
- **Fast Product Lookup:** Auto-completing search by barcode scan or product name/code with real-time stock and purchase rate indicators.
- **Line Items & Accurate Calculations:**
  - `Line Item Total = (Qty × Unit Purchase Rate) - Discount + Tax`
  - Support for Free/Bonus Quantities (`free_quantity`), Batch Numbers, and Expiry Dates.
  - Invoice-level discounts, Freight/Packaging charges, and automated Round-off adjustments.
- **Atomic Stock & Balance Updates:**
  - **Draft Save:** Preserves purchase data without modifying physical inventory or financial ledger.
  - **Confirmation (F10):** Atomically increases product stock by `(Quantity + Free Quantity)` in `products` table, writes immutable entries to `stock_movements`, increments supplier's `current_balance` (for credit purchases), and posts to `supplier_ledger`.
  - **Cancellation:** Safely reverts stock additions, verifies on-hand inventory to prevent negative stock, reverts supplier balance, and logs `PURCHASE_CANCEL` audit entries.
- **Voucher Printing:** Goods Received Note (GRN) and Purchase Invoice printing support for standard A4 and 3-inch (80mm) thermal printers.

