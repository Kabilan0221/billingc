import React, { useState, useEffect, useCallback } from 'react';
import * as XLSX from 'xlsx';
import { Win7TitleBar } from './components/Win7TitleBar';
import { Win7Sidebar, SIDEBAR_ITEMS } from './components/Win7Sidebar';
import { ProductMaster } from './components/ProductMaster';
import { ProductDialog } from './components/ProductDialog';
import { CategoryBrandDialog } from './components/CategoryBrandDialog';
import { ExcelDialog } from './components/ExcelDialog';
import { BarcodeDialog } from './components/BarcodeDialog';
import { SolutionExplorer } from './components/SolutionExplorer';
import { BackupRestoreView } from './components/BackupRestoreView';
import { PosBillingPreview } from './components/PosBillingPreview';
import { ModulePlaceholderView } from './components/ModulePlaceholderView';
import { SupplierManagement } from './components/SupplierManagement';
import { InboundGoods } from './components/InboundGoods';
import { StockManagement } from './components/StockManagement';
import { db } from './services/dbEngine';
import { Product } from './types';

export default function App() {
  const [activeView, setActiveView] = useState<'desktop' | 'solution'>('desktop');
  const [activeNav, setActiveNav] = useState<string>('products'); // Default to Products
  const [selectedSupplierForInbound, setSelectedSupplierForInbound] = useState<number | undefined>(undefined);
  const [dataVersion, setDataVersion] = useState(0);

  // Dialogs state
  const [isProductDialogOpen, setIsProductDialogOpen] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);

  const [isCategoryDialogOpen, setIsCategoryDialogOpen] = useState(false);
  const [categoryDialogTab, setCategoryDialogTab] = useState<'categories' | 'brands'>('categories');

  const [isExcelDialogOpen, setIsExcelDialogOpen] = useState(false);
  const [barcodeProduct, setBarcodeProduct] = useState<Product | null>(null);

  const [currentTime, setCurrentTime] = useState(new Date().toLocaleTimeString());

  // Real-time clock tick
  useEffect(() => {
    const timer = setInterval(() => {
      setCurrentTime(new Date().toLocaleTimeString());
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const refreshData = useCallback(() => {
    setDataVersion((v) => v + 1);
  }, []);

  const metrics = db.getMetrics();

  // Keyboard shortcut handler (F1-F10) matching WinForms shell
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F1') {
        e.preventDefault();
        setActiveNav('pos');
      } else if (e.key === 'F2') {
        e.preventDefault();
        setActiveNav('products');
        if (activeNav === 'products' && !isProductDialogOpen) {
          setEditingProduct(null);
          setIsProductDialogOpen(true);
        }
      } else if (e.key === 'F3') {
        e.preventDefault();
        setActiveNav('inbound');
      } else if (e.key === 'F4') {
        e.preventDefault();
        setActiveNav('barcode');
      } else if (e.key === 'F5') {
        e.preventDefault();
        setActiveNav('payments');
      } else if (e.key === 'F6') {
        e.preventDefault();
        setActiveNav('history');
      } else if (e.key === 'F7') {
        e.preventDefault();
        setActiveNav('suppliers');
      } else if (e.key === 'F8') {
        e.preventDefault();
        setActiveNav('stock');
      } else if (e.key === 'F9') {
        e.preventDefault();
        setActiveNav('invoices');
      } else if (e.key === 'F10') {
        e.preventDefault();
        setActiveNav('reports');
      } else if (e.key === 'Escape') {
        setIsProductDialogOpen(false);
        setIsCategoryDialogOpen(false);
        setIsExcelDialogOpen(false);
        setBarcodeProduct(null);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [activeNav, isProductDialogOpen]);

  // Export current catalog to real formatted .xlsx
  const handleExportExcel = () => {
    const all = db.getAllProducts();
    const exportRows = all.map((p) => ({
      'Product Code': p.productCode,
      Barcode: p.barcode,
      'Product Name (English)': p.nameEn,
      'Product Name (Tamil)': p.nameTa || '',
      Category: p.categoryName || '',
      Subcategory: p.subcategoryName || '',
      Brand: p.brandName || '',
      Unit: p.unit,
      'HSN Code': p.hsnCode || '',
      'Purchase Rate': p.purchaseRate,
      'Sale Rate': p.saleRate,
      'Wholesale Rate': p.wholesaleRate,
      MRP: p.mrp,
      'Tax %': p.taxRate,
      'Opening Stock': p.openingStock,
      'Current Stock': p.currentStock,
      'Reorder Level': p.reorderLevel,
      Status: p.isActive ? 'Active' : 'Inactive',
    }));

    const ws = XLSX.utils.json_to_sheet(exportRows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Products');
    const filename = `ShopBilling_Catalog_${new Date().toISOString().slice(0, 10)}.xlsx`;
    XLSX.writeFile(wb, filename);
  };

  const handleOpenAddProduct = () => {
    setEditingProduct(null);
    setIsProductDialogOpen(true);
  };

  const handleOpenEditProduct = (p: Product) => {
    setEditingProduct(p);
    setIsProductDialogOpen(true);
  };

  const handleOpenCategoryManager = () => {
    setCategoryDialogTab('categories');
    setIsCategoryDialogOpen(true);
  };

  const handleOpenBrandManager = () => {
    setCategoryDialogTab('brands');
    setIsCategoryDialogOpen(true);
  };

  return (
    <div className="w-screen h-screen bg-[#1c304a] flex items-center justify-center p-0 md:p-2 overflow-hidden font-sans">
      {/* Windows 7 Aero Shell Container */}
      <div className="w-full h-full max-w-[1600px] bg-white rounded-lg shadow-2xl border border-[#4572a7] flex flex-col overflow-hidden">
        {/* Win7 Title Bar with View Mode Switcher */}
        <Win7TitleBar
          title="ShopBilling — Commercial Retail & Inventory Suite (Windows 7 SP1 · .NET 4.8 · SQLite)"
          activeView={activeView}
          onToggleView={setActiveView}
          is50kLoaded={metrics.is50kLoaded}
        />

        {/* Sub-Header Store Banner */}
        <div className="bg-[#182b49] text-white px-4 py-2 flex flex-wrap items-center justify-between border-b border-[#122035] text-xs">
          <div className="flex items-center gap-3">
            <span className="font-bold tracking-wider text-amber-300">SRI MURUGAN SUPER MARKET</span>
            <span className="hidden sm:inline text-slate-400">|</span>
            <span className="text-slate-300 hidden sm:inline">124 Bazaar Road, Chennai - 600017</span>
            <span className="hidden sm:inline text-slate-400">|</span>
            <span className="text-slate-300 font-mono text-[11px]">GSTIN: 33AAAAA0000A1Z5</span>
          </div>

          <div className="flex items-center gap-3 text-slate-300 text-[11px]">
            <span className="font-mono bg-white/10 px-2 py-0.5 rounded">Terminal #01 (Offline)</span>
            <span className="text-emerald-400 font-medium">SQLite WAL Active</span>
          </div>
        </div>

        {/* Main Application Area */}
        {activeView === 'desktop' ? (
          <div className="flex-1 flex overflow-hidden">
            {/* Sidebar with all 14 Menu Sections */}
            <Win7Sidebar
              activeNav={activeNav}
              onSelectNav={setActiveNav}
              productCount={metrics.total}
              lowStockCount={metrics.lowStock}
            />

            {/* Active Content Module */}
            <main className="flex-1 flex flex-col h-full overflow-hidden">
              {activeNav === 'products' && (
                <ProductMaster
                  onOpenAddProduct={handleOpenAddProduct}
                  onOpenEditProduct={handleOpenEditProduct}
                  onOpenCategoryManager={handleOpenCategoryManager}
                  onOpenBrandManager={handleOpenBrandManager}
                  onOpenExcelImport={() => setIsExcelDialogOpen(true)}
                  onOpenBarcodePrint={(p) => setBarcodeProduct(p)}
                  onExportExcel={handleExportExcel}
                  onRefreshData={refreshData}
                />
              )}

              {activeNav === 'pos' && (
                <PosBillingPreview onNavigateToProducts={() => setActiveNav('products')} />
              )}

              {activeNav === 'barcode' && (
                <div className="flex-1 flex flex-col items-center justify-center p-6 bg-[#f8fafc] text-center">
                  <div className="max-w-md bg-white border border-slate-200 rounded-lg p-6 shadow-xs space-y-4 text-xs">
                    <h3 className="text-base font-bold text-slate-800">Barcode Studio (F4)</h3>
                    <p className="text-slate-600">
                      Generate and print barcode stickers for any product in the catalog. Select any product in the Product Master (F2) and click &quot;Barcode Label&quot;, or select one below:
                    </p>
                    <div className="flex flex-col gap-1.5 max-h-48 overflow-y-auto text-left border border-slate-200 rounded p-2">
                      {db.getAllProducts().slice(0, 10).map((p) => (
                        <button
                          key={p.id}
                          onClick={() => setBarcodeProduct(p)}
                          className="px-2.5 py-1.5 text-left rounded hover:bg-slate-100 flex justify-between items-center"
                        >
                          <span className="font-medium truncate">{p.nameEn}</span>
                          <span className="font-mono text-slate-500 shrink-0">{p.barcode}</span>
                        </button>
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {activeNav === 'inbound' && (
                <InboundGoods
                  initialSupplierId={selectedSupplierForInbound}
                  onRefreshData={refreshData}
                  onNavigateToSuppliers={() => setActiveNav('suppliers')}
                />
              )}

              {activeNav === 'suppliers' && (
                <SupplierManagement
                  onRefreshData={refreshData}
                  onNavigateToInbound={(supId) => {
                    setSelectedSupplierForInbound(supId);
                    setActiveNav('inbound');
                  }}
                />
              )}

              {activeNav === 'stock' && (
                <StockManagement
                  onRefreshData={refreshData}
                  onNavigateToInbound={() => setActiveNav('inbound')}
                />
              )}

              {activeNav === 'backup' && <BackupRestoreView />}

              {activeNav === 'data' && <BackupRestoreView />}

              {activeNav !== 'products' &&
                activeNav !== 'inbound' &&
                activeNav !== 'suppliers' &&
                activeNav !== 'stock' &&
                activeNav !== 'pos' &&
                activeNav !== 'barcode' &&
                activeNav !== 'backup' &&
                activeNav !== 'data' && (
                  <ModulePlaceholderView
                    title={SIDEBAR_ITEMS.find((s) => s.key === activeNav)?.label || 'Module'}
                    moduleKey={activeNav}
                    onGoToProducts={() => setActiveNav('products')}
                  />
                )}
            </main>
          </div>
        ) : (
          /* Visual Studio Solution & C# Source Explorer View */
          <SolutionExplorer />
        )}

        {/* Windows 7 Status Strip at Bottom */}
        <footer className="bg-[#f0f0f0] border-t border-[#d0d0d0] px-3 py-1 flex items-center justify-between text-[11px] text-slate-600 select-none">
          <div className="flex items-center gap-3 flex-wrap">
            <span className="flex items-center gap-1.5 text-emerald-700 font-medium">
              <span className="w-2 h-2 rounded-full bg-emerald-500" />
              <span>Database: SQLite 3.39 (WAL Mode, FK Active)</span>
            </span>
            <span className="hidden sm:inline text-slate-300">|</span>
            <span className="hidden sm:inline">
              Catalog: <strong>{metrics.total.toLocaleString()}</strong> items
            </span>
            <span className="hidden sm:inline text-slate-300">|</span>
            <span className="hidden sm:inline">
              Suppliers: <strong>{metrics.totalSuppliers}</strong> active
            </span>
            <span className="hidden sm:inline text-slate-300">|</span>
            <span className="hidden sm:inline text-blue-900 font-medium">
              Stock In Hand: <strong>{metrics.totalStockQuantity.toLocaleString()}</strong> units (₹{metrics.stockValueAtCost.toLocaleString('en-IN', { minimumFractionDigits: 0 })})
            </span>
            {metrics.pendingDrafts > 0 && (
              <>
                <span className="hidden sm:inline text-slate-300">|</span>
                <span className="text-amber-700 font-semibold">
                  Inbound Drafts: {metrics.pendingDrafts}
                </span>
              </>
            )}
            {metrics.totalPayableToSuppliers > 0 && (
              <>
                <span className="hidden sm:inline text-slate-300">|</span>
                <span className="text-rose-700 font-semibold">
                  Vendor Payable: ₹{metrics.totalPayableToSuppliers.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
              </>
            )}
            {metrics.lowStock > 0 && (
              <>
                <span className="hidden sm:inline text-slate-300">|</span>
                <span className="text-rose-600 font-semibold">
                  Low Stock: {metrics.lowStock.toLocaleString()}
                </span>
              </>
            )}
          </div>

          <div className="flex items-center gap-3 font-mono shrink-0">
            <span>Windows 7 SP1 (.NET 4.8)</span>
            <span className="text-slate-300">|</span>
            <span>{currentTime}</span>
          </div>
        </footer>
      </div>

      {/* Modals & Dialogs */}
      <ProductDialog
        product={editingProduct}
        isOpen={isProductDialogOpen}
        onClose={() => setIsProductDialogOpen(false)}
        onSaved={refreshData}
      />

      <CategoryBrandDialog
        initialTab={categoryDialogTab}
        isOpen={isCategoryDialogOpen}
        onClose={() => setIsCategoryDialogOpen(false)}
        onUpdated={refreshData}
      />

      <ExcelDialog
        isOpen={isExcelDialogOpen}
        onClose={() => setIsExcelDialogOpen(false)}
        onImportComplete={refreshData}
      />

      <BarcodeDialog
        product={barcodeProduct}
        isOpen={!!barcodeProduct}
        onClose={() => setBarcodeProduct(null)}
      />
    </div>
  );
}
