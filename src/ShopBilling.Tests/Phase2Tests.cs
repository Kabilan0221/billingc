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
    public class Phase2Tests
    {
        private string testDbPath;
        private ProductRepository productRepo;
        private CategoryRepository categoryRepo;
        private BrandRepository brandRepo;
        private AuditLogRepository auditRepo;
        private SupplierRepository supplierRepo;
        private PurchaseRepository purchaseRepo;
        private SupplierPaymentRepository paymentRepo;
        private StockMovementRepository stockMoveRepo;

        private ProductService productService;
        private SupplierService supplierService;
        private PurchaseService purchaseService;
        private SupplierPaymentService paymentService;

        [TestInitialize]
        public void Setup()
        {
            testDbPath = Path.Combine(Path.GetTempPath(), $"ShopBilling_Phase2_{Guid.NewGuid():N}.db");
            DatabaseConnection.Initialize(testDbPath);
            DatabaseMigrator.RunMigrations();

            productRepo = new ProductRepository();
            categoryRepo = new CategoryRepository();
            brandRepo = new BrandRepository();
            auditRepo = new AuditLogRepository();
            supplierRepo = new SupplierRepository();
            purchaseRepo = new PurchaseRepository();
            paymentRepo = new SupplierPaymentRepository();
            stockMoveRepo = new StockMovementRepository();

            productService = new ProductService(productRepo, auditRepo);
            supplierService = new SupplierService(supplierRepo, auditRepo);
            purchaseService = new PurchaseService(purchaseRepo, productRepo, supplierRepo, auditRepo);
            paymentService = new SupplierPaymentService(paymentRepo, supplierRepo, auditRepo);
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

        [TestMethod]
        public void Supplier_Add_Edit_Deactivate_DuplicateValidation()
        {
            // 1. Add valid supplier
            var s1 = new Supplier
            {
                Code = "SUP-TST-01",
                Name = "Velan Rice Mills",
                ContactPerson = "K. Velan",
                Mobile = "9840123456",
                Email = "info@velanmills.in",
                Gstin = "33AABCV1122D1Z4",
                City = "Thanjavur",
                State = "Tamil Nadu",
                OpeningBalance = 2500m
            };

            var res1 = supplierService.SaveSupplier(s1);
            Assert.IsTrue(res1.IsValid, "Valid supplier must pass validation");
            Assert.IsTrue(s1.Id > 0);
            Assert.AreEqual(2500m, s1.CurrentBalance);

            // 2. Duplicate Code rejected
            var s2 = new Supplier
            {
                Code = "SUP-TST-01", // Duplicate code!
                Name = "Another Supplier",
                Mobile = "9841234567"
            };
            var res2 = supplierService.SaveSupplier(s2);
            Assert.IsFalse(res2.IsValid);
            Assert.IsTrue(res2.Errors.Any(e => e.Contains("already registered")));

            // 3. Invalid GSTIN format rejected
            var s3 = new Supplier
            {
                Code = "SUP-TST-03",
                Name = "Bad GSTIN Supplier",
                Gstin = "INVALID_GSTIN_123"
            };
            var res3 = supplierService.SaveSupplier(s3);
            Assert.IsFalse(res3.IsValid);
            Assert.IsTrue(res3.Errors.Any(e => e.Contains("GSTIN")));

            // 4. Invalid mobile rejected
            var s4 = new Supplier
            {
                Code = "SUP-TST-04",
                Name = "Bad Mobile Supplier",
                Mobile = "12345" // Not 10 digits
            };
            var res4 = supplierService.SaveSupplier(s4);
            Assert.IsFalse(res4.IsValid);
            Assert.IsTrue(res4.Errors.Any(e => e.Contains("Mobile Number")));

            // 5. Deactivate
            string err;
            bool deact = supplierService.DeactivateSupplier(s1.Id, out err);
            Assert.IsTrue(deact);
            var deactivated = supplierService.GetById(s1.Id);
            Assert.IsFalse(deactivated.IsActive);
        }

        [TestMethod]
        public void Purchase_Calculations_DraftDoesNotAffectStock_ConfirmUpdatesStockExactlyOnce()
        {
            // 1. Create a test product with initial stock 10
            var prod = new Product
            {
                ProductCode = "PRD-PUR-01",
                Barcode = "890888800001",
                NameEn = "Test Toor Dal 1kg",
                PurchaseRate = 120m,
                SaleRate = 140m,
                Mrp = 150m,
                TaxRate = 5m,
                OpeningStock = 10m
            };
            productService.SaveProduct(prod);
            long prodId = prod.Id;

            var initialProd = productService.GetProductById(prodId);
            Assert.AreEqual(10m, initialProd.CurrentStock);

            // 2. Create supplier
            var sup = new Supplier
            {
                Code = "SUP-PUR-01",
                Name = "Pulses Wholesale Hub",
                Mobile = "9840999888"
            };
            supplierService.SaveSupplier(sup);

            // 3. Create Draft Purchase Order
            // Item: Qty 50, FreeQty 5, Rate 100, Disc 10%, Tax 5%
            // Gross: 50 * 100 = 5000. Disc: 500. Taxable: 4500. Tax: 225. Total: 4725.
            var purchase = new PurchaseHeader
            {
                PurchaseNumber = "PUR-2026-TEST1",
                SupplierId = sup.Id,
                SupplierInvoiceNumber = "INV-0012",
                Items = new List<PurchaseItem>
                {
                    new PurchaseItem
                    {
                        ProductId = prodId,
                        Quantity = 50m,
                        FreeQuantity = 5m,
                        PurchaseRate = 100m,
                        DiscountPercent = 10m,
                        TaxRate = 5m
                    }
                }
            };

            var draftRes = purchaseService.SaveDraft(purchase);
            Assert.IsTrue(draftRes.IsValid, "Draft save must succeed");
            Assert.AreEqual(4725m, purchase.GrandTotal);

            // Verify stock is UNCHANGED after draft save
            var stockAfterDraft = productService.GetProductById(prodId).CurrentStock;
            Assert.AreEqual(10m, stockAfterDraft, "Draft purchase must NOT alter stock");

            // Verify supplier balance is UNCHANGED after draft save
            var supAfterDraft = supplierService.GetById(sup.Id).CurrentBalance;
            Assert.AreEqual(0m, supAfterDraft, "Draft purchase must NOT alter supplier balance");

            // 4. Confirm Purchase
            string confirmErr;
            bool confirmed = purchaseService.ConfirmPurchase(purchase.Id, out confirmErr);
            Assert.IsTrue(confirmed, $"Confirm failed: {confirmErr}");

            // Verify stock increased by Quantity (50) + FreeQuantity (5) = +55
            // Expected stock = 10 + 55 = 65
            var stockAfterConfirm = productService.GetProductById(prodId).CurrentStock;
            Assert.AreEqual(65m, stockAfterConfirm, "Confirmed purchase must update stock by Qty + FreeQty");

            // Verify stock movement ledger record
            var movements = stockMoveRepo.GetByProductId(prodId).ToList();
            Assert.IsTrue(movements.Count > 0);
            Assert.AreEqual(55m, movements[0].Quantity);
            Assert.AreEqual(10m, movements[0].QuantityBefore);
            Assert.AreEqual(65m, movements[0].QuantityAfter);

            // Verify supplier balance increased by GrandTotal (4725)
            var supAfterConfirm = supplierService.GetById(sup.Id).CurrentBalance;
            Assert.AreEqual(4725m, supAfterConfirm);

            // 5. Duplicate Confirmation Prevention
            bool secondConfirm = purchaseService.ConfirmPurchase(purchase.Id, out confirmErr);
            Assert.IsFalse(secondConfirm, "Cannot confirm a purchase that is already confirmed");
            Assert.AreEqual(65m, productService.GetProductById(prodId).CurrentStock, "Duplicate confirm must not double-increment stock");
        }

        [TestMethod]
        public void Purchase_Cancellation_ReversesStockAndProtectsNegativeStock()
        {
            // 1. Create product & supplier
            var prod = new Product
            {
                ProductCode = "PRD-REV-01",
                Barcode = "890888800002",
                NameEn = "Cooking Oil 1L",
                PurchaseRate = 110m,
                SaleRate = 130m,
                Mrp = 140m,
                TaxRate = 5m,
                OpeningStock = 0m
            };
            productService.SaveProduct(prod);

            var sup = new Supplier { Code = "SUP-REV-01", Name = "Oil Mill Co", Mobile = "9840112233" };
            supplierService.SaveSupplier(sup);

            // 2. Create and confirm purchase of 20 units
            var purchase = new PurchaseHeader
            {
                PurchaseNumber = "PUR-2026-REV1",
                SupplierId = sup.Id,
                Items = new List<PurchaseItem>
                {
                    new PurchaseItem { ProductId = prod.Id, Quantity = 20m, PurchaseRate = 100m, TaxRate = 0m }
                }
            };
            purchaseService.SaveDraft(purchase);
            string err;
            purchaseService.ConfirmPurchase(purchase.Id, out err);

            Assert.AreEqual(20m, productService.GetProductById(prod.Id).CurrentStock);
            Assert.AreEqual(2000m, supplierService.GetById(sup.Id).CurrentBalance);

            // 3. Cancel confirmed purchase
            bool cancelled = purchaseService.CancelPurchase(purchase.Id, out err);
            Assert.IsTrue(cancelled, $"Cancellation failed: {err}");

            // Verify stock reversed back to 0
            Assert.AreEqual(0m, productService.GetProductById(prod.Id).CurrentStock);
            // Verify supplier balance reversed back to 0
            Assert.AreEqual(0m, supplierService.GetById(sup.Id).CurrentBalance);
            // Verify status is Cancelled
            Assert.AreEqual("Cancelled", purchaseService.GetById(purchase.Id).Status);

            // 4. Test Negative Stock Protection
            // Create another purchase of 10 units, confirm it, reduce stock manually to 5, then attempt cancel 10
            var purchase2 = new PurchaseHeader
            {
                PurchaseNumber = "PUR-2026-REV2",
                SupplierId = sup.Id,
                Items = new List<PurchaseItem>
                {
                    new PurchaseItem { ProductId = prod.Id, Quantity = 10m, PurchaseRate = 100m, TaxRate = 0m }
                }
            };
            purchaseService.SaveDraft(purchase2);
            purchaseService.ConfirmPurchase(purchase2.Id, out err);

            // Simulate that 8 units were sold (Current stock = 10 - 8 = 2)
            var pFetched = productService.GetProductById(prod.Id);
            pFetched.CurrentStock = 2m;
            productService.SaveProduct(pFetched);

            // Now attempting to cancel purchase of 10 units should be REJECTED to prevent negative stock!
            bool cancelRejected = purchaseService.CancelPurchase(purchase2.Id, out err);
            Assert.IsFalse(cancelRejected, "Should reject cancellation that causes negative stock");
            Assert.IsTrue(err.Contains("negative"));
        }

        [TestMethod]
        public void SupplierPayment_PartialAndFullPayment_UpdatesBalance()
        {
            var sup = new Supplier { Code = "SUP-PAY-01", Name = "Payment Test Supplier", Mobile = "9840556677", OpeningBalance = 10000m };
            supplierService.SaveSupplier(sup);
            Assert.AreEqual(10000m, supplierService.GetById(sup.Id).CurrentBalance);

            // 1. Record Partial Payment of 4000
            var pmt1 = new SupplierPayment
            {
                SupplierId = sup.Id,
                Amount = 4000m,
                PaymentMethod = "Bank Transfer",
                ReferenceNumber = "UTR12345678"
            };
            var res1 = paymentService.RecordPayment(pmt1);
            Assert.IsTrue(res1.IsValid);

            // Balance should now be 10000 - 4000 = 6000
            Assert.AreEqual(6000m, supplierService.GetById(sup.Id).CurrentBalance);

            // 2. Record Remaining Payment of 6000
            var pmt2 = new SupplierPayment
            {
                SupplierId = sup.Id,
                Amount = 6000m,
                PaymentMethod = "UPI",
                ReferenceNumber = "UPI987654"
            };
            var res2 = paymentService.RecordPayment(pmt2);
            Assert.IsTrue(res2.IsValid);

            // Balance should now be 0
            Assert.AreEqual(0m, supplierService.GetById(sup.Id).CurrentBalance);

            // 3. Verify Ledger has entries
            var ledger = paymentService.GetSupplierLedger(sup.Id).ToList();
            Assert.IsTrue(ledger.Count >= 2);
        }
    }
}
