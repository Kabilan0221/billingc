using System;

namespace ShopBilling.Core.Models
{
    public class Product
    {
        public long Id { get; set; }
        public string ProductCode { get; set; }       // Unique Product Code e.g. PRD0001
        public string Barcode { get; set; }           // Unique Barcode e.g. 8901234567890
        public string NameEn { get; set; }            // Product Name in English
        public string NameTa { get; set; }            // Product Name in Tamil (Unicode UTF-8)
        
        public long? CategoryId { get; set; }
        public string CategoryName { get; set; }
        
        public long? SubcategoryId { get; set; }
        public string SubcategoryName { get; set; }
        
        public long? BrandId { get; set; }
        public string BrandName { get; set; }
        
        public string Unit { get; set; } = "PCS";     // PCS, KG, G, LTR, ML, BOX, PACK
        public string HsnCode { get; set; }          // HSN Code for GST
        
        public decimal PurchaseRate { get; set; }    // Cost price excluding or including tax
        public decimal SaleRate { get; set; }        // Retail selling price
        public decimal WholesaleRate { get; set; }   // Wholesale bulk price
        public decimal Mrp { get; set; }             // Maximum Retail Price
        public decimal TaxRate { get; set; }         // GST Rate % (0, 5, 12, 18, 28)
        
        public decimal OpeningStock { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal ReorderLevel { get; set; }    // Alert threshold
        public decimal MaxStockLevel { get; set; }
        
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Computed helpers
        public bool IsLowStock => CurrentStock <= ReorderLevel;
        
        public decimal CgstRate => TaxRate / 2m;
        public decimal SgstRate => TaxRate / 2m;
        
        public decimal SaleRateWithTax => SaleRate + (SaleRate * TaxRate / 100m);
    }
}
