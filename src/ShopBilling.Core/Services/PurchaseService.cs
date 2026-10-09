using System;
using System.Collections.Generic;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Services
{
    public class PurchaseService
    {
        private readonly IPurchaseRepository _purchaseRepo;
        private readonly IProductRepository _productRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly IAuditLogRepository _auditRepo;

        public PurchaseService(
            IPurchaseRepository purchaseRepo,
            IProductRepository productRepo,
            ISupplierRepository supplierRepo,
            IAuditLogRepository auditRepo)
        {
            _purchaseRepo = purchaseRepo ?? throw new ArgumentNullException(nameof(purchaseRepo));
            _productRepo = productRepo;
            _supplierRepo = supplierRepo;
            _auditRepo = auditRepo;
        }

        public static void CalculatePurchase(PurchaseHeader p)
        {
            if (p == null) return;

            decimal totalQty = 0;
            decimal totalFreeQty = 0;
            decimal grossAmount = 0;
            decimal itemDiscountTotal = 0;
            decimal taxableAmountTotal = 0;
            decimal taxAmountTotal = 0;

            if (p.Items != null)
            {
                foreach (var item in p.Items)
                {
                    totalQty += item.Quantity;
                    totalFreeQty += item.FreeQuantity;

                    decimal lineGross = item.Quantity * item.PurchaseRate;
                    grossAmount += lineGross;

                    decimal lineDisc = item.DiscountAmount;
                    if (item.DiscountPercent > 0)
                    {
                        lineDisc = Math.Round(lineGross * (item.DiscountPercent / 100m), 2);
                        item.DiscountAmount = lineDisc;
                    }
                    itemDiscountTotal += lineDisc;

                    decimal lineTaxable = Math.Max(0, lineGross - lineDisc);
                    item.TaxableAmount = lineTaxable;
                    taxableAmountTotal += lineTaxable;

                    decimal lineTax = 0;
                    if (item.TaxRate > 0)
                    {
                        lineTax = Math.Round(lineTaxable * (item.TaxRate / 100m), 2);
                    }
                    item.TaxAmount = lineTax;
                    taxAmountTotal += lineTax;

                    item.LineTotal = lineTaxable + lineTax;
                }
            }

            p.TotalQty = totalQty;
            p.TotalFreeQty = totalFreeQty;
            p.GrossAmount = grossAmount;
            p.ItemDiscount = itemDiscountTotal;

            // Apply additional discount proportionally or directly
            decimal effectiveTaxable = Math.Max(0, taxableAmountTotal - p.AdditionalDiscount);
            p.TaxableAmount = effectiveTaxable;
            p.TaxAmount = taxAmountTotal;

            decimal netBeforeRound = effectiveTaxable + taxAmountTotal + p.AdditionalCharges;
            decimal rounded = Math.Round(netBeforeRound, 0, MidpointRounding.AwayFromZero);
            p.RoundOff = rounded - netBeforeRound;
            p.GrandTotal = rounded;

            p.BalanceDue = Math.Max(0, p.GrandTotal - p.AmountPaid);
        }

        public ValidationResult SaveDraft(PurchaseHeader purchase)
        {
            CalculatePurchase(purchase);
            var valResult = ValidationService.ValidatePurchase(purchase);
            if (!valResult.IsValid) return valResult;

            try
            {
                if (string.IsNullOrWhiteSpace(purchase.PurchaseNumber))
                {
                    purchase.PurchaseNumber = _purchaseRepo.GenerateNextPurchaseNumber();
                }

                if (purchase.Id == 0)
                {
                    purchase.Status = "Draft";
                    long id = _purchaseRepo.CreateDraft(purchase);
                    purchase.Id = id;
                    _auditRepo?.Log("Purchase", id.ToString(), "INSERT", $"Created draft purchase order '{purchase.PurchaseNumber}'");
                    AppLogger.Info($"Saved draft purchase #{purchase.PurchaseNumber}");
                }
                else
                {
                    bool updated = _purchaseRepo.UpdateDraft(purchase);
                    if (!updated)
                    {
                        valResult.AddError("Failed to update purchase draft.");
                        return valResult;
                    }
                    _auditRepo?.Log("Purchase", purchase.Id.ToString(), "UPDATE", $"Updated draft purchase '{purchase.PurchaseNumber}'");
                    AppLogger.Info($"Updated draft purchase #{purchase.PurchaseNumber}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error saving purchase {purchase.PurchaseNumber}", ex);
                valResult.AddError($"Database Error: {ex.Message}");
            }

            return valResult;
        }

        public bool ConfirmPurchase(long purchaseId, out string errorMessage)
        {
            errorMessage = null;
            bool success = _purchaseRepo.ConfirmPurchase(purchaseId, out errorMessage);
            if (success)
            {
                _auditRepo?.Log("Purchase", purchaseId.ToString(), "CONFIRM", $"Confirmed purchase and updated inventory stock safely.");
                AppLogger.Info($"Successfully confirmed purchase #{purchaseId}");
            }
            return success;
        }

        public bool CancelPurchase(long purchaseId, out string errorMessage)
        {
            errorMessage = null;
            bool success = _purchaseRepo.CancelPurchase(purchaseId, out errorMessage);
            if (success)
            {
                _auditRepo?.Log("Purchase", purchaseId.ToString(), "CANCEL", $"Reversed and cancelled purchase #{purchaseId}.");
                AppLogger.Info($"Successfully cancelled purchase #{purchaseId}");
            }
            return success;
        }

        public PurchaseHeader GetById(long id)
        {
            return _purchaseRepo.GetById(id);
        }

        public PagedResult<PurchaseHeader> Search(PurchaseFilter filter)
        {
            if (filter == null) filter = new PurchaseFilter();
            return _purchaseRepo.Search(filter);
        }
    }
}
