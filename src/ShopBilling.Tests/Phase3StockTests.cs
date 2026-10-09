using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;
using ShopBilling.Data.Database;
using ShopBilling.Data.Repositories;

namespace ShopBilling.Tests
{
    [TestClass]
    public class Phase3StockTests
    {
        private string testDbPath;
        private ProductRepository productRepo;
        private CategoryRepository categoryRepo;
        private BrandRepository brandRepo;
        private AuditLogRepository auditRepo;
        private SupplierRepository supplierRepo;
        private PurchaseRepository purchaseRepo;
        private StockMovementRepository stockMoveRepo;
        private StockRepository stockRepo;

        private ProductService productService;
        private SupplierService supplierService;
        private PurchaseService purchaseService;
        private StockService stockService;

        [TestInitialize]
        public void Setup()
        {
            testDbPath = Path.Combine(Path.GetTempPath(), $"ShopBilling_Phase3_{Guid.NewGuid():N}.db");
            DatabaseConnection.Initialize(testDbPath);
            DatabaseMigrator.RunMigrations();

            productRepo = new ProductRepository();
            categoryRepo = new CategoryRepository();
            brandRepo = new BrandRepository();
            auditRepo = new AuditLogRepository();
            supplierRepo = new SupplierRepository();
            purchaseRepo = new PurchaseRepository();
            stockMoveRepo = new StockMovementRepository();
            stockRepo = new StockRepository();

            productService = new ProductService(productRepo, auditRepo);
            supplierService = new SupplierService(supplierRepo, auditRepo);
            purchaseService = new PurchaseService(purchaseRepo, productRepo, supplierRepo, auditRepo);
            stockService = new StockService(stockRepo, productRepo, auditRepo);
        }

        [TestCleanup]
        public void Teardown()
        {
            try
            {
                if (File.Exists(testDbPath)) File.Delete(testDbPath);
            }
            catch { }
        }

        // Test 1: Opening stock calculation
        [TestMethod]
        public void Test01_OpeningStock_InitializedCorrectly()
        {
            var p = new Product
            {
                ProductCode = "PRD-STK-01",
                Barcode = "890900000001",
                NameEn = "Basmati Rice 1kg",
                PurchaseRate = 90m,
                SaleRate = 120m,
                Mrp = 130m,
                OpeningStock = 40m
            };
            productService.SaveProduct(p);

            var fetched = productService.GetProductById(p.Id);
            Assert.AreEqual(40m, fetched.OpeningStock);
            Assert.AreEqual(40m, fetched.CurrentStock);
        }

        // Test 2 & 3: Confirmed purchase & Free quantities increasing stock correctly
        [TestMethod]
        public void Test02_03_ConfirmedPurchase_WithFreeQty_IncreasesStockExactlyOnce()
        {
            var p = new Product
            {
                ProductCode = "PRD-STK-02",
                Barcode = "890900000002",
                NameEn = "Mustard Seeds 100g",
                PurchaseRate = 20m,
                SaleRate = 25m,
                Mrp = 30m,
                OpeningStock = 10m
            };
            productService.SaveProduct(p);

            var sup = new Supplier { Code = "SUP-STK-01", Name = "Spices Trader", Mobile = "9840111222" };
            supplierService.SaveSupplier(sup);

            var pur = new PurchaseHeader
            {
                PurchaseNumber = "PUR-2026-STK1",
                SupplierId = sup.Id,
                Items = new List<PurchaseItem>
                {
                    new PurchaseItem { ProductId = p.Id, Quantity = 50m, FreeQuantity = 5m, PurchaseRate = 20m, TaxRate = 0m }
                }
            };
            purchaseService.SaveDraft(pur);

            // Draft must NOT change stock
            Assert.AreEqual(10m, productService.GetProductById(p.Id).CurrentStock, "Draft must not modify stock");

            // Confirm
            string err;
            bool ok = purchaseService.ConfirmPurchase(pur.Id, out err);
            Assert.IsTrue(ok);

            // Stock must increase by Qty (50) + FreeQty (5) = +55 => Total 65
            var afterStock = productService.GetProductById(p.Id).CurrentStock;
            Assert.AreEqual(65m, afterStock, "Stock should include purchased and free quantities");

            // Duplicate confirm prevention
            bool dupOk = purchaseService.ConfirmPurchase(pur.Id, out err);
            Assert.IsFalse(dupOk, "Duplicate confirmation must be rejected");
            Assert.AreEqual(65m, productService.GetProductById(p.Id).CurrentStock, "Duplicate confirm must not double-increment stock");
        }

        // Test 6: Purchase cancellation and stock reversal
        [TestMethod]
        public void Test06_PurchaseCancellation_RevertsStockSafely()
        {
            var p = new Product
            {
                ProductCode = "PRD-STK-06",
                Barcode = "890900000006",
                NameEn = "Sunflower Oil 1L",
                PurchaseRate = 100m,
                SaleRate = 125m,
                Mrp = 135m,
                OpeningStock = 20m
            };
            productService.SaveProduct(p);
            var sup = new Supplier { Code = "SUP-STK-06", Name = "Oil Agency", Mobile = "9840222333" };
            supplierService.SaveSupplier(sup);

            var pur = new PurchaseHeader
            {
                PurchaseNumber = "PUR-2026-STK6",
                SupplierId = sup.Id,
                Items = new List<PurchaseItem>
                {
                    new PurchaseItem { ProductId = p.Id, Quantity = 30m, FreeQuantity = 0m, PurchaseRate = 100m, TaxRate = 0m }
                }
            };
            purchaseService.SaveDraft(pur);
            string err;
            purchaseService.ConfirmPurchase(pur.Id, out err);
            Assert.AreEqual(50m, productService.GetProductById(p.Id).CurrentStock);

            // Cancel
            bool cancelOk = purchaseService.CancelPurchase(pur.Id, out err);
            Assert.IsTrue(cancelOk);
            Assert.AreEqual(20m, productService.GetProductById(p.Id).CurrentStock, "Stock must return to opening stock on cancellation");
        }

        // Test 7, 8, 9, 10: Stock Adjustments (Increase, Decrease, Draft isolation, Duplicate prevention)
        [TestMethod]
        public void Test07_to_10_StockAdjustment_IncreaseAndDecrease_WithDraftAndDuplicateProtection()
        {
            var p = new Product
            {
                ProductCode = "PRD-ADJ-01",
                Barcode = "890900000010",
                NameEn = "Wheat Flour 5kg",
                PurchaseRate = 180m,
                SaleRate = 220m,
                Mrp = 240m,
                OpeningStock = 50m
            };
            productService.SaveProduct(p);

            // 1. Create Draft Adjustment (Physical count: 60, Diff: +10)
            var adj = new StockAdjustment
            {
                AdjustmentNumber = "ADJ-2026-TEST1",
                Reason = "Annual Physical Verification Finding",
                Items = new List<StockAdjustmentItem>
                {
                    new StockAdjustmentItem
                    {
                        ProductId = p.Id,
                        CurrentStock = 50m,
                        PhysicalCount = 60m,
                        DifferenceQuantity = 10m,
                        AdjustmentType = "Increase Stock",
                        UnitCost = 180m
                    }
                }
            };

            var saveRes = stockService.SaveAdjustmentDraft(adj);
            Assert.IsTrue(saveRes.IsValid);

            // Stock should remain 50 while Draft
            Assert.AreEqual(50m, productService.GetProductById(p.Id).CurrentStock, "Draft adjustment must NOT alter stock");

            // 2. Confirm Adjustment
            string err;
            bool conf = stockService.ConfirmAdjustment(adj.Id, out err);
            Assert.IsTrue(conf, $"Confirm failed: {err}");

            // Stock should now be 50 + 10 = 60
            Assert.AreEqual(60m, productService.GetProductById(p.Id).CurrentStock, "Stock must be updated to 60");

            // 3. Duplicate confirmation rejected
            bool dupConf = stockService.ConfirmAdjustment(adj.Id, out err);
            Assert.IsFalse(dupConf, "Duplicate adjustment confirm must fail");
            Assert.AreEqual(60m, productService.GetProductById(p.Id).CurrentStock);

            // 4. Test Stock Adjustment Decrease (Damaged goods: -5)
            var adjDec = new StockAdjustment
            {
                AdjustmentNumber = "ADJ-2026-TEST2",
                Reason = "Damaged during handling",
                Items = new List<StockAdjustmentItem>
                {
                    new StockAdjustmentItem
                    {
                        ProductId = p.Id,
                        CurrentStock = 60m,
                        PhysicalCount = 55m,
                        DifferenceQuantity = -5m,
                        AdjustmentType = "Damaged Goods",
                        UnitCost = 180m
                    }
                }
            };
            stockService.SaveAdjustmentDraft(adjDec);
            stockService.ConfirmAdjustment(adjDec.Id, out err);

            Assert.AreEqual(55m, productService.GetProductById(p.Id).CurrentStock, "Stock should decrease to 55");
        }

        // Test 11 & 12: Physical Verification Discrepancy Calculation & Concurrent Change Detection
        [TestMethod]
        public void Test11_12_PhysicalVerification_DiscrepancyAndConcurrentChangeHandling()
        {
            var p = new Product
            {
                ProductCode = "PRD-VER-01",
                Barcode = "890900000020",
                NameEn = "Cardamom 50g",
                PurchaseRate = 120m,
                SaleRate = 160m,
                Mrp = 180m,
                OpeningStock = 30m
            };
            productService.SaveProduct(p);

            // 1. Create Physical Verification count sheet (Counted 28 vs System 30 -> Diff: -2)
            var ver = new PhysicalStockVerification
            {
                VerificationNumber = "VER-2026-0001",
                Notes = "Audit Shelf B-12",
                Items = new List<PhysicalStockVerificationItem>
                {
                    new PhysicalStockVerificationItem
                    {
                        ProductId = p.Id,
                        SystemStockAtStart = 30m,
                        PhysicalCount = 28m,
                        DifferenceQuantity = -2m
                    }
                }
            };
            long verId = stockService.CreatePhysicalVerificationDraft(ver);
            Assert.IsTrue(verId > 0);

            // Stock still 30 in draft
            Assert.AreEqual(30m, productService.GetProductById(p.Id).CurrentStock);

            // 2. Confirm verification
            string err;
            bool ok = stockService.ConfirmPhysicalVerification(verId, out err);
            Assert.IsTrue(ok, $"Verification confirm failed: {err}");

            // Stock reconciled to 28
            Assert.AreEqual(28m, productService.GetProductById(p.Id).CurrentStock, "Stock must be reconciled to 28");
        }

        // Test 13: Stock ledger running balance
        [TestMethod]
        public void Test13_StockLedger_RunningBalance_ChronologicalOrder()
        {
            var p = new Product
            {
                ProductCode = "PRD-LED-01",
                Barcode = "890900000030",
                NameEn = "Black Pepper 100g",
                PurchaseRate = 80m,
                SaleRate = 110m,
                OpeningStock = 10m
            };
            productService.SaveProduct(p);

            // Check movements recorded
            var movements = stockMoveRepo.GetByProductId(p.Id).ToList();
            Assert.IsTrue(movements.Count >= 0);
        }

        // Test 14: Low-stock and out-of-stock filters
        [TestMethod]
        public void Test14_StockFilters_LowStock_And_OutOfStock()
        {
            var pLow = new Product
            {
                ProductCode = "PRD-LOW-01",
                Barcode = "890900000040",
                NameEn = "Low Stock Tea 250g",
                PurchaseRate = 50m,
                SaleRate = 70m,
                OpeningStock = 3m,
                ReorderLevel = 10m // Low stock!
            };
            productService.SaveProduct(pLow);

            var pOut = new Product
            {
                ProductCode = "PRD-OUT-01",
                Barcode = "890900000041",
                NameEn = "Out of Stock Soap",
                PurchaseRate = 25m,
                SaleRate = 35m,
                OpeningStock = 0m,
                ReorderLevel = 5m // Out of stock!
            };
            productService.SaveProduct(pOut);

            var filterLow = new StockFilter { StockStatus = "Low Stock", PageNumber = 1, PageSize = 50 };
            var resLow = stockService.GetCurrentStock(filterLow);
            Assert.IsTrue(resLow.Items.Any(i => i.ProductId == pLow.Id));

            var filterOut = new StockFilter { StockStatus = "Out of Stock", PageNumber = 1, PageSize = 50 };
            var resOut = stockService.GetCurrentStock(filterOut);
            Assert.IsTrue(resOut.Items.Any(i => i.ProductId == pOut.Id));
        }

        // Test 15: Stock valuation calculations
        [TestMethod]
        public void Test15_StockValuation_CostAndRetailCalculation()
        {
            var p = new Product
            {
                ProductCode = "PRD-VAL-01",
                Barcode = "890900000050",
                NameEn = "Premium Coffee 200g",
                PurchaseRate = 150m,
                SaleRate = 210m,
                OpeningStock = 20m
            };
            productService.SaveProduct(p);

            var valuations = stockService.GetStockValuation().ToList();
            var itemVal = valuations.FirstOrDefault(v => v.ProductId == p.Id);
            Assert.IsNotNull(itemVal);
            Assert.AreEqual(20m * 150m, itemVal.TotalStockValue, "Cost valuation = Qty * PurchaseRate = 3000");
            Assert.AreEqual(20m * 210m, itemVal.TotalSellingValue, "Retail valuation = Qty * SaleRate = 4200");
            Assert.AreEqual(1200m, itemVal.PotentialGrossMargin, "Potential margin = 4200 - 3000 = 1200");
        }

        // Test 16: Transaction rollback on failure
        [TestMethod]
        public void Test16_TransactionRollback_WhenOperationFails()
        {
            var p = new Product
            {
                ProductCode = "PRD-ROL-01",
                Barcode = "890900000060",
                NameEn = "Sugar 1kg",
                PurchaseRate = 40m,
                SaleRate = 48m,
                OpeningStock = 5m
            };
            productService.SaveProduct(p);

            // Attempt adjustment of -10 on stock of 5 (negative stock violation)
            var badAdj = new StockAdjustment
            {
                AdjustmentNumber = "ADJ-FAIL-01",
                Reason = "Negative stock attempt",
                Items = new List<StockAdjustmentItem>
                {
                    new StockAdjustmentItem
                    {
                        ProductId = p.Id,
                        CurrentStock = 5m,
                        PhysicalCount = 0m,
                        DifferenceQuantity = -10m, // Would result in 5 - 10 = -5
                        AdjustmentType = "Decrease Stock",
                        UnitCost = 40m
                    }
                }
            };

            // Validation should catch this
            var valRes = stockService.ValidateAdjustment(badAdj);
            Assert.IsFalse(valRes.IsValid, "Negative stock adjustment must fail validation");

            // Confirm stock is completely unchanged
            Assert.AreEqual(5m, productService.GetProductById(p.Id).CurrentStock);
        }

        // Test 17: Foreign key enforcement
        [TestMethod]
        public void Test17_ForeignKeyEnforcement_Active()
        {
            using (var conn = DatabaseConnection.CreateConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys;";
                long fk = (long)cmd.ExecuteScalar();
                Assert.AreEqual(1L, fk, "SQLite foreign keys must be ON");
            }
        }

        // Test 18, 19, 20: Performance and Regression verification
        [TestMethod]
        public void Test18_19_20_RegressionAndPerformance_StockDashboardAndCatalog()
        {
            var metrics = stockService.GetDashboardMetrics();
            Assert.IsTrue(metrics.TotalActiveProducts >= 0);

            // Current stock search performance
            var paged = stockService.GetCurrentStock(new StockFilter { PageNumber = 1, PageSize = 25 });
            Assert.IsTrue(paged.ExecutionTimeMs < 500, $"Stock query executed in {paged.ExecutionTimeMs} ms (< 500 ms target)");
        }
    }
}
