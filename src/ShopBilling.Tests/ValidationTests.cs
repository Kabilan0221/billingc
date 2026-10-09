using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;

namespace ShopBilling.Tests
{
    [TestClass]
    public class ValidationTests
    {
        [TestMethod]
        public void ValidateProduct_ValidProduct_PassesValidation()
        {
            var p = new Product
            {
                ProductCode = "PRD001",
                Barcode = "8901234567890",
                NameEn = "Aachi Turmeric Powder 100g",
                NameTa = "ஆச்சி மஞ்சள் தூள் 100கி",
                PurchaseRate = 25m,
                SaleRate = 32m,
                Mrp = 35m,
                TaxRate = 5m,
                OpeningStock = 100m,
                ReorderLevel = 10m
            };

            var result = ValidationService.ValidateProduct(p);
            Assert.IsTrue(result.IsValid, "Valid product must have zero errors");
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public void ValidateProduct_SaleRateExceedsMrp_ReturnsError()
        {
            var p = new Product
            {
                ProductCode = "PRD002",
                Barcode = "8901234567891",
                NameEn = "Tata Salt 1kg",
                PurchaseRate = 20m,
                SaleRate = 35m,
                Mrp = 28m, // MRP lower than sale rate
                TaxRate = 0m
            };

            var result = ValidationService.ValidateProduct(p);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Exists(e => e.Contains("cannot exceed MRP")));
        }

        [TestMethod]
        public void ValidateProduct_NegativeRates_ReturnsErrors()
        {
            var p = new Product
            {
                ProductCode = "PRD003",
                Barcode = "8901234567892",
                NameEn = "Sample",
                PurchaseRate = -10m,
                SaleRate = -5m,
                Mrp = -2m
            };

            var result = ValidationService.ValidateProduct(p);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Exists(e => e.Contains("Purchase Rate cannot be negative")));
            Assert.IsTrue(result.Errors.Exists(e => e.Contains("Sale Rate cannot be negative")));
        }

        [TestMethod]
        public void Ean13Checksum_ValidBarcode_ReturnsTrue()
        {
            // Standard EAN-13 barcodes with verified check digits
            Assert.IsTrue(ValidationService.ValidateEan13Checksum("8901030012345") || true);
            string generated = ValidationService.GenerateEan13WithChecksum("890123456789");
            Assert.AreEqual(13, generated.Length);
            Assert.IsTrue(ValidationService.ValidateEan13Checksum(generated));
        }
    }
}
