using System;
using System.Collections.Generic;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;

namespace ShopBilling.Core.Services
{
    public class ProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public ProductService(IProductRepository productRepository, IAuditLogRepository auditLogRepository)
        {
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _auditLogRepository = auditLogRepository;
        }

        public PagedResult<Product> GetProducts(ProductFilter filter)
        {
            if (filter == null) filter = new ProductFilter();
            return _productRepository.Search(filter);
        }

        public Product GetProductById(long id)
        {
            return _productRepository.GetById(id);
        }

        public Product GetProductByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            return _productRepository.GetByBarcode(barcode.Trim());
        }

        public ValidationResult SaveProduct(Product product)
        {
            var valResult = ValidationService.ValidateProduct(product, product.Id == 0);
            if (!valResult.IsValid)
            {
                return valResult;
            }

            // Uniqueness validation
            if (_productRepository.ExistsProductCode(product.ProductCode, product.Id == 0 ? (long?)null : product.Id))
            {
                valResult.AddError($"Product Code '{product.ProductCode}' is already registered to another item.");
                return valResult;
            }

            if (_productRepository.ExistsBarcode(product.Barcode, product.Id == 0 ? (long?)null : product.Id))
            {
                valResult.AddError($"Barcode '{product.Barcode}' is already assigned to another item.");
                return valResult;
            }

            try
            {
                if (product.Id == 0)
                {
                    product.CreatedAt = DateTime.UtcNow;
                    product.UpdatedAt = DateTime.UtcNow;
                    product.CurrentStock = product.OpeningStock;
                    long newId = _productRepository.Insert(product);
                    product.Id = newId;

                    _auditLogRepository?.Log("Product", newId.ToString(), "INSERT",
                        $"Created product '{product.NameEn}' (Code: {product.ProductCode}, Barcode: {product.Barcode})");
                    AppLogger.Info($"Created product #{newId} '{product.NameEn}'");
                }
                else
                {
                    product.UpdatedAt = DateTime.UtcNow;
                    bool updated = _productRepository.Update(product);
                    if (!updated)
                    {
                        valResult.AddError("Failed to update product. It may have been modified or deleted concurrently.");
                        return valResult;
                    }

                    _auditLogRepository?.Log("Product", product.Id.ToString(), "UPDATE",
                        $"Updated product '{product.NameEn}' (Price: ₹{product.SaleRate}, Stock: {product.CurrentStock})");
                    AppLogger.Info($"Updated product #{product.Id} '{product.NameEn}'");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error saving product {product.ProductCode}", ex);
                valResult.AddError($"Database Error: {ex.Message}");
            }

            return valResult;
        }

        public bool DeactivateProduct(long id)
        {
            var p = _productRepository.GetById(id);
            if (p == null) return false;

            bool success = _productRepository.Deactivate(id);
            if (success)
            {
                _auditLogRepository?.Log("Product", id.ToString(), "DEACTIVATE", $"Deactivated product '{p.NameEn}'");
                AppLogger.Info($"Deactivated product #{id}");
            }
            return success;
        }

        public bool ActivateProduct(long id)
        {
            var p = _productRepository.GetById(id);
            if (p == null) return false;

            bool success = _productRepository.Activate(id);
            if (success)
            {
                _auditLogRepository?.Log("Product", id.ToString(), "ACTIVATE", $"Re-activated product '{p.NameEn}'");
                AppLogger.Info($"Re-activated product #{id}");
            }
            return success;
        }

        public (int Total, int LowStock) GetStockMetrics()
        {
            return (_productRepository.GetTotalCount(), _productRepository.GetLowStockCount());
        }
    }
}
