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
    public class SupplierExcelImportRow
    {
        public int RowIndex { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string Gstin { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public decimal OpeningBalance { get; set; }
        public string PaymentTerms { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => ValidationErrors.Count == 0;
    }

    public class SupplierExcelImportResult
    {
        public int TotalRows { get; set; }
        public int SuccessfullyImported { get; set; }
        public int FailedCount { get; set; }
        public double DurationMs { get; set; }
        public List<SupplierExcelImportRow> FailedRows { get; set; } = new List<SupplierExcelImportRow>();
    }

    public class SupplierExcelService
    {
        private readonly ISupplierRepository _supplierRepo;
        private readonly IAuditLogRepository _auditRepo;

        public SupplierExcelService(ISupplierRepository supplierRepo, IAuditLogRepository auditRepo)
        {
            _supplierRepo = supplierRepo;
            _auditRepo = auditRepo;
        }

        public SupplierExcelImportResult ImportSuppliersFromExcel(string filePath, Action<int, int> progressCallback = null, bool updateExisting = false)
        {
            var sw = Stopwatch.StartNew();
            var result = new SupplierExcelImportResult();

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Excel file not found.", filePath);
            }

            var suppliersToInsert = new List<Supplier>();
            var seenCodesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var workbook = new XLWorkbook(filePath))
            {
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null) throw new InvalidOperationException("Workbook contains no worksheets.");

                var rows = ws.RangeUsed().RowsUsed().Skip(1).ToList();
                result.TotalRows = rows.Count;

                int currentRow = 1;
                foreach (var row in rows)
                {
                    currentRow++;
                    var importRow = new SupplierExcelImportRow { RowIndex = currentRow };

                    try
                    {
                        importRow.Code = row.Cell(1).GetString()?.Trim();
                        importRow.Name = row.Cell(2).GetString()?.Trim();
                        importRow.ContactPerson = row.Cell(3).GetString()?.Trim();
                        importRow.Mobile = row.Cell(4).GetString()?.Trim();
                        importRow.Email = row.Cell(5).GetString()?.Trim();
                        importRow.Gstin = row.Cell(6).GetString()?.Trim();
                        importRow.City = row.Cell(7).GetString()?.Trim();
                        importRow.State = row.Cell(8).GetString()?.Trim() ?? "Tamil Nadu";

                        decimal opBal;
                        decimal.TryParse(row.Cell(9).GetString(), out opBal);
                        importRow.OpeningBalance = opBal;
                        importRow.PaymentTerms = row.Cell(10).GetString()?.Trim();

                        if (string.IsNullOrWhiteSpace(importRow.Code))
                            importRow.ValidationErrors.Add("Supplier Code is required.");

                        if (string.IsNullOrWhiteSpace(importRow.Name))
                            importRow.ValidationErrors.Add("Supplier Name is required.");

                        if (!string.IsNullOrWhiteSpace(importRow.Code))
                        {
                            if (seenCodesInBatch.Contains(importRow.Code))
                                importRow.ValidationErrors.Add($"Duplicate supplier code '{importRow.Code}' within import sheet.");
                            else
                                seenCodesInBatch.Add(importRow.Code);
                        }

                        var tempSupplier = new Supplier
                        {
                            Code = importRow.Code,
                            Name = importRow.Name,
                            ContactPerson = importRow.ContactPerson,
                            Mobile = importRow.Mobile,
                            Email = importRow.Email,
                            Gstin = importRow.Gstin,
                            City = importRow.City,
                            State = importRow.State,
                            OpeningBalance = importRow.OpeningBalance,
                            CurrentBalance = importRow.OpeningBalance,
                            PaymentTerms = importRow.PaymentTerms,
                            IsActive = true
                        };

                        var valRes = ValidationService.ValidateSupplier(tempSupplier);
                        if (!valRes.IsValid)
                        {
                            importRow.ValidationErrors.AddRange(valRes.Errors);
                        }

                        if (!importRow.IsValid)
                        {
                            result.FailedRows.Add(importRow);
                            result.FailedCount++;
                            continue;
                        }

                        suppliersToInsert.Add(tempSupplier);

                        if (suppliersToInsert.Count >= 500)
                        {
                            int inserted = _supplierRepo.BulkInsert(suppliersToInsert, updateExisting);
                            result.SuccessfullyImported += inserted;
                            suppliersToInsert.Clear();
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

                if (suppliersToInsert.Count > 0)
                {
                    int inserted = _supplierRepo.BulkInsert(suppliersToInsert, updateExisting);
                    result.SuccessfullyImported += inserted;
                    suppliersToInsert.Clear();
                    progressCallback?.Invoke(result.SuccessfullyImported + result.FailedCount, result.TotalRows);
                }
            }

            sw.Stop();
            result.DurationMs = sw.Elapsed.TotalMilliseconds;

            _auditRepo?.Log("SupplierExcel", "BulkImport", "IMPORT",
                $"Imported {result.SuccessfullyImported} suppliers, {result.FailedCount} errors in {result.DurationMs:F0}ms");
            AppLogger.Info($"Supplier Excel import complete: {result.SuccessfullyImported} imported in {result.DurationMs:F0}ms");

            return result;
        }

        public void ExportSuppliersToExcel(string destinationPath, IEnumerable<Supplier> suppliers)
        {
            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Suppliers");
                string[] headers = {
                    "Supplier Code", "Supplier Name", "Contact Person", "Mobile", "Email",
                    "GSTIN", "City", "State", "Opening Balance", "Current Balance", "Payment Terms", "Status"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                    cell.Style.Font.FontColor = XLColor.White;
                }

                int r = 2;
                foreach (var s in suppliers)
                {
                    ws.Cell(r, 1).Value = s.Code;
                    ws.Cell(r, 2).Value = s.Name;
                    ws.Cell(r, 3).Value = s.ContactPerson ?? "";
                    ws.Cell(r, 4).Value = "'" + (s.Mobile ?? "");
                    ws.Cell(r, 5).Value = s.Email ?? "";
                    ws.Cell(r, 6).Value = s.Gstin ?? "";
                    ws.Cell(r, 7).Value = s.City ?? "";
                    ws.Cell(r, 8).Value = s.State ?? "";
                    ws.Cell(r, 9).Value = s.OpeningBalance;
                    ws.Cell(r, 10).Value = s.CurrentBalance;
                    ws.Cell(r, 11).Value = s.PaymentTerms ?? "";
                    ws.Cell(r, 12).Value = s.IsActive ? "Active" : "Inactive";
                    r++;
                }

                ws.Columns().AdjustToContents();
                wb.SaveAs(destinationPath);
            }
            AppLogger.Info($"Exported suppliers to Excel: {destinationPath}");
        }

        public void GenerateSampleTemplate(string destinationPath)
        {
            var samples = new List<Supplier>
            {
                new Supplier { Code = "SUP001", Name = "Sri Meenakshi Trading Co", ContactPerson = "K. Ramasamy", Mobile = "9840123456", Email = "sales@meenakshitrading.com", Gstin = "33AABCS1429B1Z1", City = "Madurai", State = "Tamil Nadu", OpeningBalance = 5000, PaymentTerms = "30 Days Net", IsActive = true },
                new Supplier { Code = "SUP002", Name = "Aachi Masala Foods Pvt Ltd", ContactPerson = "M. Saravanan", Mobile = "9841234567", Email = "distributors@aachigroup.com", Gstin = "33AAACA2355F1ZY", City = "Chennai", State = "Tamil Nadu", OpeningBalance = 0, PaymentTerms = "15 Days Net", IsActive = true }
            };
            ExportSuppliersToExcel(destinationPath, samples);
        }
    }
}
