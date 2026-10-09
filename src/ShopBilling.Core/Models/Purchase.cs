using System;
using System.Collections.Generic;

namespace ShopBilling.Core.Models
{
    public enum PurchaseStatus
    {
        Draft,
        Confirmed,
        Cancelled
    }

    public class PurchaseHeader
    {
        public long Id { get; set; }
        public string PurchaseNumber { get; set; }          // e.g. PUR-2026-0001
        public long SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string SupplierInvoiceNumber { get; set; }
        public DateTime? SupplierInvoiceDate { get; set; }
        public DateTime GoodsReceivedDate { get; set; } = DateTime.UtcNow;
        public string PurchaseType { get; set; } = "Credit"; // "Cash" or "Credit"
        public DateTime? PaymentDueDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; } = "Draft";       // "Draft", "Confirmed", "Cancelled"

        // Totals & Calculations
        public decimal TotalQty { get; set; }
        public decimal TotalFreeQty { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal ItemDiscount { get; set; }
        public decimal AdditionalDiscount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal AdditionalCharges { get; set; }
        public decimal RoundOff { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }

        public string CreatedBy { get; set; } = "Admin";
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Line items
        public List<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
    }

    public class PurchaseItem
    {
        public long Id { get; set; }
        public long PurchaseId { get; set; }
        public long ProductId { get; set; }
        public string ProductCode { get; set; }
        public string Barcode { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        
        public decimal Quantity { get; set; }
        public decimal FreeQuantity { get; set; }
        public decimal PurchaseRate { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal LineTotal { get; set; }
        public string BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }

    public class PurchaseFilter
    {
        public string SearchTerm { get; set; }
        public long? SupplierId { get; set; }
        public string Status { get; set; } // "All", "Draft", "Confirmed", "Cancelled"
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
