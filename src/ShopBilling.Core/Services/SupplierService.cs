using System;
using System.Collections.Generic;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Services
{
    public class SupplierService
    {
        private readonly ISupplierRepository _supplierRepo;
        private readonly IAuditLogRepository _auditRepo;

        public SupplierService(ISupplierRepository supplierRepo, IAuditLogRepository auditRepo)
        {
            _supplierRepo = supplierRepo ?? throw new ArgumentNullException(nameof(supplierRepo));
            _auditRepo = auditRepo;
        }

        public PagedResult<Supplier> Search(SupplierFilter filter)
        {
            if (filter == null) filter = new SupplierFilter();
            return _supplierRepo.Search(filter);
        }

        public Supplier GetById(long id)
        {
            return _supplierRepo.GetById(id);
        }

        public IEnumerable<Supplier> GetAllActive()
        {
            return _supplierRepo.GetAllActive();
        }

        public ValidationResult SaveSupplier(Supplier supplier)
        {
            var valResult = ValidationService.ValidateSupplier(supplier, supplier.Id == 0);
            if (!valResult.IsValid)
            {
                return valResult;
            }

            // Uniqueness check for Supplier Code
            if (_supplierRepo.ExistsCode(supplier.Code, supplier.Id == 0 ? (long?)null : supplier.Id))
            {
                valResult.AddError($"Supplier Code '{supplier.Code}' is already registered to another supplier.");
                return valResult;
            }

            try
            {
                if (supplier.Id == 0)
                {
                    supplier.CurrentBalance = supplier.OpeningBalance;
                    supplier.CreatedAt = DateTime.UtcNow;
                    supplier.UpdatedAt = DateTime.UtcNow;
                    long newId = _supplierRepo.Insert(supplier);
                    supplier.Id = newId;

                    _auditRepo?.Log("Supplier", newId.ToString(), "INSERT",
                        $"Registered supplier '{supplier.Name}' (Code: {supplier.Code}, GSTIN: {supplier.Gstin ?? "N/A"})");
                    AppLogger.Info($"Registered supplier #{newId} '{supplier.Name}'");
                }
                else
                {
                    supplier.UpdatedAt = DateTime.UtcNow;
                    bool updated = _supplierRepo.Update(supplier);
                    if (!updated)
                    {
                        valResult.AddError("Failed to update supplier record.");
                        return valResult;
                    }

                    _auditRepo?.Log("Supplier", supplier.Id.ToString(), "UPDATE",
                        $"Updated supplier '{supplier.Name}' (Phone: {supplier.Mobile})");
                    AppLogger.Info($"Updated supplier #{supplier.Id} '{supplier.Name}'");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error saving supplier {supplier.Code}", ex);
                valResult.AddError($"Database Error: {ex.Message}");
            }

            return valResult;
        }

        public bool DeactivateSupplier(long id, out string errorMessage)
        {
            errorMessage = null;
            var s = _supplierRepo.GetById(id);
            if (s == null)
            {
                errorMessage = "Supplier not found.";
                return false;
            }

            // Phase 2 Requirement: Do not permanently delete a supplier who has purchase or payment transactions. Allow deactivation instead.
            bool success = _supplierRepo.Deactivate(id);
            if (success)
            {
                _auditRepo?.Log("Supplier", id.ToString(), "DEACTIVATE", $"Deactivated supplier '{s.Name}'");
                AppLogger.Info($"Deactivated supplier #{id} '{s.Name}'");
            }
            return success;
        }

        public bool ActivateSupplier(long id)
        {
            var s = _supplierRepo.GetById(id);
            if (s == null) return false;

            bool success = _supplierRepo.Activate(id);
            if (success)
            {
                _auditRepo?.Log("Supplier", id.ToString(), "ACTIVATE", $"Re-activated supplier '{s.Name}'");
                AppLogger.Info($"Re-activated supplier #{id} '{s.Name}'");
            }
            return success;
        }
    }
}
