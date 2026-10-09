import React, { useState } from 'react';
import { X, Upload, FileSpreadsheet, Download, AlertTriangle, CheckCircle2, Clock } from 'lucide-react';
import * as XLSX from 'xlsx';
import { db } from '../services/dbEngine';
import { Product } from '../types';

interface ExcelDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onImportComplete: () => void;
}

export const ExcelDialog: React.FC<ExcelDialogProps> = ({
  isOpen,
  onClose,
  onImportComplete,
}) => {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [parsedRows, setParsedRows] = useState<any[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const [progress, setProgress] = useState(0);
  const [importResult, setImportResult] = useState<{ imported: number; failed: number; errors: any[] } | null>(null);
  const [updateDuplicates, setUpdateDuplicates] = useState(false);

  if (!isOpen) return null;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setSelectedFile(file);
    setImportResult(null);

    const reader = new FileReader();
    reader.onload = (evt) => {
      try {
        const bstr = evt.target?.result;
        const wb = XLSX.read(bstr, { type: 'binary' });
        const wsName = wb.SheetNames[0];
        const ws = wb.Sheets[wsName];
        const data = XLSX.utils.sheet_to_json(ws, { defval: '' });
        setParsedRows(data);
      } catch (err: any) {
        alert(`Failed to parse Excel file: ${err.message}`);
      }
    };
    reader.readAsBinaryString(file);
  };

  const handleStartImport = async () => {
    if (parsedRows.length === 0) return;
    setIsProcessing(true);
    setProgress(0);

    const productsToImport: Partial<Product>[] = [];

    for (const row of parsedRows) {
      // Map possible column variations
      const code = (row['Product Code'] || row['product_code'] || row['Code'] || '').toString();
      const barcode = (row['Barcode'] || row['barcode'] || row['EAN'] || '').toString();
      const nameEn = (row['Product Name (English)'] || row['Product Name'] || row['Name'] || row['name_en'] || '').toString();
      const nameTa = (row['Product Name (Tamil)'] || row['Tamil Name'] || row['name_ta'] || '').toString();
      const category = (row['Category'] || row['category'] || '').toString();
      const brand = (row['Brand'] || row['brand'] || '').toString();
      const unit = (row['Unit'] || row['unit'] || 'PCS').toString();
      const hsn = (row['HSN Code'] || row['HSN'] || '').toString();

      const pur = parseFloat(row['Purchase Rate'] || row['purchase_rate'] || 0);
      const sale = parseFloat(row['Sale Rate'] || row['sale_rate'] || 0);
      const ws = parseFloat(row['Wholesale Rate'] || row['wholesale_rate'] || 0);
      const mrp = parseFloat(row['MRP'] || row['mrp'] || 0);
      const tax = parseFloat(row['Tax %'] || row['Tax'] || row['tax_rate'] || 0);
      const op = parseFloat(row['Opening Stock'] || row['opening_stock'] || 0);
      const reorder = parseFloat(row['Reorder Level'] || row['reorder_level'] || 5);

      // Auto resolve or create category / brand
      let catId: number | undefined;
      if (category) {
        const existingCat = db.getCategories().find((c) => c.name.toLowerCase() === category.toLowerCase());
        catId = existingCat ? existingCat.id : db.addCategory(category).id;
      }

      let brandId: number | undefined;
      if (brand) {
        const existingBrand = db.getBrands().find((b) => b.name.toLowerCase() === brand.toLowerCase());
        brandId = existingBrand ? existingBrand.id : db.addBrand(brand).id;
      }

      productsToImport.push({
        productCode: code,
        barcode,
        nameEn,
        nameTa,
        categoryId: catId,
        brandId,
        unit,
        hsnCode: hsn,
        purchaseRate: isNaN(pur) ? 0 : pur,
        saleRate: isNaN(sale) ? 0 : sale,
        wholesaleRate: isNaN(ws) ? 0 : ws,
        mrp: isNaN(mrp) ? 0 : mrp,
        taxRate: isNaN(tax) ? 0 : tax,
        openingStock: isNaN(op) ? 0 : op,
        currentStock: isNaN(op) ? 0 : op,
        reorderLevel: isNaN(reorder) ? 5 : reorder,
        isActive: true,
      });
    }

    // Process chunked
    setTimeout(() => {
      const result = db.bulkImportProducts(productsToImport, updateDuplicates);
      setProgress(100);
      setIsProcessing(false);
      setImportResult(result);
      onImportComplete();
    }, 250);
  };

  const handleDownloadSampleTemplate = () => {
    const templateData = [
      {
        'Product Code': 'PRD0001',
        Barcode: '8901030012345',
        'Product Name (English)': 'Ponni Boiled Rice 5kg',
        'Product Name (Tamil)': 'பொன்னி புழுங்கல் அரிசி 5கிகி',
        Category: 'Groceries & Staples',
        Brand: 'Tata Consumer',
        Unit: 'BAG',
        'HSN Code': '1006',
        'Purchase Rate': 280,
        'Sale Rate': 320,
        'Wholesale Rate': 305,
        MRP: 340,
        'Tax %': 0,
        'Opening Stock': 100,
        'Reorder Level': 15,
      },
      {
        'Product Code': 'PRD0002',
        Barcode: '8901030012346',
        'Product Name (English)': 'Aachi Chilli Powder 100g',
        'Product Name (Tamil)': 'ஆச்சி மிளகாய் தூள் 100கி',
        Category: 'Groceries & Staples',
        Brand: 'Aachi Masala',
        Unit: 'PACK',
        'HSN Code': '0904',
        'Purchase Rate': 32,
        'Sale Rate': 38,
        'Wholesale Rate': 35,
        MRP: 40,
        'Tax %': 5,
        'Opening Stock': 250,
        'Reorder Level': 25,
      },
      {
        'Product Code': 'PRD0003',
        Barcode: '8901030012347',
        'Product Name (English)': 'Tata Salt Iodized 1kg',
        'Product Name (Tamil)': 'டாடா அயோடின் உப்பு 1கிகி',
        Category: 'Groceries & Staples',
        Brand: 'Tata Consumer',
        Unit: 'PACK',
        'HSN Code': '2501',
        'Purchase Rate': 22,
        'Sale Rate': 28,
        'Wholesale Rate': 25,
        MRP: 28,
        'Tax %': 0,
        'Opening Stock': 500,
        'Reorder Level': 50,
      },
    ];

    const ws = XLSX.utils.json_to_sheet(templateData);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Products');
    XLSX.writeFile(wb, 'ShopBilling_Import_Template.xlsx');
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-2xl overflow-hidden flex flex-col max-h-[88vh]">
        {/* Header */}
        <div className="bg-gradient-to-r from-[#173e65] to-[#255a90] text-white px-4 py-2.5 flex items-center justify-between select-none">
          <div className="flex items-center gap-2">
            <FileSpreadsheet className="w-4 h-4 text-emerald-300" />
            <h3 className="text-xs font-semibold">Bulk Excel / CSV Import (ClosedXML Specification)</h3>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Content */}
        <div className="p-4 overflow-y-auto space-y-4 text-xs">
          {/* File Picker */}
          <div className="border border-slate-200 rounded p-3 bg-slate-50/50 space-y-3">
            <div className="flex items-center justify-between">
              <label className="font-semibold text-slate-700">1. Select Excel or CSV File</label>
              <button
                type="button"
                onClick={handleDownloadSampleTemplate}
                className="text-[#106ebe] hover:underline font-medium flex items-center gap-1"
              >
                <Download className="w-3.5 h-3.5" />
                <span>Download Sample Template (.xlsx)</span>
              </button>
            </div>

            <div className="flex items-center gap-2">
              <input
                type="file"
                accept=".xlsx, .xls, .csv"
                onChange={handleFileChange}
                className="w-full text-xs text-slate-500 file:mr-3 file:py-1.5 file:px-3 file:rounded file:border-0 file:text-xs file:font-semibold file:bg-[#106ebe] file:text-white hover:file:bg-[#005a9e] file:cursor-pointer"
              />
            </div>

            <label className="flex items-center gap-2 text-slate-600 cursor-pointer pt-1">
              <input
                type="checkbox"
                checked={updateDuplicates}
                onChange={(e) => setUpdateDuplicates(e.target.checked)}
                className="accent-[#106ebe] rounded"
              />
              <span>Update existing product records if Barcode or Code matches</span>
            </label>
          </div>

          {/* Parsed Preview */}
          {parsedRows.length > 0 && !importResult && (
            <div className="border border-slate-200 rounded p-3 bg-slate-50/50 space-y-2">
              <div className="flex items-center justify-between font-semibold text-slate-700">
                <span>2. Ready to Import ({parsedRows.length.toLocaleString()} rows detected)</span>
                <span className="text-emerald-700 font-mono text-[11px]">Validated & Ready</span>
              </div>

              <div className="max-h-40 overflow-auto border border-slate-200 rounded bg-white">
                <table className="w-full text-left text-[11px]">
                  <thead className="bg-slate-100 text-slate-700 sticky top-0">
                    <tr>
                      <th className="p-1.5 border-b">Code</th>
                      <th className="p-1.5 border-b">Barcode</th>
                      <th className="p-1.5 border-b">English Name</th>
                      <th className="p-1.5 border-b">Tamil Name</th>
                      <th className="p-1.5 border-b text-right">Sale ₹</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {parsedRows.slice(0, 5).map((r, i) => (
                      <tr key={i}>
                        <td className="p-1 font-mono">{r['Product Code'] || r['Code']}</td>
                        <td className="p-1 font-mono">{r['Barcode']}</td>
                        <td className="p-1">{r['Product Name (English)'] || r['Name']}</td>
                        <td className="p-1 font-sans">{r['Product Name (Tamil)'] || '—'}</td>
                        <td className="p-1 text-right font-mono">₹{r['Sale Rate']}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {parsedRows.length > 5 && (
                <div className="text-[10px] text-slate-400 text-right">
                  + {parsedRows.length - 5} more rows...
                </div>
              )}
            </div>
          )}

          {/* Progress Indicator */}
          {isProcessing && (
            <div className="space-y-1.5">
              <div className="flex justify-between text-slate-600 font-medium">
                <span>Importing records in transactions...</span>
                <span>{progress}%</span>
              </div>
              <div className="w-full bg-slate-200 rounded-full h-2 overflow-hidden">
                <div
                  className="bg-[#106ebe] h-2 rounded-full transition-all duration-200"
                  style={{ width: `${progress}%` }}
                />
              </div>
            </div>
          )}

          {/* Import Results Summary */}
          {importResult && (
            <div className="border border-slate-200 rounded p-3 bg-slate-50/50 space-y-2">
              <div className="flex items-center gap-2 text-slate-800 font-bold">
                <CheckCircle2 className="w-4 h-4 text-emerald-600" />
                <span>Import Operation Completed</span>
              </div>

              <div className="grid grid-cols-2 gap-2 text-xs">
                <div className="bg-emerald-50 text-emerald-900 border border-emerald-200 p-2 rounded">
                  <span className="block text-[10px] text-emerald-700">Successfully Imported</span>
                  <strong className="text-sm font-mono">{importResult.imported.toLocaleString()}</strong> items
                </div>
                <div className="bg-rose-50 text-rose-900 border border-rose-200 p-2 rounded">
                  <span className="block text-[10px] text-rose-700">Failed / Rejected Rows</span>
                  <strong className="text-sm font-mono">{importResult.failed.toLocaleString()}</strong> items
                </div>
              </div>

              {importResult.errors.length > 0 && (
                <div className="mt-2 border border-rose-200 rounded p-2 bg-white max-h-32 overflow-auto">
                  <span className="font-semibold text-rose-800 block mb-1">Failed Row Errors:</span>
                  <ul className="list-disc list-inside space-y-0.5 text-[11px] text-rose-700">
                    {importResult.errors.map((err, i) => (
                      <li key={i}>
                        Row {err.row}: {err.error}
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div className="bg-slate-100 border-t border-slate-200 px-4 py-2.5 flex items-center justify-end gap-2">
          <button
            onClick={onClose}
            className="px-3.5 py-1.5 bg-slate-200 hover:bg-slate-300 text-slate-700 rounded font-medium text-xs"
          >
            Close
          </button>
          <button
            onClick={handleStartImport}
            disabled={parsedRows.length === 0 || isProcessing}
            className="px-4 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold text-xs disabled:opacity-40 flex items-center gap-1.5"
          >
            <Upload className="w-3.5 h-3.5" />
            <span>{isProcessing ? 'Importing...' : `Import ${parsedRows.length} Rows`}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
