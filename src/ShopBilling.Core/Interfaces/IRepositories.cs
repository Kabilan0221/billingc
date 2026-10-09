using System.Collections.Generic;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Interfaces
{
    public interface IProductRepository
    {
        Product GetById(long id);
        Product GetByBarcode(string barcode);
        Product GetByProductCode(string code);
        PagedResult<Product> Search(ProductFilter filter);
        long Insert(Product product);
        bool Update(Product product);
        bool Deactivate(long id);
        bool Activate(long id);
        int BulkInsert(IEnumerable<Product> products, bool updateDuplicates = false);
        bool ExistsBarcode(string barcode, long? excludeId = null);
        bool ExistsProductCode(string code, long? excludeId = null);
        int GetTotalCount();
        int GetLowStockCount();
    }

    public interface ICategoryRepository
    {
        IEnumerable<Category> GetAll(bool includeInactive = false);
        Category GetById(long id);
        Category GetByName(string name);
        long Insert(Category category);
        bool Update(Category category);
        bool Delete(long id);
        
        IEnumerable<Subcategory> GetSubcategories(long categoryId, bool includeInactive = false);
        Subcategory GetSubcategoryById(long id);
        long InsertSubcategory(Subcategory subcategory);
        bool UpdateSubcategory(Subcategory subcategory);
    }

    public interface IBrandRepository
    {
        IEnumerable<Brand> GetAll(bool includeInactive = false);
        Brand GetById(long id);
        Brand GetByName(string name);
        long Insert(Brand brand);
        bool Update(Brand brand);
        bool Delete(long id);
    }

    public interface IAuditLogRepository
    {
        void Log(string entity, string entityId, string action, string details);
        IEnumerable<AuditLog> GetRecent(int limit = 100);
    }

    public interface ISupplierRepository
    {
        Supplier GetById(long id);
        Supplier GetByCode(string code);
        PagedResult<Supplier> Search(SupplierFilter filter);
        IEnumerable<Supplier> GetAllActive();
        long Insert(Supplier supplier);
        bool Update(Supplier supplier);
        bool Deactivate(long id);
        bool Activate(long id);
        bool ExistsCode(string code, long? excludeId = null);
        bool HasTransactions(long id);
        int BulkInsert(IEnumerable<Supplier> suppliers, bool updateDuplicates = false);
    }

    public interface IPurchaseRepository
    {
        PurchaseHeader GetById(long id);
        PurchaseHeader GetByNumber(string purchaseNumber);
        PagedResult<PurchaseHeader> Search(PurchaseFilter filter);
        long CreateDraft(PurchaseHeader purchase);
        bool UpdateDraft(PurchaseHeader purchase);
        bool ConfirmPurchase(long purchaseId, out string errorMessage);
        bool CancelPurchase(long purchaseId, out string errorMessage);
        string GenerateNextPurchaseNumber();
    }

    public interface ISupplierPaymentRepository
    {
        SupplierPayment GetById(long id);
        IEnumerable<SupplierPayment> GetBySupplierId(long supplierId);
        IEnumerable<SupplierPayment> GetByPurchaseId(long purchaseId);
        long InsertPayment(SupplierPayment payment);
        string GenerateNextPaymentNumber();
        IEnumerable<SupplierLedgerEntry> GetLedger(long supplierId);
    }

    public interface IStockMovementRepository
    {
        IEnumerable<StockMovement> GetByProductId(long productId, int limit = 50);
        IEnumerable<StockMovement> GetByReference(string referenceType, string referenceId);
    }

    public interface IStockRepository
    {
        StockDashboardMetrics GetDashboardMetrics();
        PagedResult<CurrentStockSummary> GetCurrentStock(StockFilter filter);
        IEnumerable<StockValuationSummary> GetStockValuation(long? categoryId = null);
        PagedResult<StockMovement> GetMovements(StockMovementFilter filter);
        long CreateAdjustmentDraft(StockAdjustment adjustment);
        bool UpdateAdjustmentDraft(StockAdjustment adjustment);
        bool ConfirmAdjustment(long adjustmentId, out string errorMessage);
        bool CancelAdjustment(long adjustmentId, out string errorMessage);
        StockAdjustment GetAdjustmentById(long id);
        PagedResult<StockAdjustment> SearchAdjustments(StockAdjustmentFilter filter);
        string GenerateNextAdjustmentNumber();
        long CreatePhysicalVerificationDraft(PhysicalStockVerification verification);
        bool ConfirmPhysicalVerification(long verificationId, out string errorMessage);
        PhysicalStockVerification GetPhysicalVerificationById(long id);
        string GenerateNextVerificationNumber();
    }
}
