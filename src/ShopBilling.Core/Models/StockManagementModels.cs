using System;
using System.Collections.Generic;

namespace ShopBilling.Core.Models
{
    public enum StockMovementType
    {
        OpeningStock,
        PurchaseReceipt,
        PurchaseReversal,
        StockAdjustmentIn,
        StockAdjustmentOut,
        Sales,
        SalesReturn,
        SupplierReturn,
        DamagedExpiredStock,
        PhysicalCountCorrection,
        OtherApprovedMovement
    }

    public class StockAdjustment
    {
        public long Id { get; set; }
        public string AdjustmentNumber { get; set; } // e.g. ADJ-2026-0001
        public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;
        public string Reason { get; set; }           // Required
        public string Notes { get; set; }
        public string Status { get; set; } = "Draft"; // "Draft", "Confirmed", "Cancelled"
        public int TotalItems { get; set; }
        public decimal TotalDifferenceQuantity { get; set; }
        public decimal TotalCostImpact { get; set; }
        public string CreatedBy { get; set; } = "Admin";
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
    }

    public class StockAdjustmentItem
    {
        public long Id { get; set; }
        public long AdjustmentId { get; set; }
        public long ProductId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public decimal CurrentStock { get; set; }      // System stock at time of adjustment
        public decimal PhysicalCount { get; set; }     // Counted quantity
        public decimal DifferenceQuantity { get; set; } // Physical - Current (positive = increase, negative = decrease)
        public string AdjustmentType { get; set; }     // "Increase Stock", "Decrease Stock", "Damaged Goods", "Expired Goods", "Physical Count Correction", "Other"
        public decimal UnitCost { get; set; }
        public decimal CostImpact => DifferenceQuantity * UnitCost;
        public string Reason { get; set; }
        public string Notes { get; set; }
    }

    public class PhysicalStockVerification
    {
        public long Id { get; set; }
        public string VerificationNumber { get; set; } // e.g. VER-2026-0001
        public DateTime VerificationDate { get; set; } = DateTime.UtcNow;
        public long? CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Status { get; set; } = "Draft"; // "Draft", "Confirmed", "Cancelled"
        public int TotalCountedProducts { get; set; }
        public int TotalDiscrepancies { get; set; }
        public decimal NetDifferenceQuantity { get; set; }
        public string Notes { get; set; }
        public string CreatedBy { get; set; } = "Admin";
        public DateTime? ConfirmedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<PhysicalStockVerificationItem> Items { get; set; } = new List<PhysicalStockVerificationItem>();
    }

    public class PhysicalStockVerificationItem
    {
        public long Id { get; set; }
        public long VerificationId { get; set; }
        public long ProductId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public decimal SystemStockAtStart { get; set; }   // Stock recorded when verification draft was created
        public decimal SystemStockAtConfirm { get; set; } // Stock checked right before confirmation (to detect concurrent changes)
        public decimal PhysicalCount { get; set; }        // Physically audited count
        public decimal DifferenceQuantity { get; set; }   // Physical - System
        public bool DiscrepancyResolved { get; set; }
        public string Notes { get; set; }
    }

    public class CurrentStockSummary
    {
        public long ProductId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public string BrandName { get; set; }
        public string Unit { get; set; }
        public decimal OpeningStock { get; set; }
        public decimal PurchasedQuantity { get; set; }
        public decimal FreeQuantityReceived { get; set; }
        public decimal SoldQuantity { get; set; }          // 0 until Phase 4 Billing
        public decimal ReturnedQuantity { get; set; }      // 0 until returns
        public decimal AdjustmentQuantity { get; set; }    // Net adjustments (in - out)
        public decimal CurrentStock { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal MaxStockLevel { get; set; }
        public decimal PurchaseRate { get; set; }
        public decimal SaleRate { get; set; }
        public decimal StockValuePurchase => CurrentStock * PurchaseRate;
        public decimal StockValueSale => CurrentStock * SaleRate;
        public string StockStatus
        {
            get
            {
                if (CurrentStock <= 0) return "Out of Stock";
                if (CurrentStock <= ReorderLevel) return "Low Stock";
                if (MaxStockLevel > 0 && CurrentStock > MaxStockLevel) return "Excess Stock";
                return "In Stock";
            }
        }
    }

    public class StockValuationSummary
    {
        public long ProductId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal CostRate { get; set; }           // Purchase cost / average purchase price
        public decimal TotalStockValue => CurrentQuantity * CostRate;
        public decimal SellingPrice { get; set; }
        public decimal TotalSellingValue => CurrentQuantity * SellingPrice;
        public decimal PotentialGrossMargin => TotalSellingValue - TotalStockValue;
        public string ValuationMethod { get; set; } = "Purchase Cost (FIFO Equivalent)";
    }

    public class StockDashboardMetrics
    {
        public int TotalActiveProducts { get; set; }
        public decimal TotalStockQuantity { get; set; }
        public int OutOfStockCount { get; set; }
        public int LowStockCount { get; set; }
        public int ExcessStockCount { get; set; }
        public int AdjustedProductsCount { get; set; }
        public decimal StockValueAtCost { get; set; }
        public decimal StockValueAtSale { get; set; }
        public decimal PotentialGrossMargin => StockValueAtSale - StockValueAtCost;
        public List<StockMovement> RecentMovements { get; set; } = new List<StockMovement>();
    }

    public class StockFilter
    {
        public string SearchTerm { get; set; }
        public long? CategoryId { get; set; }
        public long? BrandId { get; set; }
        public string StockStatus { get; set; } // "All", "In Stock", "Low Stock", "Out of Stock", "Excess Stock"
        public string SortBy { get; set; } = "name";
        public bool SortDescending { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class StockMovementFilter
    {
        public long? ProductId { get; set; }
        public long? CategoryId { get; set; }
        public string MovementType { get; set; } // "All", "Purchase Receipt", "Stock Adjustment In", etc.
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string ReferenceNumber { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class StockAdjustmentFilter
    {
        public string SearchTerm { get; set; }
        public string Status { get; set; } // "All", "Draft", "Confirmed", "Cancelled"
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
