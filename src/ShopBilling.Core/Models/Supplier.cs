using System;

namespace ShopBilling.Core.Models
{
    public class Supplier
    {
        public long Id { get; set; }
        public string Code { get; set; }            // Unique e.g. SUP001
        public string Name { get; set; }            // Required
        public string ContactPerson { get; set; }
        public string Mobile { get; set; }
        public string AltMobile { get; set; }
        public string WhatsApp { get; set; }
        public string Email { get; set; }
        public string Gstin { get; set; }
        public string Pan { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; } = "Tamil Nadu";
        public string StateCode { get; set; } = "33";
        public string PinCode { get; set; }
        
        public decimal OpeningBalance { get; set; } // Opening balance payable
        public decimal CurrentBalance { get; set; } // Current calculated balance payable
        public string PaymentTerms { get; set; }    // e.g. "30 Days Net", "Immediate Cash"
        public string Notes { get; set; }
        
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Transaction stats
        public int TotalPurchasesCount { get; set; }
    }

    public class SupplierFilter
    {
        public string SearchTerm { get; set; }
        public bool? IsActive { get; set; } = true;
        public string SortBy { get; set; } = "name";
        public bool SortDescending { get; set; } = false;
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
