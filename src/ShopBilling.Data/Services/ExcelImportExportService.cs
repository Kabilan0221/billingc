using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using ShopBilling.Core.Interfaces;
using ShopBilling.Core.Models;
using ShopBilling.Core.Services;

namespace ShopBilling.Data.Services
{
    public class ExcelImportExportService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IBrandRepository _brandRepository;
        private readonly IAuditLogRepository _auditLogRepository;

        public ExcelImportExportService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IBrandRepository brandRepository,
            IAuditLogRepository auditLogRepository)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _brandRepository = brandRepository;
            _auditLogRepository = auditLogRepository;
        }

        public ExcelImportResult ImportProductsFromExcel(string filePath, Action<int, int> progressCallback = null, bool updateExisting = false)
        {
            var sw = Stopwatch.StartNew();
            var result = new ExcelImportResult();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var categoryCache = _categoryRepository.GetAll().ToDictionary(c => c.Name.Trim().ToLower(), c => c.Id);
            var brandCache = _brandRepository.GetAll().ToDictionary(b => b.Name.Trim().ToLower(), b => b.Id);

            var productsToInsert = new List<Product>();
            var seenBarcodesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenCodesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null) throw new InvalidOperationException("Workbook contains no worksheets.");

                var rows = worksheet.RangeUsed().RowsUsed().Skip(1); // Skip header row
                var rowList = rows.ToList();
                result.TotalRows = rowList.Count;

                int currentRowIndex = 1;
                foreach (var row in rowList)
                {
                    currentRowIndex++;
                    var importRow = new ExcelImportRow { RowIndex = currentRowIndex };

                    try
                    {
                        importRow.ProductCode = row.Cell(1).GetString()?.Trim();
                        importRow.Barcode = row.Cell(2).GetString()?.Trim();
                        importRow.NameEn = row.Cell(3).GetString()?.Trim();
                        importRow.NameTa = row.Cell(4).GetString()?.Trim();
                        importRow.Category = row.Cell(5).GetString()?.Trim();
                        importRow.Subcategory = row.Cell(6).GetString()?.Trim();
                        importRow.Brand = row.Cell(7).GetString()?.Trim();
                        importRow.Unit = row.Cell(8).GetString()?.Trim() ?? "PCS";
                        importRow.HsnCode = row.Cell(9).GetString()?.Trim();

                        decimal purRate, saleRate, wsRate, mrp, taxRate, opStock, reorder;
                        decimal.TryParse(row.Cell(10).GetString(), out purRate);
                        decimal.TryParse(row.Cell(11).GetString(), out saleRate);
                        decimal.TryParse(row.Cell(12).GetString(), out wsRate);
                        decimal.TryParse(row.Cell(13).GetString(), out mrp);
                        decimal.TryParse(row.Cell(14).GetString(), out taxRate);
                        decimal.TryParse(row.Cell(15).GetString(), out opStock);
                        decimal.TryParse(row.Cell(16).GetString(), out reorder);

                        importRow.PurchaseRate = purRate;
                        importRow.SaleRate = saleRate;
                        importRow.WholesaleRate = wsRate;
                        importRow.Mrp = mrp;
                        importRow.TaxRate = taxRate;
                        importRow.OpeningStock = opStock;
                        importRow.ReorderLevel = reorder;

                        // Validation checks
                        if (string.IsNullOrWhiteSpace(importRow.NameEn))
                            importRow.ValidationErrors.Add("Product Name (EN) is required.");

                        if (string.IsNullOrWhiteSpace(importRow.ProductCode))
                            importRow.ValidationErrors.Add("Product Code is required.");

                        if (string.IsNullOrWhiteSpace(importRow.Barcode))
                            importRow.ValidationErrors.Add("Barcode is required.");

                        if (importRow.Mrp > 0 && importRow.SaleRate > importRow.Mrp)
                            importRow.ValidationErrors.Add($"Sale rate ({importRow.SaleRate}) exceeds MRP ({importRow.Mrp}).");

                        // Intra-batch duplicate check
                        if (!string.IsNullOrWhiteSpace(importRow.Barcode))
                        {
                            if (seenBarcodesInBatch.Contains(importRow.Barcode))
                                importRow.ValidationErrors.Add($"Duplicate barcode '{importRow.Barcode}' in this import file.");
                            else
                                seenBarcodesInBatch.Add(importRow.Barcode);
                        }

                        if (!string.IsNullOrWhiteSpace(importRow.ProductCode))
                        {
                            if (seenCodesInBatch.Contains(importRow.ProductCode))
                                importRow.ValidationErrors.Add($"Duplicate product code '{importRow.ProductCode}' in this import file.");
                            else
                                seenCodesInBatch.Add(importRow.ProductCode);
                        }

                        if (!importRow.IsValid)
                        {
                            result.FailedRows.Add(importRow);
                            result.FailedCount++;
                            continue;
                        }

                        // Category lookup / dynamic insert
                        long? catId = null;
                        if (!string.IsNullOrWhiteSpace(importRow.Category))
                        {
                            var catKey = importRow.Category.ToLower();
                            if (categoryCache.ContainsKey(catKey)) catId = categoryCache[catKey];
                            else
                            {
                                var newCat = new Category { Name = importRow.Category, Code = "CAT-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() };
                                catId = _categoryRepository.Insert(newCat);
                                categoryCache[catKey] = catId.Value;
                            }
                        }

                        // Brand lookup / dynamic insert
                        long? brandId = null;
                        if (!string.IsNullOrWhiteSpace(importRow.Brand))
                        {
                            var brandKey = importRow.Brand.ToLower();
                            if (brandCache.ContainsKey(brandKey)) brandId = brandCache[brandKey];
                            else
                            {
                                var newBrand = new Brand { Name = importRow.Brand, Code = "BR-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper() };
                                brandId = _brandRepository.Insert(newBrand);
                                brandCache[brandKey] = brandId.Value;
                            }
                        }

                        var prod = new Product
                        {
                            ProductCode = importRow.ProductCode,
                            Barcode = importRow.Barcode,
                            NameEn = importRow.NameEn,
                            NameTa = importRow.NameTa,
                            CategoryId = catId,
                            BrandId = brandId,
                            Unit = string.IsNullOrWhiteSpace(importRow.Unit) ? "PCS" : importRow.Unit.ToUpper(),
                            HsnCode = importRow.HsnCode,
                            PurchaseRate = importRow.PurchaseRate,
                            SaleRate = importRow.SaleRate,
                            WholesaleRate = importRow.WholesaleRate,
                            Mrp = importRow.Mrp,
                            TaxRate = importRow.TaxRate,
                            OpeningStock = importRow.OpeningStock,
                            CurrentStock = importRow.OpeningStock,
                            ReorderLevel = importRow.ReorderLevel > 0 ? importRow.ReorderLevel : 5m,
                            IsActive = true
                        };

                        productsToInsert.Add(prod);

                        // Batch chunk insert every 1000 items to maintain low RAM and smooth progress
                        if (productsToInsert.Count >= 1000)
                        {
                            int inserted = _productRepository.BulkInsert(productsToInsert, updateExisting);
                            result.SuccessfullyImported += inserted;
                            productsToInsert.Clear();
                            progressCallback?.Invoke(result.SuccessfullyImported + result.FailedCount, result.TotalRows);
                        }
                    }
                    catch (Exception ex)
                    {
                        importRow.ValidationErrors.Add($"Parse Error: {ex.Message}");
                        result.FailedRows.Add(importRow);
                        result.FailedCount++;
                    }
                }

                // Insert remaining buffer
                if (productsToInsert.Count > 0)
                {
                    int inserted = _productRepository.BulkInsert(productsToInsert, updateExisting);
                    result.SuccessfullyImported += inserted;
                    productsToInsert.Clear();
                    progressCallback?.Invoke(result.SuccessfullyImported + result.FailedCount, result.TotalRows);
                }
            }

            sw.Stop();
            result.DurationMs = sw.Elapsed.TotalMilliseconds;

            _auditLogRepository?.Log("Excel", "BulkImport", "IMPORT",
                $"Imported {result.SuccessfullyImported} products, {result.FailedCount} errors in {result.DurationMs:F0}ms");
            AppLogger.Info($"Excel import finished: {result.SuccessfullyImported} success, {result.FailedCount} failed in {result.DurationMs:F0}ms");

            return result;
        }

        public void ExportProductsToExcel(string destinationPath, IEnumerable<Product> products)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Products");

                // Headers
                string[] headers = new string[]
                {
                    "Product Code", "Barcode", "Product Name (English)", "Product Name (Tamil)",
                    "Category", "Subcategory", "Brand", "Unit", "HSN Code",
                    "Purchase Rate", "Sale Rate", "Wholesale Rate", "MRP", "Tax %",
                    "Opening Stock", "Current Stock", "Reorder Level", "Status"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2B579A");
                    cell.Style.Font.FontColor = XLColor.White;
                }

                int row = 2;
                foreach (var p in products)
                {
                    ws.Cell(row, 1).Value = p.ProductCode;
                    ws.Cell(row, 2).Value = "'" + p.Barcode; // Leading apostrophe to preserve barcode string
                    ws.Cell(row, 3).Value = p.NameEn;
                    ws.Cell(row, 4).Value = p.NameTa ?? "";
                    ws.Cell(row, 5).Value = p.CategoryName ?? "";
                    ws.Cell(row, 6).Value = p.SubcategoryName ?? "";
                    ws.Cell(row, 7).Value = p.BrandName ?? "";
                    ws.Cell(row, 8).Value = p.Unit;
                    ws.Cell(row, 9).Value = p.HsnCode ?? "";
                    ws.Cell(row, 10).Value = p.PurchaseRate;
                    ws.Cell(row, 11).Value = p.SaleRate;
                    ws.Cell(row, 12).Value = p.WholesaleRate;
                    ws.Cell(row, 13).Value = p.Mrp;
                    ws.Cell(row, 14).Value = p.TaxRate;
                    ws.Cell(row, 15).Value = p.OpeningStock;
                    ws.Cell(row, 16).Value = p.CurrentStock;
                    ws.Cell(row, 17).Value = p.ReorderLevel;
                    ws.Cell(row, 18).Value = p.IsActive ? "Active" : "Inactive";
                    row++;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(destinationPath);
            }

            AppLogger.Info($"Exported products to Excel: {destinationPath}");
        }

        public void GenerateSampleTemplate(string destinationPath)
        {
            var sampleProducts = new List<Product>
            {
                new Product { ProductCode = "PRD0001", Barcode = "8901030012345", NameEn = "Ponni Boiled Rice 5kg", NameTa = "பொன்னி புழுங்கல் அரிசி 5கிகி", CategoryName = "Groceries & Staples", BrandName = "Tata Consumer", Unit = "BAG", HsnCode = "1006", PurchaseRate = 280, SaleRate = 320, WholesaleRate = 305, Mrp = 340, TaxRate = 0, OpeningStock = 100, ReorderLevel = 10 },
                new Product { ProductCode = "PRD0002", Barcode = "8901030012346", NameEn = "Aachi Chilli Powder 100g", NameTa = "ஆச்சி மிளகாய் தூள் 100கி", CategoryName = "Groceries & Staples", BrandName = "Aachi Masala", Unit = "PACK", HsnCode = "0904", PurchaseRate = 32, SaleRate = 38, WholesaleRate = 35, Mrp = 40, TaxRate = 5, OpeningStock = 250, ReorderLevel = 25 },
                new Product { ProductCode = "PRD0003", Barcode = "8901030012347", NameEn = "Tata Salt Iodized 1kg", NameTa = "டாடா அயோடின் உப்பு 1கிகி", CategoryName = "Groceries & Staples", BrandName = "Tata Consumer", Unit = "PACK", HsnCode = "2501", PurchaseRate = 22, SaleRate = 28, WholesaleRate = 25, Mrp = 28, TaxRate = 0, OpeningStock = 500, ReorderLevel = 50 },
                new Product { ProductCode = "PRD0004", Barcode = "8901030012348", NameEn = "Britannia Good Day Butter 200g", NameTa = "பிரிட்டானியா குட் டே பிஸ்கட் 200கி", CategoryName = "Snacks & Confectionery", BrandName = "Britannia", Unit = "PACK", HsnCode = "1905", PurchaseRate = 40, SaleRate = 48, WholesaleRate = 44, Mrp = 50, TaxRate = 18, OpeningStock = 120, ReorderLevel = 15 }
            };

            ExportProductsToExcel(destinationPath, sampleProducts);
        }
    }
}
