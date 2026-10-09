using System;
using System.Collections.Generic;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Services
{
    public class SupplierPaymentService
    {
        private readonly ISupplierPaymentRepository _paymentRepo;
        private readonly ISupplierRepository _supplierRepo;
        private readonly IAuditLogRepository _auditRepo;

        public SupplierPaymentService(
            ISupplierPaymentRepository paymentRepo,
            ISupplierRepository supplierRepo,
            IAuditLogRepository auditRepo)
        {
            _paymentRepo = paymentRepo ?? throw new ArgumentNullException(nameof(paymentRepo));
            _supplierRepo = supplierRepo;
            _auditRepo = auditRepo;
        }

        public ValidationResult RecordPayment(SupplierPayment payment)
        {
            var supplier = _supplierRepo.GetById(payment.SupplierId);
            decimal outstanding = supplier != null ? supplier.CurrentBalance : 0;

            var valResult = ValidationService.ValidatePayment(payment, outstanding);
            if (!valResult.IsValid) return valResult;

            try
            {
                if (string.IsNullOrWhiteSpace(payment.PaymentNumber))
                {
                    payment.PaymentNumber = _paymentRepo.GenerateNextPaymentNumber();
                }

                long id = _paymentRepo.InsertPayment(payment);
                payment.Id = id;

                _auditRepo?.Log("SupplierPayment", id.ToString(), "INSERT",
                    $"Recorded payment '{payment.PaymentNumber}' of ₹{payment.Amount:N2} to supplier '{supplier?.Name ?? "Unknown"}' ({payment.PaymentMethod})");
                AppLogger.Info($"Recorded supplier payment #{payment.PaymentNumber} (₹{payment.Amount:N2})");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error recording supplier payment {payment.PaymentNumber}", ex);
                valResult.AddError($"Database Error: {ex.Message}");
            }

            return valResult;
        }

        public IEnumerable<SupplierPayment> GetPaymentsBySupplier(long supplierId)
        {
            return _paymentRepo.GetBySupplierId(supplierId);
        }

        public IEnumerable<SupplierLedgerEntry> GetSupplierLedger(long supplierId)
        {
            return _paymentRepo.GetLedger(supplierId);
        }
    }
}
