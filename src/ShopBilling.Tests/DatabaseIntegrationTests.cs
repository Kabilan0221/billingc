using System;
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
    public class DatabaseIntegrationTests
    {
        private string testDbPath;
        private ProductRepository productRepo;
        private CategoryRepository categoryRepo;
        private BrandRepository brandRepo;
        private AuditLogRepository auditRepo;
        private ProductService productService;

        [TestInitialize]
        public void Setup()
        {
            testDbPath = Path.Combine(Path.GetTempPath(), $"ShopBilling_Test_{Guid.NewGuid():N}.db");
            DatabaseConnection.Initialize(testDbPath);
            DatabaseMigrator.RunMigrations();

            productRepo = new ProductRepository();
            categoryRepo = new CategoryRepository();
            brandRepo = new BrandRepository();
            auditRepo = new AuditLogRepository();
            productService = new ProductService(productRepo, auditRepo);
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
        public void Migrations_CreatesDefaultSeedCategoriesAndBrands()
        {
            var categories = categoryRepo.GetAll().ToList();
            Assert.IsTrue(categories.Count >= 6, "Expected at least 6 default categories seeded by migration v3");

            var brands = brandRepo.GetAll().ToList();
            Assert.IsTrue(brands.Count >= 8, "Expected at least 8 default brands seeded by migration v3");
        }

        [TestMethod]
        public void ProductCRUD_WithTamilUnicode_PreservesTamilCharacters()
        {
            var p = new Product
            {
                ProductCode = "PRD-TEST-01",
                Barcode = "890999900001",
                NameEn = "Heritage Cow Milk 500ml",
                NameTa = "ஹெரிடேஜ் பசும்பால் 500மி.லி",
                CategoryId = 2,
                Unit = "PACK",
                PurchaseRate = 22m,
                SaleRate = 26m,
                Mrp = 26m,
                TaxRate = 0m,
                OpeningStock = 50m,
                ReorderLevel = 10m
            };

            var saveResult = productService.SaveProduct(p);
            Assert.IsTrue(saveResult.IsValid, "Product save should succeed");
            Assert.IsTrue(p.Id > 0, "Product ID should be generated");

            var fetched = productService.GetProductById(p.Id);
            Assert.IsNotNull(fetched);
            Assert.AreEqual("ஹெரிடேஜ் பசும்பால் 500மி.லி", fetched.NameTa, "Tamil Unicode characters must match exactly");
            Assert.AreEqual("Heritage Cow Milk 500ml", fetched.NameEn);

            // Update
            fetched.SaleRate = 27m;
            fetched.Mrp = 27m;
            var updateResult = productService.SaveProduct(fetched);
            Assert.IsTrue(updateResult.IsValid);

            var updated = productService.GetProductById(p.Id);
            Assert.AreEqual(27m, updated.SaleRate);

            // Deactivate
            bool deact = productService.DeactivateProduct(p.Id);
            Assert.IsTrue(deact);
            var deactivated = productService.GetProductById(p.Id);
            Assert.IsFalse(deactivated.IsActive);
        }

        [TestMethod]
        public void DuplicateValidation_RejectsExistingBarcodeAndCode()
        {
            var p1 = new Product
            {
                ProductCode = "DUP01",
                Barcode = "890111111111",
                NameEn = "Item 1",
                SaleRate = 10m,
                Mrp = 10m
            };
            productService.SaveProduct(p1);

            var p2 = new Product
            {
                ProductCode = "DUP02",
                Barcode = "890111111111", // Duplicate barcode
                NameEn = "Item 2",
                SaleRate = 15m,
                Mrp = 15m
            };
            var res2 = productService.SaveProduct(p2);
            Assert.IsFalse(res2.IsValid);
            Assert.IsTrue(res2.Errors.Any(e => e.Contains("Barcode") && e.Contains("already assigned")));
        }
    }
}
