using System;
using System.Collections.Generic;
using System.Linq;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Services
{
    public class StockService
    {
        private readonly IStockRepository _stockRepo;
        private readonly IProductRepository _productRepo;
        private readonly IAuditLogRepository _auditRepo;

        public StockService(
            IStockRepository stockRepo,
            IProductRepository productRepo,
            IAuditLogRepository auditRepo)
        {
            _stockRepo = stockRepo;
            _productRepo = productRepo;
            _auditRepo = auditRepo;
        }

        public StockDashboardMetrics GetDashboardMetrics()
        {
            return _stockRepo.GetDashboardMetrics();
        }

        public PagedResult<CurrentStockSummary> GetCurrentStock(StockFilter filter)
        {
            return _stockRepo.GetCurrentStock(filter);
        }

        public IEnumerable<StockValuationSummary> GetStockValuation(long? categoryId = null)
        {
            return _stockRepo.GetStockValuation(categoryId);
        }

        public PagedResult<StockMovement> GetMovements(StockMovementFilter filter)
        {
            return _stockRepo.GetMovements(filter);
        }

        public string GenerateNextAdjustmentNumber()
        {
            return _stockRepo.GenerateNextAdjustmentNumber();
        }

        public string GenerateNextVerificationNumber()
        {
            return _stockRepo.GenerateNextVerificationNumber();
        }

        public ValidationResult ValidateAdjustment(StockAdjustment adj)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(adj.AdjustmentNumber))
            {
                result.AddError("Adjustment Number is required.");
            }

            if (string.IsNullOrWhiteSpace(adj.Reason))
            {
                result.AddError("A valid Reason is required for recording stock adjustments.");
            }

            if (adj.Items == null || adj.Items.Count == 0)
            {
                result.AddError("At least one product item is required for stock adjustment.");
                return result;
            }

            foreach (var item in adj.Items)
            {
                if (item.ProductId <= 0)
                {
                    result.AddError("Valid Product must be selected for all items.");
                }

                if (item.DifferenceQuantity == 0 && item.PhysicalCount == item.CurrentStock)
                {
                    result.AddError($"Product '{item.ProductName ?? item.ProductCode}': Difference quantity cannot be zero.");
                }

                if (string.IsNullOrWhiteSpace(item.AdjustmentType))
                {
                    result.AddError($"Product '{item.ProductName ?? item.ProductCode}': Adjustment type must be specified.");
                }

                // Check negative stock violation
                if (item.DifferenceQuantity < 0)
                {
                    decimal resultingStock = item.CurrentStock + item.DifferenceQuantity;
                    if (resultingStock < 0)
                    {
                        result.AddError($"Product '{item.ProductName ?? item.ProductCode}': Adjustment of {item.DifferenceQuantity} would cause negative stock ({resultingStock}). Operation rejected.");
                    }
                }
            }

            return result;
        }

        public ValidationResult SaveAdjustmentDraft(StockAdjustment adj)
        {
            var val = ValidateAdjustment(adj);
            if (!val.IsValid) return val;

            if (adj.Id == 0)
            {
                adj.Id = _stockRepo.CreateAdjustmentDraft(adj);
                _auditRepo?.Log("StockAdjustment", adj.AdjustmentNumber, "INSERT", $"Created draft adjustment with {adj.Items.Count} items");
            }
            else
            {
                _stockRepo.UpdateAdjustmentDraft(adj);
                _auditRepo?.Log("StockAdjustment", adj.AdjustmentNumber, "UPDATE", $"Updated draft adjustment with {adj.Items.Count} items");
            }

            return val;
        }

        public bool ConfirmAdjustment(long adjustmentId, out string errorMessage)
        {
            bool success = _stockRepo.ConfirmAdjustment(adjustmentId, out errorMessage);
            if (success)
            {
                var adj = _stockRepo.GetAdjustmentById(adjustmentId);
                _auditRepo?.Log("StockAdjustment", adj?.AdjustmentNumber ?? adjustmentId.ToString(), "CONFIRM", $"Confirmed stock adjustment. Stock updated for {adj?.Items?.Count ?? 0} products");
            }
            return success;
        }

        public bool CancelAdjustment(long adjustmentId, out string errorMessage)
        {
            bool success = _stockRepo.CancelAdjustment(adjustmentId, out errorMessage);
            if (success)
            {
                var adj = _stockRepo.GetAdjustmentById(adjustmentId);
                _auditRepo?.Log("StockAdjustment", adj?.AdjustmentNumber ?? adjustmentId.ToString(), "CANCEL", "Reverted stock adjustment and restored stock");
            }
            return success;
        }

        public StockAdjustment GetAdjustmentById(long id)
        {
            return _stockRepo.GetAdjustmentById(id);
        }

        public PagedResult<StockAdjustment> SearchAdjustments(StockAdjustmentFilter filter)
        {
            return _stockRepo.SearchAdjustments(filter);
        }

        public long CreatePhysicalVerificationDraft(PhysicalStockVerification verification)
        {
            long id = _stockRepo.CreatePhysicalVerificationDraft(verification);
            _auditRepo?.Log("PhysicalVerification", verification.VerificationNumber, "INSERT", $"Created stock verification count sheet with {verification.Items.Count} products");
            return id;
        }

        public bool ConfirmPhysicalVerification(long verificationId, out string errorMessage)
        {
            bool success = _stockRepo.ConfirmPhysicalVerification(verificationId, out errorMessage);
            if (success)
            {
                var ver = _stockRepo.GetPhysicalVerificationById(verificationId);
                _auditRepo?.Log("PhysicalVerification", ver?.VerificationNumber ?? verificationId.ToString(), "CONFIRM", $"Applied physical verification reconciliation for {ver?.TotalDiscrepancies ?? 0} discrepancies");
            }
            return success;
        }

        public PhysicalStockVerification GetPhysicalVerificationById(long id)
        {
            return _stockRepo.GetPhysicalVerificationById(id);
        }
    }
}
