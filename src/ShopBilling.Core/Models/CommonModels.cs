using System;
using System.Collections.Generic;

namespace ShopBilling.Core.Models
{
    public class Category
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        public int ProductCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Subcategory
    {
        public long Id { get; set; }
        public long CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        public int ProductCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Brand
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;
        public int ProductCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ProductFilter
    {
        public string SearchTerm { get; set; }
        public long? CategoryId { get; set; }
        public long? SubcategoryId { get; set; }
        public long? BrandId { get; set; }
        public bool? IsActive { get; set; } = true;
        public bool? IsLowStockOnly { get; set; }
        public string SortBy { get; set; } = "name_en"; // name_en, barcode, product_code, sale_rate, current_stock
        public bool SortDescending { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / Math.Max(1, PageSize));
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public double ExecutionTimeMs { get; set; }
    }

    public class ExcelImportRow
    {
        public int RowIndex { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string NameEn { get; set; }
        public string NameTa { get; set; }
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public string Brand { get; set; }
        public string Unit { get; set; }
        public string HsnCode { get; set; }
        public decimal PurchaseRate { get; set; }
        public decimal SaleRate { get; set; }
        public decimal WholesaleRate { get; set; }
        public decimal Mrp { get; set; }
        public decimal TaxRate { get; set; }
        public decimal OpeningStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => ValidationErrors.Count == 0;
    }

    public class ExcelImportResult
    {
        public int TotalRows { get; set; }
        public int SuccessfullyImported { get; set; }
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public double DurationMs { get; set; }
        public List<ExcelImportRow> FailedRows { get; set; } = new List<ExcelImportRow>();
    }

    public class AuditLog
    {
        public long Id { get; set; }
        public string Entity { get; set; }
        public string EntityId { get; set; }
        public string Action { get; set; } // INSERT, UPDATE, DEACTIVATE, RESTORE, IMPORT
        public string Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
