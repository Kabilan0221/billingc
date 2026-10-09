using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;
using ShopBilling.Data.Database;
using ShopBilling.Data.Repositories;

namespace ShopBilling.Tests
{
    [TestClass]
    public class PerformanceBenchmarkTests
    {
        private string testDbPath;
        private ProductRepository productRepo;
        private AuditLogRepository auditRepo;
        private ProductService productService;

        [TestInitialize]
        public void Setup()
        {
            testDbPath = Path.Combine(Path.GetTempPath(), $"ShopBilling_Perf_{Guid.NewGuid():N}.db");
            DatabaseConnection.Initialize(testDbPath);
            DatabaseMigrator.RunMigrations();

            productRepo = new ProductRepository();
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
        public void Benchmark_50000_Products_SearchUnder500ms()
        {
            // 1. Seed 50,000 products
            var products = new List<Product>(50000);
            var rand = new Random(100);

            for (int i = 1; i <= 50000; i++)
            {
                products.Add(new Product
                {
                    ProductCode = $"PRD{i:D6}",
                    Barcode = $"890{i:D10}",
                    NameEn = $"FMCG Product Item {i}",
                    NameTa = $"பொருள் {i}",
                    CategoryId = (long)(rand.Next(1, 6)),
                    BrandId = (long)(rand.Next(1, 8)),
                    Unit = "PCS",
                    PurchaseRate = rand.Next(10, 500),
                    SaleRate = rand.Next(15, 600),
                    Mrp = rand.Next(20, 650),
                    TaxRate = 5,
                    OpeningStock = rand.Next(10, 200),
                    CurrentStock = rand.Next(0, 200),
                    ReorderLevel = 10,
                    IsActive = true
                });
            }

            var insertSw = Stopwatch.StartNew();
            int inserted = productRepo.BulkInsert(products, false);
            insertSw.Stop();

            Assert.AreEqual(50000, inserted, "Must successfully insert all 50,000 items in SQLite");

            // 2. Exact Barcode Lookup Benchmark (Indexed)
            var barcodeSw = Stopwatch.StartNew();
            var barcodeResult = productService.GetProductByBarcode("890000025000");
            barcodeSw.Stop();

            Assert.IsNotNull(barcodeResult);
            Assert.IsTrue(barcodeSw.ElapsedMilliseconds < 50, $"Indexed barcode lookup took {barcodeSw.ElapsedMilliseconds}ms (Target <50ms)");

            // 3. Search Filter Benchmark (Paged 50 items with text search)
            var searchSw = Stopwatch.StartNew();
            var searchResult = productService.GetProducts(new ProductFilter
            {
                SearchTerm = "Item 250",
                PageNumber = 1,
                PageSize = 50
            });
            searchSw.Stop();

            Assert.IsTrue(searchResult.TotalCount > 0);
            Assert.IsTrue(searchSw.ElapsedMilliseconds < 500,
                $"Indexed multi-field search across 50,000 items took {searchSw.ElapsedMilliseconds}ms, which satisfies the <500ms target!");
        }
    }
}
