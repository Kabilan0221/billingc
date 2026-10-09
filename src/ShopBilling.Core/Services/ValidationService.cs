using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Services
{
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }

    public class ValidationService
    {
        public static ValidationResult ValidateProduct(Product product, bool isNew = false)
        {
            var result = new ValidationResult();

            if (product == null)
            {
                result.AddError("Product payload cannot be null.");
                return result;
            }

            // 1. Product Name (English)
            if (string.IsNullOrWhiteSpace(product.NameEn))
            {
                result.AddError("Product Name (English) is required.");
            }
            else if (product.NameEn.Trim().Length < 2)
            {
                result.AddError("Product Name (English) must be at least 2 characters.");
            }
            else if (product.NameEn.Length > 200)
            {
                result.AddError("Product Name (English) cannot exceed 200 characters.");
            }

            // 2. Product Name (Tamil) - optional, but length check if provided
            if (!string.IsNullOrWhiteSpace(product.NameTa) && product.NameTa.Length > 200)
            {
                result.AddError("Product Name (Tamil) cannot exceed 200 characters.");
            }

            // 3. Product Code
            if (string.IsNullOrWhiteSpace(product.ProductCode))
            {
                result.AddError("Product Code is required.");
            }
            else if (!Regex.IsMatch(product.ProductCode.Trim(), @"^[A-Za-z0-9\-_]{2,30}$"))
            {
                result.AddError("Product Code must be 2-30 alphanumeric characters (hyphens and underscores allowed).");
            }

            // 4. Barcode
            if (string.IsNullOrWhiteSpace(product.Barcode))
            {
                result.AddError("Barcode is required.");
            }
            else
            {
                var barcodeClean = product.Barcode.Trim();
                if (barcodeClean.Length < 3 || barcodeClean.Length > 48)
                {
                    result.AddError("Barcode length must be between 3 and 48 characters.");
                }
            }

            // 5. Rates & Currency Calculations
            if (product.PurchaseRate < 0)
            {
                result.AddError("Purchase Rate cannot be negative.");
            }

            if (product.SaleRate < 0)
            {
                result.AddError("Sale Rate cannot be negative.");
            }

            if (product.Mrp < 0)
            {
                result.AddError("MRP cannot be negative.");
            }

            if (product.WholesaleRate < 0)
            {
                result.AddError("Wholesale Rate cannot be negative.");
            }

            if (product.Mrp > 0 && product.SaleRate > product.Mrp)
            {
                result.AddError($"Sale Rate (₹{product.SaleRate:N2}) cannot exceed MRP (₹{product.Mrp:N2}).");
            }

            if (product.PurchaseRate > product.SaleRate && product.SaleRate > 0)
            {
                result.AddWarning($"Sale Rate (₹{product.SaleRate:N2}) is below Purchase Rate (₹{product.PurchaseRate:N2}), which will yield negative margin.");
            }

            // 6. Tax Rate
            if (product.TaxRate < 0 || product.TaxRate > 100)
            {
                result.AddError("Tax Rate must be between 0% and 100%.");
            }

            // 7. Stock Fields
            if (product.ReorderLevel < 0)
            {
                result.AddError("Reorder Level cannot be negative.");
            }

            return result;
        }

        public static ValidationResult ValidateSupplier(Supplier supplier, bool isNew = false)
        {
            var result = new ValidationResult();

            if (supplier == null)
            {
                result.AddError("Supplier cannot be null.");
                return result;
            }

            // 1. Supplier Name (Required, min 2 chars)
            if (string.IsNullOrWhiteSpace(supplier.Name))
            {
                result.AddError("Supplier Name is required.");
            }
            else if (supplier.Name.Trim().Length < 2)
            {
                result.AddError("Supplier Name must be at least 2 characters.");
            }

            // 2. Supplier Code (Required, 2-30 chars)
            if (string.IsNullOrWhiteSpace(supplier.Code))
            {
                result.AddError("Supplier Code is required.");
            }
            else if (!Regex.IsMatch(supplier.Code.Trim(), @"^[A-Za-z0-9\-_]{2,30}$"))
            {
                result.AddError("Supplier Code must be 2-30 alphanumeric characters (hyphens and underscores allowed).");
            }

            // 3. Mobile Number Format
            if (!string.IsNullOrWhiteSpace(supplier.Mobile))
            {
                var mob = supplier.Mobile.Trim();
                if (!Regex.IsMatch(mob, @"^[6-9]\d{9}$") && !Regex.IsMatch(mob, @"^0\d{2,4}-?\d{6,8}$"))
                {
                    result.AddError("Mobile Number must be a valid 10-digit Indian mobile number (starting with 6,7,8,9) or standard landline.");
                }
            }

            // 4. GSTIN Format (15 chars: StateCode + PAN + EntityNum + Z + Checksum)
            if (!string.IsNullOrWhiteSpace(supplier.Gstin))
            {
                var gstin = supplier.Gstin.Trim().ToUpper();
                if (!Regex.IsMatch(gstin, @"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$"))
                {
                    result.AddError("GSTIN must follow standard 15-character Indian GST format (e.g., 33AABCS1429B1Z1).");
                }
            }

            // 5. Email format
            if (!string.IsNullOrWhiteSpace(supplier.Email))
            {
                if (!Regex.IsMatch(supplier.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    result.AddError("Email address format is invalid.");
                }
            }

            return result;
        }

        public static ValidationResult ValidatePurchase(PurchaseHeader purchase)
        {
            var result = new ValidationResult();

            if (purchase == null)
            {
                result.AddError("Purchase record cannot be null.");
                return result;
            }

            if (purchase.SupplierId <= 0)
            {
                result.AddError("A valid Supplier must be selected.");
            }

            if (string.IsNullOrWhiteSpace(purchase.PurchaseNumber))
            {
                result.AddError("Purchase Number is required.");
            }

            if (purchase.Items == null || purchase.Items.Count == 0)
            {
                result.AddError("Purchase must contain at least one product item.");
                return result;
            }

            for (int i = 0; i < purchase.Items.Count; i++)
            {
                var item = purchase.Items[i];
                if (item.ProductId <= 0)
                {
                    result.AddError($"Row #{i + 1}: Valid product must be selected.");
                }
                if (item.Quantity <= 0)
                {
                    result.AddError($"Row #{i + 1}: Quantity must be greater than zero.");
                }
                if (item.FreeQuantity < 0)
                {
                    result.AddError($"Row #{i + 1}: Free Quantity cannot be negative.");
                }
                if (item.PurchaseRate < 0)
                {
                    result.AddError($"Row #{i + 1}: Purchase Rate cannot be negative.");
                }
                if (item.DiscountPercent < 0 || item.DiscountPercent > 100)
                {
                    result.AddError($"Row #{i + 1}: Discount percentage must be between 0% and 100%.");
                }
                if (item.TaxRate < 0 || item.TaxRate > 100)
                {
                    result.AddError($"Row #{i + 1}: Tax rate must be between 0% and 100%.");
                }
            }

            if (purchase.GrandTotal < 0)
            {
                result.AddError("Grand Total cannot be negative.");
            }

            if (purchase.AmountPaid < 0)
            {
                result.AddError("Amount Paid cannot be negative.");
            }

            return result;
        }

        public static ValidationResult ValidatePayment(SupplierPayment payment, decimal outstandingBalance)
        {
            var result = new ValidationResult();

            if (payment == null)
            {
                result.AddError("Payment record cannot be null.");
                return result;
            }

            if (payment.SupplierId <= 0)
            {
                result.AddError("Supplier must be selected.");
            }

            if (payment.Amount <= 0)
            {
                result.AddError("Payment amount must be greater than zero.");
            }

            if (payment.Amount > outstandingBalance && outstandingBalance > 0)
            {
                result.AddWarning($"Payment amount (₹{payment.Amount:N2}) exceeds current outstanding balance (₹{outstandingBalance:N2}). This will be recorded as an advance credit.");
            }

            return result;
        }

        public static bool ValidateEan13Checksum(string ean13)
        {
            if (string.IsNullOrWhiteSpace(ean13) || ean13.Length != 13 || !Regex.IsMatch(ean13, @"^\d{13}$"))
                return false;

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int digit = ean13[i] - '0';
                sum += (i % 2 == 0) ? digit : digit * 3;
            }

            int checkDigit = (10 - (sum % 10)) % 10;
            return checkDigit == (ean13[12] - '0');
        }

        public static string GenerateEan13WithChecksum(string prefix12)
        {
            if (string.IsNullOrWhiteSpace(prefix12)) prefix12 = "890" + DateTime.UtcNow.Ticks.ToString().Substring(0, 9);
            prefix12 = Regex.Replace(prefix12, @"\D", "");
            if (prefix12.Length > 12) prefix12 = prefix12.Substring(0, 12);
            while (prefix12.Length < 12) prefix12 = prefix12.PadRight(12, '0');

            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int digit = prefix12[i] - '0';
                sum += (i % 2 == 0) ? digit : digit * 3;
            }
            int checkDigit = (10 - (sum % 10)) % 10;
            return prefix12 + checkDigit.ToString();
        }
    }
}
