using System;

namespace ShopBilling.Core.Models
{
    public class SupplierPayment
    {
        public long Id { get; set; }
        public string PaymentNumber { get; set; } // PMT-2026-0001
        public long SupplierId { get; set; }
        public string SupplierName { get; set; }
        public long? PurchaseId { get; set; }
        public string PurchaseNumber { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        public string PaymentMethod { get; set; } = "Cash"; // "Cash", "Bank Transfer", "UPI", "Cheque", "Other"
        public decimal Amount { get; set; }
        public string ReferenceNumber { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class StockMovement
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public string ReferenceType { get; set; } // "PURCHASE_CONFIRM", "PURCHASE_CANCEL", "ADJUSTMENT"
        public string ReferenceId { get; set; }   // e.g. PUR-2026-0001
        public decimal Quantity { get; set; }      // positive = in, negative = out
        public decimal QuantityBefore { get; set; }
        public decimal QuantityAfter { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class SupplierLedgerEntry
    {
        public long Id { get; set; }
        public long SupplierId { get; set; }
        public string TransactionType { get; set; } // "OPENING_BALANCE", "PURCHASE", "PAYMENT", "PURCHASE_CANCEL"
        public string ReferenceId { get; set; }
        public decimal Debit { get; set; }          // Payments
        public decimal Credit { get; set; }         // Purchases
        public decimal BalanceAfter { get; set; }
        public string Notes { get; set; }
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    }
}
