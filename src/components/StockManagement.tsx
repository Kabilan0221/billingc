import React, { useState, useEffect, useMemo, useRef } from 'react';
import * as XLSX from 'xlsx';
import {
  Layers,
  Search,
  Filter,
  Download,
  Plus,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  TrendingUp,
  Boxes,
  FileSpreadsheet,
  ClipboardCheck,
  History,
  ArrowRight,
  Trash2,
  Calendar,
  Building,
  RotateCcw,
  BarChart2,
  Eye,
  Printer,
  Sliders,
  DollarSign,
  Package,
} from 'lucide-react';
import { db } from '../services/dbEngine';
import {
  CurrentStockSummary,
  StockDashboardMetrics,
  StockMovement,
  StockAdjustment,
  StockAdjustmentItem,
  PhysicalStockVerification,
  PhysicalStockVerificationItem,
  Product,
  Category,
  Brand,
} from '../types';

interface StockManagementProps {
  onRefreshData?: () => void;
  onNavigateToInbound?: () => void;
}

export const StockManagement: React.FC<StockManagementProps> = ({
  onRefreshData,
  onNavigateToInbound,
}) => {
  // Navigation Subtabs
  const [activeTab, setActiveTab] = useState<
    'dashboard' | 'current' | 'ledger' | 'adjustments' | 'verification' | 'valuation'
  >('dashboard');

  // Metrics
  const [metrics, setMetrics] = useState<StockDashboardMetrics>(db.getStockDashboardMetrics());

  // Current Stock State
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<number>(0);
  const [selectedBrand, setSelectedBrand] = useState<number>(0);
  const [selectedStatus, setSelectedStatus] = useState<string>('All');
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [pageSize, setPageSize] = useState<number>(50);
  const [currentStockData, setCurrentStockData] = useState<{
    items: CurrentStockSummary[];
    totalCount: number;
    totalPages: number;
    executionTimeMs: number;
  }>({ items: [], totalCount: 0, totalPages: 1, executionTimeMs: 0 });

  // Ledger Filter State
  const [ledgerProductId, setLedgerProductId] = useState<number>(0);
  const [ledgerType, setLedgerType] = useState<string>('All');
  const [ledgerDateFrom, setLedgerDateFrom] = useState<string>('');
  const [ledgerDateTo, setLedgerDateTo] = useState<string>('');

  // Adjustments State
  const [adjustments, setAdjustments] = useState<StockAdjustment[]>(db.getAllStockAdjustments());
  const [selectedAdjustment, setSelectedAdjustment] = useState<StockAdjustment | null>(null);
  const [isAdjModalOpen, setIsAdjModalOpen] = useState(false);
  const [adjModalMode, setAdjModalMode] = useState<'create' | 'view'>('create');

  // Adjustment Form State
  const [adjReason, setAdjReason] = useState('');
  const [adjNotes, setAdjNotes] = useState('');
  const [adjItems, setAdjItems] = useState<StockAdjustmentItem[]>([]);
  const [adjProductSearch, setAdjProductSearch] = useState('');
  const [adjProductCandidates, setAdjProductCandidates] = useState<Product[]>([]);
  const [adjError, setAdjError] = useState<string | null>(null);

  // Physical Verification State
  const [verifications, setVerifications] = useState<PhysicalStockVerification[]>(
    db.getAllPhysicalVerifications()
  );
  const [isVerModalOpen, setIsVerModalOpen] = useState(false);
  const [verCategoryId, setVerCategoryId] = useState<number>(0);
  const [verNotes, setVerNotes] = useState('');
  const [verItems, setVerItems] = useState<
    Array<{
      productId: number;
      productCode: string;
      barcode: string;
      productName: string;
      categoryName: string;
      systemStockAtStart: number;
      physicalCount: number;
      unitCost: number;
    }>
  >([]);
  const [verError, setVerError] = useState<string | null>(null);

  // Reference catalogs
  const categories = useMemo(() => db.getCategories(), []);
  const brands = useMemo(() => db.getBrands(), []);
  const allProducts = useMemo(() => db.getAllProducts(), []);

  // Reload Current Stock
  const loadCurrentStock = () => {
    const res = db.getCurrentStockSummary({
      searchTerm,
      categoryId: selectedCategory > 0 ? selectedCategory : undefined,
      brandId: selectedBrand > 0 ? selectedBrand : undefined,
      stockStatus: selectedStatus,
      pageNumber: currentPage,
      pageSize,
    });
    setCurrentStockData(res);
  };

  // Reload Metrics & Adjustments
  const reloadAll = () => {
    setMetrics(db.getStockDashboardMetrics());
    loadCurrentStock();
    setAdjustments([...db.getAllStockAdjustments()]);
    setVerifications([...db.getAllPhysicalVerifications()]);
    if (onRefreshData) onRefreshData();
  };

  useEffect(() => {
    loadCurrentStock();
  }, [searchTerm, selectedCategory, selectedBrand, selectedStatus, currentPage, pageSize]);

  // Handle Search Product for Adjustment
  useEffect(() => {
    if (!adjProductSearch.trim()) {
      setAdjProductCandidates([]);
      return;
    }
    const q = adjProductSearch.toLowerCase().trim();
    const matches = allProducts
      .filter(
        (p) =>
          p.isActive &&
          (p.nameEn.toLowerCase().includes(q) ||
            p.productCode.toLowerCase().includes(q) ||
            p.barcode.toLowerCase().includes(q))
      )
      .slice(0, 8);
    setAdjProductCandidates(matches);
  }, [adjProductSearch, allProducts]);

  // Add Item to Adjustment
  const handleAddAdjustmentItem = (p: Product) => {
    if (adjItems.some((i) => i.productId === p.id)) {
      setAdjError(`Product '${p.nameEn}' is already in this adjustment list.`);
      return;
    }
    setAdjError(null);
    const newItem: StockAdjustmentItem = {
      id: Date.now() + Math.random(),
      adjustmentId: 0,
      productId: p.id,
      productCode: p.productCode,
      barcode: p.barcode,
      productName: p.nameEn,
      categoryName: p.categoryName || 'General',
      currentStock: p.currentStock,
      physicalCount: p.currentStock,
      differenceQuantity: 0,
      adjustmentType: 'Physical Count Correction',
      unitCost: p.purchaseRate,
      reason: '',
      notes: '',
    };
    setAdjItems([...adjItems, newItem]);
    setAdjProductSearch('');
    setAdjProductCandidates([]);
  };

  // Update Adjustment Item Difference or Physical Count
  const handleUpdateAdjItem = (
    index: number,
    field: 'physicalCount' | 'differenceQuantity' | 'adjustmentType' | 'notes',
    value: any
  ) => {
    setAdjError(null);
    const updated = [...adjItems];
    const item = { ...updated[index] };

    if (field === 'physicalCount') {
      const pCount = Math.max(0, parseFloat(value) || 0);
      item.physicalCount = pCount;
      item.differenceQuantity = parseFloat((pCount - item.currentStock).toFixed(2));
      item.adjustmentType = item.differenceQuantity >= 0 ? 'Increase Stock' : 'Decrease Stock';
    } else if (field === 'differenceQuantity') {
      const diff = parseFloat(value) || 0;
      item.differenceQuantity = diff;
      item.physicalCount = Math.max(0, item.currentStock + diff);
      item.adjustmentType = diff >= 0 ? 'Increase Stock' : 'Decrease Stock';
    } else if (field === 'adjustmentType') {
      item.adjustmentType = value;
    } else if (field === 'notes') {
      item.notes = value;
    }

    updated[index] = item;
    setAdjItems(updated);
  };

  // Remove Item from Adjustment
  const handleRemoveAdjItem = (index: number) => {
    const updated = adjItems.filter((_, i) => i !== index);
    setAdjItems(updated);
  };

  // Save Adjustment Draft
  const handleSaveAdjDraft = () => {
    if (!adjReason.trim()) {
      setAdjError('Adjustment Reason is mandatory.');
      return;
    }
    if (adjItems.length === 0) {
      setAdjError('Please add at least one product item to adjust.');
      return;
    }

    const res = db.saveStockAdjustmentDraft({
      reason: adjReason,
      notes: adjNotes,
      items: adjItems,
    });

    if (!res.success) {
      setAdjError(res.errors.join(' '));
      return;
    }

    setIsAdjModalOpen(false);
    resetAdjForm();
    reloadAll();
  };

  // Confirm Adjustment Immediately
  const handleConfirmAdjImmediately = () => {
    if (!adjReason.trim()) {
      setAdjError('Adjustment Reason is mandatory.');
      return;
    }
    if (adjItems.length === 0) {
      setAdjError('Please add at least one product item to adjust.');
      return;
    }

    // Check negative stock in UI before calling
    for (const item of adjItems) {
      if (item.differenceQuantity < 0 && item.currentStock + item.differenceQuantity < 0) {
        setAdjError(
          `Rejection: Item '${item.productName}' stock cannot be reduced below zero (Current: ${item.currentStock}, Diff: ${item.differenceQuantity}).`
        );
        return;
      }
    }

    const draftRes = db.saveStockAdjustmentDraft({
      reason: adjReason,
      notes: adjNotes,
      items: adjItems,
    });

    if (!draftRes.success || !draftRes.adjustment) {
      setAdjError(draftRes.errors.join(' '));
      return;
    }

    const confirmRes = db.confirmStockAdjustment(draftRes.adjustment.id);
    if (!confirmRes.success) {
      setAdjError(confirmRes.error || 'Failed to confirm adjustment.');
      return;
    }

    setIsAdjModalOpen(false);
    resetAdjForm();
    reloadAll();
  };

  // Confirm an existing Draft Adjustment from Table
  const handleConfirmDraftAdjustment = (adjId: number) => {
    const res = db.confirmStockAdjustment(adjId);
    if (!res.success) {
      alert(`Cannot confirm adjustment: ${res.error}`);
    } else {
      reloadAll();
    }
  };

  // Cancel a Confirmed Adjustment
  const handleCancelAdjustment = (adjId: number) => {
    if (
      !window.confirm(
        'Are you sure you want to cancel and reverse this stock adjustment? The quantities will be restored to previous inventory levels.'
      )
    ) {
      return;
    }
    const res = db.cancelStockAdjustment(adjId);
    if (!res.success) {
      alert(`Cannot cancel adjustment: ${res.error}`);
    } else {
      reloadAll();
    }
  };

  const resetAdjForm = () => {
    setAdjReason('');
    setAdjNotes('');
    setAdjItems([]);
    setAdjProductSearch('');
    setAdjError(null);
  };

  // Open New Physical Verification Setup
  const handleOpenNewVerification = () => {
    setIsVerModalOpen(true);
    setVerNotes('');
    setVerError(null);

    // Pre-populate products based on category
    let prods = allProducts.filter((p) => p.isActive);
    if (verCategoryId > 0) {
      prods = prods.filter((p) => p.categoryId === verCategoryId);
    }

    const items = prods.slice(0, 100).map((p) => ({
      productId: p.id,
      productCode: p.productCode,
      barcode: p.barcode,
      productName: p.nameEn,
      categoryName: p.categoryName || 'General',
      systemStockAtStart: p.currentStock,
      physicalCount: p.currentStock,
      unitCost: p.purchaseRate,
    }));
    setVerItems(items);
  };

  // Update Physical Count in Verification
  const handleUpdateVerCount = (idx: number, count: number) => {
    const updated = [...verItems];
    updated[idx].physicalCount = Math.max(0, count);
    setVerItems(updated);
  };

  // Confirm Physical Verification & Apply Reconciliation
  const handleConfirmVerificationSession = () => {
    if (verItems.length === 0) {
      setVerError('No products in this verification session.');
      return;
    }

    const items: PhysicalStockVerificationItem[] = verItems.map((v, i) => ({
      id: i + 1,
      verificationId: 0,
      productId: v.productId,
      productCode: v.productCode,
      barcode: v.barcode,
      productName: v.productName,
      categoryName: v.categoryName,
      systemStockAtStart: v.systemStockAtStart,
      systemStockAtConfirm: v.systemStockAtStart,
      physicalCount: v.physicalCount,
      differenceQuantity: v.physicalCount - v.systemStockAtStart,
      discrepancyResolved: true,
    }));

    const draft = db.savePhysicalVerificationDraft({
      categoryId: verCategoryId > 0 ? verCategoryId : undefined,
      categoryName:
        verCategoryId > 0
          ? categories.find((c: Category) => c.id === verCategoryId)?.name || 'Category'
          : 'All Categories',
      notes: verNotes,
      items,
    });

    if (!draft.success || !draft.verification) {
      setVerError(draft.errors.join(' '));
      return;
    }

    const confirmRes = db.confirmPhysicalVerification(draft.verification.id);
    if (!confirmRes.success) {
      setVerError(confirmRes.error || 'Failed to reconcile verification count.');
      return;
    }

    setIsVerModalOpen(false);
    reloadAll();
  };

  // Export Current Stock to Excel
  const handleExportStockExcel = () => {
    const allStock = db.getCurrentStockSummary({
      stockStatus: 'All',
      pageNumber: 1,
      pageSize: 50000,
    }).items;

    const exportRows = allStock.map((s) => ({
      'Product Code': s.productCode,
      Barcode: s.barcode,
      'Product Name': s.productName,
      Category: s.categoryName,
      Brand: s.brandName,
      Unit: s.unit,
      'Opening Stock': s.openingStock,
      'Purchased Qty': s.purchasedQuantity,
      'Free Qty': s.freeQuantityReceived,
      'Sold Qty': s.soldQuantity,
      'Returned Qty': s.returnedQuantity,
      Adjustments: s.adjustmentQuantity,
      'Current Stock': s.currentStock,
      'Reorder Level': s.reorderLevel,
      'Max Stock Level': s.maxStockLevel,
      'Purchase Rate (Cost)': s.purchaseRate,
      'Sale Rate': s.saleRate,
      'Stock Value (Cost)': s.stockValuePurchase,
      'Stock Value (Retail)': s.stockValueSale,
      Status: s.stockStatus,
    }));

    const ws = XLSX.utils.json_to_sheet(exportRows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Current Stock');
    const filename = `Stock_Report_${new Date().toISOString().slice(0, 10)}.xlsx`;
    XLSX.writeFile(wb, filename);
  };

  // Export Valuation Report to Excel
  const handleExportValuationExcel = () => {
    const val = db.getStockValuation();
    const rows = val.map((v) => ({
      'Product Code': v.productCode,
      Barcode: v.barcode,
      'Product Name': v.productName,
      Category: v.categoryName,
      'Current Qty': v.currentStock,
      'Cost Rate (₹)': v.purchaseRate,
      'Stock Value @ Cost (₹)': v.stockValueCost,
      'Selling Price (₹)': v.saleRate,
      'Stock Value @ Retail (₹)': v.stockValueSale,
      'Potential Gross Margin (₹)': v.potentialMargin,
      'Margin %':
        v.stockValueSale > 0
          ? ((v.potentialMargin / v.stockValueSale) * 100).toFixed(1) + '%'
          : '0%',
      'Valuation Method': v.valuationMethod,
    }));

    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Stock Valuation');
    const filename = `Stock_Valuation_${new Date().toISOString().slice(0, 10)}.xlsx`;
    XLSX.writeFile(wb, filename);
  };

  // Ledger Movements Filtered
  const filteredMovements = useMemo(() => {
    let list = db.getStockMovements(ledgerProductId > 0 ? ledgerProductId : undefined);
    if (ledgerType && ledgerType !== 'All') {
      list = list.filter((m) => m.referenceType === ledgerType);
    }
    if (ledgerDateFrom) {
      list = list.filter((m) => m.createdAt >= ledgerDateFrom);
    }
    if (ledgerDateTo) {
      list = list.filter((m) => m.createdAt <= ledgerDateTo + 'T23:59:59Z');
    }
    return list;
  }, [ledgerProductId, ledgerType, ledgerDateFrom, ledgerDateTo, metrics]);

  // Valuation Data
  const valuationData = useMemo(() => {
    return db.getStockValuation();
  }, [metrics]);

  return (
    <div className="flex-1 flex flex-col h-full bg-[#f0f3f6] select-none overflow-hidden font-sans text-slate-800">
      {/* 1. Windows 7 Header Strip */}
      <div className="bg-[#1f4e79] text-white px-4 py-2.5 flex items-center justify-between shadow-xs shrink-0 border-b border-[#143657]">
        <div className="flex items-center gap-3">
          <div className="p-1.5 bg-white/10 rounded flex items-center justify-center">
            <Layers className="w-5 h-5 text-blue-200" />
          </div>
          <div>
            <h1 className="text-sm font-bold tracking-wide flex items-center gap-2">
              STOCK & INVENTORY MANAGEMENT (F8)
              <span className="text-[10px] font-normal px-2 py-0.5 rounded bg-blue-900/60 text-blue-200 border border-blue-400/20">
                Phase 3 Active
              </span>
            </h1>
            <p className="text-[11px] text-blue-200/80">
              Perpetual Stock Tracking • Physical Count Audit • Real-Time Valuation • Movement Ledger
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={reloadAll}
            className="px-3 py-1.5 text-xs bg-white/10 hover:bg-white/20 active:bg-white/30 text-white rounded flex items-center gap-1.5 font-medium border border-white/20 transition-colors cursor-pointer"
            title="Refresh All Database Tables"
          >
            <RefreshCw className="w-3.5 h-3.5" />
            Refresh Data
          </button>
        </div>
      </div>

      {/* 2. Subtabs Navigation Bar (WinForms TabControl) */}
      <div className="bg-[#e4ebf2] border-b border-[#c8d4e2] px-3 pt-2 flex items-center gap-1 shrink-0 overflow-x-auto">
        <button
          onClick={() => setActiveTab('dashboard')}
          className={`px-4 py-2 text-xs font-semibold rounded-t-md transition-colors flex items-center gap-1.5 cursor-pointer ${
            activeTab === 'dashboard'
              ? 'bg-white text-[#1f4e79] border-t-2 border-t-[#1f4e79] border-x border-[#c8d4e2] shadow-xs'
              : 'text-slate-600 hover:text-slate-900 hover:bg-[#d8e2ed]'
          }`}
        >
          <BarChart2 className="w-3.5 h-3.5" />
          Stock Dashboard
        </button>

        <button
          onClick={() => setActiveTab('current')}
          className={`px-4 py-2 text-xs font-semibold rounded-t-md transition-colors flex items-center gap-1.5 cursor-pointer ${
            activeTab === 'current'
              ? 'bg-white text-[#1f4e79] border-t-2 border-t-[#1f4e79] border-x border-[#c8d4e2] shadow-xs'
              : 'text-slate-600 hover:text-slate-900 hover:bg-[#d8e2ed]'
          }`}
        >
          <Boxes className="w-3.5 h-3.5" />
          Current Stock Inventory
          <span className="text-[10px] px-1.5 py-0.2 bg-slate-200 text-slate-700 rounded-full font-bold">
            {metrics.totalActiveProducts}
          </span>
        </button>

        <button
          onClick={() => setActiveTab('ledger')}
          className={`px-4 py-2 text-xs font-semibold rounded-t-md transition-colors flex items-center gap-1.5 cursor-pointer ${
            activeTab === 'ledger'
              ? 'bg-white text-[#1f4e79] border-t-2 border-t-[#1f4e79] border-x border-[#c8d4e2] shadow-xs'
              : 'text-slate-600 hover:text-slate-900 hover:bg-[#d8e2ed]'
          }`}
        >
          <History className="w-3.5 h-3.5" />
          Stock Movement Ledger
        </button>

        <button
          onClick={() => setActiveTab('adjustments')}
          className={`px-4 py-2 text-xs font-semibold rounded-t-md transition-colors flex items-center gap-1.5 cursor-pointer ${
            activeTab === 'adjustments'
              ? 'bg-white text-[#1f4e79] border-t-2 border-t-[#1f4e79] border-x border-[#c8d4e2] shadow-xs'
              : 'text-slate-600 hover:text-slate-900 hover:bg-[#d8e2ed]'
          }`}
        >
          <Sliders className="w-3.5 h-3.5" />
          Stock Adjustments
          {adjustments.length > 0 && (
            <span className="text-[10px] px-1.5 py-0.2 bg-blue-100 text-blue-800 rounded-full font-bold">
              {adjustments.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab('verification')}
          className={`px-4 py-2 text-xs font-semibold rounded-t-md transition-colors flex items-center gap-1.5 cursor-pointer ${
            activeTab === 'verification'
              ? 'bg-white text-[#1f4e79] border-t-2 border-t-[#1f4e79] border-x border-[#c8d4e2] shadow-xs'
              : 'text-slate-600 hover:text-slate-900 hover:bg-[#d8e2ed]'
          }`}
        >
          <ClipboardCheck className="w-3.5 h-3.5" />
          Physical Stock Audit
        </button>

        <button
          onClick={() => setActiveTab('valuation')}
          className={`px-4 py-2 text-xs font-semibold rounded-t-md transition-colors flex items-center gap-1.5 cursor-pointer ${
            activeTab === 'valuation'
              ? 'bg-white text-[#1f4e79] border-t-2 border-t-[#1f4e79] border-x border-[#c8d4e2] shadow-xs'
              : 'text-slate-600 hover:text-slate-900 hover:bg-[#d8e2ed]'
          }`}
        >
          <TrendingUp className="w-3.5 h-3.5" />
          Stock Valuation & Margin
        </button>
      </div>

      {/* 3. Subtab Content Area */}
      <div className="flex-1 bg-white flex flex-col overflow-hidden">
        {/* ============================================================== */}
        {/* TAB 1: STOCK DASHBOARD */}
        {/* ============================================================== */}
        {activeTab === 'dashboard' && (
          <div className="flex-1 p-5 overflow-y-auto space-y-5 bg-[#fbfcfd]">
            {/* Top Metric Cards */}
            <div className="grid grid-cols-1 md:grid-cols-4 gap-3.5">
              {/* Card 1: Total Active Products */}
              <div
                onClick={() => {
                  setSelectedStatus('All');
                  setActiveTab('current');
                }}
                className="bg-white border border-slate-200 rounded-lg p-3.5 shadow-2xs hover:border-blue-400 hover:shadow-xs transition-all cursor-pointer flex flex-col justify-between"
              >
                <div className="flex items-center justify-between text-slate-500">
                  <span className="text-xs font-medium uppercase tracking-wider">Active Catalog</span>
                  <Package className="w-4 h-4 text-blue-600" />
                </div>
                <div className="mt-2">
                  <span className="text-2xl font-bold text-slate-800">
                    {metrics.totalActiveProducts.toLocaleString()}
                  </span>
                  <span className="text-xs text-slate-500 ml-1.5 font-normal">items</span>
                </div>
                <div className="text-[11px] text-blue-600 mt-2 font-medium flex items-center gap-1">
                  View Current Inventory <ArrowRight className="w-3 h-3" />
                </div>
              </div>

              {/* Card 2: Total Units in Stock */}
              <div className="bg-white border border-slate-200 rounded-lg p-3.5 shadow-2xs flex flex-col justify-between">
                <div className="flex items-center justify-between text-slate-500">
                  <span className="text-xs font-medium uppercase tracking-wider">Total Units in Hand</span>
                  <Boxes className="w-4 h-4 text-emerald-600" />
                </div>
                <div className="mt-2">
                  <span className="text-2xl font-bold text-emerald-700">
                    {metrics.totalStockQuantity.toLocaleString()}
                  </span>
                  <span className="text-xs text-slate-500 ml-1.5 font-normal">units</span>
                </div>
                <div className="text-[11px] text-slate-500 mt-2">Sum of all warehouse stock</div>
              </div>

              {/* Card 3: Out of Stock Warning */}
              <div
                onClick={() => {
                  setSelectedStatus('Out of Stock');
                  setActiveTab('current');
                }}
                className="bg-white border border-red-200 rounded-lg p-3.5 shadow-2xs hover:border-red-400 hover:shadow-xs transition-all cursor-pointer flex flex-col justify-between"
              >
                <div className="flex items-center justify-between text-red-600">
                  <span className="text-xs font-bold uppercase tracking-wider">Out of Stock</span>
                  <XCircle className="w-4 h-4 text-red-600" />
                </div>
                <div className="mt-2">
                  <span className="text-2xl font-bold text-red-600">
                    {metrics.outOfStockCount.toLocaleString()}
                  </span>
                  <span className="text-xs text-red-500 ml-1.5 font-normal">items depleted</span>
                </div>
                <div className="text-[11px] text-red-600 mt-2 font-medium flex items-center gap-1">
                  Filter Out of Stock <ArrowRight className="w-3 h-3" />
                </div>
              </div>

              {/* Card 4: Low Stock Warning */}
              <div
                onClick={() => {
                  setSelectedStatus('Low Stock');
                  setActiveTab('current');
                }}
                className="bg-white border border-amber-200 rounded-lg p-3.5 shadow-2xs hover:border-amber-400 hover:shadow-xs transition-all cursor-pointer flex flex-col justify-between"
              >
                <div className="flex items-center justify-between text-amber-700">
                  <span className="text-xs font-bold uppercase tracking-wider">Below Reorder Level</span>
                  <AlertTriangle className="w-4 h-4 text-amber-600" />
                </div>
                <div className="mt-2">
                  <span className="text-2xl font-bold text-amber-700">
                    {metrics.lowStockCount.toLocaleString()}
                  </span>
                  <span className="text-xs text-amber-600 ml-1.5 font-normal">needs reorder</span>
                </div>
                <div className="text-[11px] text-amber-700 mt-2 font-medium flex items-center gap-1">
                  Filter Low Stock Items <ArrowRight className="w-3 h-3" />
                </div>
              </div>
            </div>

            {/* Financial Valuation Summary Banner */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3.5">
              <div className="bg-[#f0f9ff] border border-blue-200 rounded-lg p-4">
                <div className="text-xs font-semibold text-blue-900 uppercase tracking-wider">
                  Total Valuation @ Purchase Cost
                </div>
                <div className="text-2xl font-bold text-blue-950 mt-1">
                  ₹{metrics.stockValueAtCost.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </div>
                <div className="text-[11px] text-blue-700 mt-1 font-medium">
                  Actual acquisition expenditure in inventory
                </div>
              </div>

              <div className="bg-[#f0fdf4] border border-emerald-200 rounded-lg p-4">
                <div className="text-xs font-semibold text-emerald-900 uppercase tracking-wider">
                  Total Valuation @ Selling Price
                </div>
                <div className="text-2xl font-bold text-emerald-950 mt-1">
                  ₹{metrics.stockValueAtSale.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </div>
                <div className="text-[11px] text-emerald-700 mt-1 font-medium">
                  Expected revenue at current retail price
                </div>
              </div>

              <div className="bg-[#faf5ff] border border-purple-200 rounded-lg p-4">
                <div className="text-xs font-semibold text-purple-900 uppercase tracking-wider">
                  Potential Gross Margin
                </div>
                <div className="text-2xl font-bold text-purple-950 mt-1">
                  ₹{metrics.potentialGrossMargin.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </div>
                <div className="text-[11px] text-purple-700 mt-1 font-medium">
                  {metrics.stockValueAtSale > 0
                    ? ((metrics.potentialGrossMargin / metrics.stockValueAtSale) * 100).toFixed(1) +
                      '% estimated retail gross margin'
                    : '0% margin'}
                </div>
              </div>
            </div>

            {/* Quick Actions Bar */}
            <div className="bg-white border border-slate-200 rounded-lg p-3.5 flex items-center justify-between flex-wrap gap-2">
              <span className="text-xs font-bold text-slate-700 flex items-center gap-1.5">
                <Sliders className="w-4 h-4 text-blue-600" />
                Quick Stock Actions:
              </span>
              <div className="flex items-center gap-2 flex-wrap">
                <button
                  onClick={() => {
                    setIsAdjModalOpen(true);
                    setAdjModalMode('create');
                    resetAdjForm();
                  }}
                  className="px-3 py-1.5 text-xs bg-[#1f4e79] hover:bg-[#163959] text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
                >
                  <Plus className="w-3.5 h-3.5" />
                  New Stock Adjustment
                </button>
                <button
                  onClick={handleOpenNewVerification}
                  className="px-3 py-1.5 text-xs bg-emerald-700 hover:bg-emerald-800 text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
                >
                  <ClipboardCheck className="w-3.5 h-3.5" />
                  Start Physical Audit
                </button>
                <button
                  onClick={handleExportStockExcel}
                  className="px-3 py-1.5 text-xs bg-slate-700 hover:bg-slate-800 text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
                >
                  <Download className="w-3.5 h-3.5" />
                  Export Stock (.xlsx)
                </button>
                {onNavigateToInbound && (
                  <button
                    onClick={onNavigateToInbound}
                    className="px-3 py-1.5 text-xs bg-blue-50 text-blue-800 border border-blue-200 hover:bg-blue-100 rounded font-medium flex items-center gap-1.5 cursor-pointer"
                  >
                    Receive Inbound Goods (F3)
                  </button>
                )}
              </div>
            </div>

            {/* Recent Stock Movements Table */}
            <div className="bg-white border border-slate-200 rounded-lg p-4 shadow-2xs">
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-xs font-bold uppercase tracking-wider text-slate-700 flex items-center gap-1.5">
                  <History className="w-4 h-4 text-blue-600" />
                  Recent Stock Movements Audit Trail
                </h3>
                <button
                  onClick={() => setActiveTab('ledger')}
                  className="text-xs text-blue-600 font-medium hover:underline flex items-center gap-1"
                >
                  View Full Movement Ledger <ArrowRight className="w-3 h-3" />
                </button>
              </div>

              <div className="border border-slate-200 rounded overflow-hidden">
                <table className="w-full text-left text-xs border-collapse">
                  <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-200">
                    <tr>
                      <th className="py-2 px-3">Date & Time</th>
                      <th className="py-2 px-3">Reference</th>
                      <th className="py-2 px-3">Product Name</th>
                      <th className="py-2 px-3">Movement Type</th>
                      <th className="py-2 px-3 text-right">Qty Before</th>
                      <th className="py-2 px-3 text-right">Quantity Change</th>
                      <th className="py-2 px-3 text-right">Qty After</th>
                      <th className="py-2 px-3">Audit Details</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {metrics.recentMovements.length === 0 ? (
                      <tr>
                        <td colSpan={8} className="py-6 text-center text-slate-400">
                          No stock movements recorded yet. Movements are generated automatically from Purchase Receipts, Reversals, and Adjustments.
                        </td>
                      </tr>
                    ) : (
                      metrics.recentMovements.map((m) => (
                        <tr key={m.id} className="hover:bg-slate-50 transition-colors">
                          <td className="py-2 px-3 font-mono text-[11px] text-slate-500">
                            {new Date(m.createdAt).toLocaleString()}
                          </td>
                          <td className="py-2 px-3 font-semibold text-blue-900">{m.referenceId}</td>
                          <td className="py-2 px-3 text-slate-800 font-medium">
                            <span className="text-slate-400 mr-1 font-mono text-[11px]">{m.productCode}</span>
                            {m.productName}
                          </td>
                          <td className="py-2 px-3">
                            <span
                              className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                                m.referenceType === 'PURCHASE_CONFIRM'
                                  ? 'bg-emerald-100 text-emerald-800'
                                  : m.referenceType === 'PURCHASE_CANCEL'
                                  ? 'bg-red-100 text-red-800'
                                  : m.referenceType === 'ADJUSTMENT'
                                  ? 'bg-purple-100 text-purple-800'
                                  : 'bg-blue-100 text-blue-800'
                              }`}
                            >
                              {m.referenceType}
                            </span>
                          </td>
                          <td className="py-2 px-3 text-right font-mono text-slate-600">
                            {m.quantityBefore}
                          </td>
                          <td
                            className={`py-2 px-3 text-right font-mono font-bold ${
                              m.quantity > 0 ? 'text-emerald-700' : 'text-red-700'
                            }`}
                          >
                            {m.quantity > 0 ? `+${m.quantity}` : m.quantity}
                          </td>
                          <td className="py-2 px-3 text-right font-mono font-bold text-slate-800">
                            {m.quantityAfter}
                          </td>
                          <td className="py-2 px-3 text-slate-600 truncate max-w-xs">{m.notes || '—'}</td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        )}

        {/* ============================================================== */}
        {/* TAB 2: CURRENT STOCK INVENTORY (DATAGRIDVIEW) */}
        {/* ============================================================== */}
        {activeTab === 'current' && (
          <div className="flex-1 flex flex-col overflow-hidden bg-white">
            {/* Filter Toolbar */}
            <div className="bg-[#f5f8fa] border-b border-slate-200 px-4 py-2.5 flex items-center justify-between flex-wrap gap-2 shrink-0">
              <div className="flex items-center gap-2 flex-wrap">
                {/* Search Box */}
                <div className="relative w-64">
                  <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" />
                  <input
                    type="text"
                    value={searchTerm}
                    onChange={(e) => {
                      setSearchTerm(e.target.value);
                      setCurrentPage(1);
                    }}
                    placeholder="Search by Code, Barcode, Name..."
                    className="w-full pl-8 pr-3 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                  />
                  {searchTerm && (
                    <button
                      onClick={() => setSearchTerm('')}
                      className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 text-xs"
                    >
                      ×
                    </button>
                  )}
                </div>

                {/* Category Dropdown */}
                <select
                  value={selectedCategory}
                  onChange={(e) => {
                    setSelectedCategory(Number(e.target.value));
                    setCurrentPage(1);
                  }}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                >
                  <option value={0}>All Categories</option>
                  {categories.map((c: Category) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>

                {/* Brand Dropdown */}
                <select
                  value={selectedBrand}
                  onChange={(e) => {
                    setSelectedBrand(Number(e.target.value));
                    setCurrentPage(1);
                  }}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                >
                  <option value={0}>All Brands</option>
                  {brands.map((b: Brand) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
                </select>

                {/* Stock Status Dropdown */}
                <select
                  value={selectedStatus}
                  onChange={(e) => {
                    setSelectedStatus(e.target.value);
                    setCurrentPage(1);
                  }}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden font-medium"
                >
                  <option value="All">All Stock Levels</option>
                  <option value="In Stock">In Stock (Healthy)</option>
                  <option value="Low Stock">Low Stock (≤ Reorder Level)</option>
                  <option value="Out of Stock">Out of Stock (Zero)</option>
                  <option value="Excess Stock">Excess Stock (&gt; Max)</option>
                </select>

                {(searchTerm || selectedCategory > 0 || selectedBrand > 0 || selectedStatus !== 'All') && (
                  <button
                    onClick={() => {
                      setSearchTerm('');
                      setSelectedCategory(0);
                      setSelectedBrand(0);
                      setSelectedStatus('All');
                      setCurrentPage(1);
                    }}
                    className="px-2.5 py-1.5 text-xs text-slate-500 hover:text-slate-800 flex items-center gap-1 cursor-pointer"
                  >
                    <RotateCcw className="w-3 h-3" /> Reset Filters
                  </button>
                )}
              </div>

              {/* Action Buttons */}
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    setIsAdjModalOpen(true);
                    setAdjModalMode('create');
                    resetAdjForm();
                  }}
                  className="px-3 py-1.5 text-xs bg-[#1f4e79] hover:bg-[#163959] text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
                >
                  <Plus className="w-3.5 h-3.5" />
                  Adjust Stock
                </button>
                <button
                  onClick={handleExportStockExcel}
                  className="px-3 py-1.5 text-xs bg-emerald-700 hover:bg-emerald-800 text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
                >
                  <Download className="w-3.5 h-3.5" />
                  Export .xlsx
                </button>
              </div>
            </div>

            {/* DataGridView Table */}
            <div className="flex-1 overflow-auto bg-white">
              <table className="w-full text-left text-xs border-collapse">
                <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-300 sticky top-0 z-10 shadow-2xs">
                  <tr>
                    <th className="py-2.5 px-3">Code / SKU</th>
                    <th className="py-2.5 px-3">Barcode</th>
                    <th className="py-2.5 px-3">Product Name</th>
                    <th className="py-2.5 px-3">Category</th>
                    <th className="py-2.5 px-3 text-right">Opening</th>
                    <th className="py-2.5 px-3 text-right">Purchased</th>
                    <th className="py-2.5 px-3 text-right">Free Qty</th>
                    <th className="py-2.5 px-3 text-right">Adjustments</th>
                    <th className="py-2.5 px-3 text-right bg-blue-50/70 border-x border-blue-200 font-bold text-blue-900">
                      Current Stock
                    </th>
                    <th className="py-2.5 px-3 text-right">Reorder Level</th>
                    <th className="py-2.5 px-3 text-right">Cost Rate (₹)</th>
                    <th className="py-2.5 px-3 text-right">Stock Value (₹)</th>
                    <th className="py-2.5 px-3 text-center">Status</th>
                    <th className="py-2.5 px-3 text-center">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-sans">
                  {currentStockData.items.length === 0 ? (
                    <tr>
                      <td colSpan={14} className="py-12 text-center text-slate-400">
                        No products match the selected criteria.
                      </td>
                    </tr>
                  ) : (
                    currentStockData.items.map((row) => (
                      <tr
                        key={row.productId}
                        className={`hover:bg-blue-50/40 transition-colors ${
                          row.currentStock <= 0
                            ? 'bg-red-50/30'
                            : row.currentStock <= row.reorderLevel
                            ? 'bg-amber-50/30'
                            : ''
                        }`}
                      >
                        <td className="py-2 px-3 font-mono text-[11px] font-semibold text-blue-900">
                          {row.productCode}
                        </td>
                        <td className="py-2 px-3 font-mono text-[11px] text-slate-500">{row.barcode}</td>
                        <td className="py-2 px-3 font-medium text-slate-800">{row.productName}</td>
                        <td className="py-2 px-3 text-slate-600">{row.categoryName}</td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">{row.openingStock}</td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">{row.purchasedQuantity}</td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">{row.freeQuantityReceived}</td>
                        <td
                          className={`py-2 px-3 text-right font-mono font-medium ${
                            row.adjustmentQuantity > 0
                              ? 'text-emerald-700'
                              : row.adjustmentQuantity < 0
                              ? 'text-red-700'
                              : 'text-slate-400'
                          }`}
                        >
                          {row.adjustmentQuantity > 0
                            ? `+${row.adjustmentQuantity}`
                            : row.adjustmentQuantity}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold text-sm bg-blue-50/50 border-x border-blue-100 text-blue-950">
                          {row.currentStock}{' '}
                          <span className="text-[10px] text-slate-500 font-normal">{row.unit}</span>
                        </td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">{row.reorderLevel}</td>
                        <td className="py-2 px-3 text-right font-mono text-slate-700">
                          ₹{row.purchaseRate.toFixed(2)}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-semibold text-slate-900">
                          ₹{row.stockValuePurchase.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="py-2 px-3 text-center">
                          <span
                            className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                              row.stockStatus === 'Out of Stock'
                                ? 'bg-red-100 text-red-800 border border-red-200'
                                : row.stockStatus === 'Low Stock'
                                ? 'bg-amber-100 text-amber-800 border border-amber-200'
                                : row.stockStatus === 'Excess Stock'
                                ? 'bg-purple-100 text-purple-800 border border-purple-200'
                                : 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                            }`}
                          >
                            {row.stockStatus}
                          </span>
                        </td>
                        <td className="py-2 px-3 text-center">
                          <div className="flex items-center justify-center gap-1">
                            <button
                              onClick={() => {
                                setLedgerProductId(row.productId);
                                setActiveTab('ledger');
                              }}
                              className="px-2 py-0.5 text-[11px] bg-slate-100 hover:bg-slate-200 text-slate-700 rounded font-medium cursor-pointer"
                              title="View Product Movement Ledger"
                            >
                              Ledger
                            </button>
                            <button
                              onClick={() => {
                                const prod = allProducts.find((p) => p.id === row.productId);
                                if (prod) {
                                  resetAdjForm();
                                  setAdjReason('Manual inventory adjustment');
                                  handleAddAdjustmentItem(prod);
                                  setIsAdjModalOpen(true);
                                  setAdjModalMode('create');
                                }
                              }}
                              className="px-2 py-0.5 text-[11px] bg-blue-50 hover:bg-blue-100 text-blue-700 rounded font-medium cursor-pointer"
                              title="Adjust stock quantity"
                            >
                              Adjust
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination & Performance Benchmark Bar */}
            <div className="bg-[#f0f4f8] border-t border-slate-300 px-4 py-2 flex items-center justify-between text-xs text-slate-600 shrink-0">
              <div className="flex items-center gap-2">
                <span>
                  Showing {Math.min(currentStockData.totalCount, (currentPage - 1) * pageSize + 1)}-
                  {Math.min(currentStockData.totalCount, currentPage * pageSize)} of{' '}
                  <span className="font-bold text-slate-800">
                    {currentStockData.totalCount.toLocaleString()}
                  </span>{' '}
                  products
                </span>
                <span className="text-slate-300">|</span>
                <span className="text-[11px] text-emerald-700 font-mono font-medium">
                  Query benchmark: {currentStockData.executionTimeMs} ms (Indexed SQLite Engine)
                </span>
              </div>

              <div className="flex items-center gap-1.5">
                <button
                  disabled={currentPage <= 1}
                  onClick={() => setCurrentPage(1)}
                  className="px-2 py-1 bg-white border border-slate-300 rounded disabled:opacity-40 hover:bg-slate-50 cursor-pointer text-xs"
                >
                  « First
                </button>
                <button
                  disabled={currentPage <= 1}
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                  className="px-2 py-1 bg-white border border-slate-300 rounded disabled:opacity-40 hover:bg-slate-50 cursor-pointer text-xs"
                >
                  ‹ Prev
                </button>
                <span className="px-2 font-medium text-slate-700">
                  Page {currentPage} of {Math.max(1, currentStockData.totalPages)}
                </span>
                <button
                  disabled={currentPage >= currentStockData.totalPages}
                  onClick={() => setCurrentPage((p) => Math.min(currentStockData.totalPages, p + 1))}
                  className="px-2 py-1 bg-white border border-slate-300 rounded disabled:opacity-40 hover:bg-slate-50 cursor-pointer text-xs"
                >
                  Next ›
                </button>
                <button
                  disabled={currentPage >= currentStockData.totalPages}
                  onClick={() => setCurrentPage(currentStockData.totalPages)}
                  className="px-2 py-1 bg-white border border-slate-300 rounded disabled:opacity-40 hover:bg-slate-50 cursor-pointer text-xs"
                >
                  Last »
                </button>
              </div>
            </div>
          </div>
        )}

        {/* ============================================================== */}
        {/* TAB 3: STOCK MOVEMENT LEDGER */}
        {/* ============================================================== */}
        {activeTab === 'ledger' && (
          <div className="flex-1 flex flex-col overflow-hidden bg-white">
            {/* Filter Bar */}
            <div className="bg-[#f5f8fa] border-b border-slate-200 px-4 py-2.5 flex items-center justify-between flex-wrap gap-2 shrink-0">
              <div className="flex items-center gap-2 flex-wrap">
                {/* Product Filter */}
                <select
                  value={ledgerProductId}
                  onChange={(e) => setLedgerProductId(Number(e.target.value))}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden max-w-xs"
                >
                  <option value={0}>All Products</option>
                  {allProducts.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.productCode} — {p.nameEn}
                    </option>
                  ))}
                </select>

                {/* Movement Type Filter */}
                <select
                  value={ledgerType}
                  onChange={(e) => setLedgerType(e.target.value)}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                >
                  <option value="All">All Movement Types</option>
                  <option value="PURCHASE_CONFIRM">Purchase Receipt (Inbound)</option>
                  <option value="PURCHASE_CANCEL">Purchase Reversal</option>
                  <option value="ADJUSTMENT">Stock Adjustment / Audit</option>
                  <option value="OPENING_STOCK">Opening Stock</option>
                </select>

                {/* Date From */}
                <input
                  type="date"
                  value={ledgerDateFrom}
                  onChange={(e) => setLedgerDateFrom(e.target.value)}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                  title="Date From"
                />

                {/* Date To */}
                <input
                  type="date"
                  value={ledgerDateTo}
                  onChange={(e) => setLedgerDateTo(e.target.value)}
                  className="px-2.5 py-1.5 text-xs bg-white border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                  title="Date To"
                />

                {(ledgerProductId > 0 || ledgerType !== 'All' || ledgerDateFrom || ledgerDateTo) && (
                  <button
                    onClick={() => {
                      setLedgerProductId(0);
                      setLedgerType('All');
                      setLedgerDateFrom('');
                      setLedgerDateTo('');
                    }}
                    className="px-2.5 py-1.5 text-xs text-slate-500 hover:text-slate-800 flex items-center gap-1 cursor-pointer"
                  >
                    <RotateCcw className="w-3 h-3" /> Clear Filters
                  </button>
                )}
              </div>

              <div className="text-xs text-slate-500 font-medium">
                {filteredMovements.length} total movement records recorded
              </div>
            </div>

            {/* Ledger Grid */}
            <div className="flex-1 overflow-auto bg-white">
              <table className="w-full text-left text-xs border-collapse">
                <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-300 sticky top-0 z-10 shadow-2xs">
                  <tr>
                    <th className="py-2.5 px-3">Date & Time</th>
                    <th className="py-2.5 px-3">Reference Voucher</th>
                    <th className="py-2.5 px-3">Product Code</th>
                    <th className="py-2.5 px-3">Product Name</th>
                    <th className="py-2.5 px-3">Movement Type</th>
                    <th className="py-2.5 px-3 text-right">Balance Before</th>
                    <th className="py-2.5 px-3 text-right font-bold">Qty Change</th>
                    <th className="py-2.5 px-3 text-right font-bold bg-blue-50/50">Balance After</th>
                    <th className="py-2.5 px-3">Audit Details / Reason</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-sans">
                  {filteredMovements.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="py-12 text-center text-slate-400">
                        No stock ledger records found for the selected filter.
                      </td>
                    </tr>
                  ) : (
                    filteredMovements.map((m) => (
                      <tr key={m.id} className="hover:bg-slate-50 transition-colors">
                        <td className="py-2 px-3 font-mono text-[11px] text-slate-500">
                          {new Date(m.createdAt).toLocaleString()}
                        </td>
                        <td className="py-2 px-3 font-semibold text-blue-900">{m.referenceId}</td>
                        <td className="py-2 px-3 font-mono text-[11px] text-slate-600">{m.productCode}</td>
                        <td className="py-2 px-3 font-medium text-slate-800">{m.productName}</td>
                        <td className="py-2 px-3">
                          <span
                            className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                              m.referenceType === 'PURCHASE_CONFIRM'
                                ? 'bg-emerald-100 text-emerald-800'
                                : m.referenceType === 'PURCHASE_CANCEL'
                                ? 'bg-red-100 text-red-800'
                                : m.referenceType === 'ADJUSTMENT'
                                ? 'bg-purple-100 text-purple-800'
                                : 'bg-blue-100 text-blue-800'
                            }`}
                          >
                            {m.referenceType}
                          </span>
                        </td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">{m.quantityBefore}</td>
                        <td
                          className={`py-2 px-3 text-right font-mono font-bold ${
                            m.quantity > 0 ? 'text-emerald-700' : 'text-red-700'
                          }`}
                        >
                          {m.quantity > 0 ? `+${m.quantity}` : m.quantity}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold bg-blue-50/50 text-blue-950">
                          {m.quantityAfter}
                        </td>
                        <td className="py-2 px-3 text-slate-600">{m.notes || '—'}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* ============================================================== */}
        {/* TAB 4: STOCK ADJUSTMENTS */}
        {/* ============================================================== */}
        {activeTab === 'adjustments' && (
          <div className="flex-1 flex flex-col overflow-hidden bg-white">
            {/* Action Bar */}
            <div className="bg-[#f5f8fa] border-b border-slate-200 px-4 py-2.5 flex items-center justify-between shrink-0">
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-slate-700">Stock Adjustment Vouchers</span>
                <span className="text-xs text-slate-500">
                  (Physical correction, damages, wastage, expired goods)
                </span>
              </div>
              <button
                onClick={() => {
                  setIsAdjModalOpen(true);
                  setAdjModalMode('create');
                  resetAdjForm();
                }}
                className="px-3 py-1.5 text-xs bg-[#1f4e79] hover:bg-[#163959] text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
              >
                <Plus className="w-3.5 h-3.5" />
                New Stock Adjustment (F8)
              </button>
            </div>

            {/* Adjustments Table */}
            <div className="flex-1 overflow-auto bg-white">
              <table className="w-full text-left text-xs border-collapse">
                <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-300 sticky top-0 z-10 shadow-2xs">
                  <tr>
                    <th className="py-2.5 px-3">Adjustment #</th>
                    <th className="py-2.5 px-3">Date</th>
                    <th className="py-2.5 px-3">Reason</th>
                    <th className="py-2.5 px-3 text-right">Items Count</th>
                    <th className="py-2.5 px-3 text-right">Net Qty Diff</th>
                    <th className="py-2.5 px-3 text-right">Cost Impact (₹)</th>
                    <th className="py-2.5 px-3 text-center">Status</th>
                    <th className="py-2.5 px-3">Created By</th>
                    <th className="py-2.5 px-3 text-center">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-sans">
                  {adjustments.length === 0 ? (
                    <tr>
                      <td colSpan={9} className="py-12 text-center text-slate-400">
                        No stock adjustments entered yet. Click &quot;New Stock Adjustment&quot; to begin.
                      </td>
                    </tr>
                  ) : (
                    adjustments.map((adj) => (
                      <tr key={adj.id} className="hover:bg-slate-50 transition-colors">
                        <td className="py-2 px-3 font-mono font-semibold text-blue-900">
                          {adj.adjustmentNumber}
                        </td>
                        <td className="py-2 px-3 font-mono text-[11px] text-slate-500">
                          {new Date(adj.adjustmentDate).toLocaleDateString()}
                        </td>
                        <td className="py-2 px-3 font-medium text-slate-800">{adj.reason}</td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">{adj.totalItems}</td>
                        <td
                          className={`py-2 px-3 text-right font-mono font-bold ${
                            adj.totalDifferenceQuantity > 0
                              ? 'text-emerald-700'
                              : adj.totalDifferenceQuantity < 0
                              ? 'text-red-700'
                              : 'text-slate-600'
                          }`}
                        >
                          {adj.totalDifferenceQuantity > 0
                            ? `+${adj.totalDifferenceQuantity}`
                            : adj.totalDifferenceQuantity}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-semibold text-slate-900">
                          ₹{adj.totalCostImpact.toFixed(2)}
                        </td>
                        <td className="py-2 px-3 text-center">
                          <span
                            className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                              adj.status === 'Confirmed'
                                ? 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                                : adj.status === 'Draft'
                                ? 'bg-amber-100 text-amber-800 border border-amber-200'
                                : 'bg-red-100 text-red-800 border border-red-200'
                            }`}
                          >
                            {adj.status}
                          </span>
                        </td>
                        <td className="py-2 px-3 text-slate-500">{adj.createdBy || 'Admin'}</td>
                        <td className="py-2 px-3 text-center">
                          <div className="flex items-center justify-center gap-1.5">
                            {adj.status === 'Draft' && (
                              <button
                                onClick={() => handleConfirmDraftAdjustment(adj.id)}
                                className="px-2 py-0.5 text-[11px] bg-emerald-600 hover:bg-emerald-700 text-white rounded font-medium cursor-pointer"
                              >
                                Confirm
                              </button>
                            )}
                            {adj.status === 'Confirmed' && (
                              <button
                                onClick={() => handleCancelAdjustment(adj.id)}
                                className="px-2 py-0.5 text-[11px] bg-red-50 hover:bg-red-100 text-red-700 rounded font-medium cursor-pointer"
                              >
                                Revert
                              </button>
                            )}
                            <button
                              onClick={() => {
                                setSelectedAdjustment(adj);
                                setIsAdjModalOpen(true);
                                setAdjModalMode('view');
                              }}
                              className="px-2 py-0.5 text-[11px] bg-slate-100 hover:bg-slate-200 text-slate-700 rounded font-medium cursor-pointer"
                            >
                              View Items
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* ============================================================== */}
        {/* TAB 5: PHYSICAL STOCK AUDIT / VERIFICATION */}
        {/* ============================================================== */}
        {activeTab === 'verification' && (
          <div className="flex-1 flex flex-col overflow-hidden bg-white">
            <div className="bg-[#f5f8fa] border-b border-slate-200 px-4 py-2.5 flex items-center justify-between shrink-0">
              <div>
                <span className="text-xs font-bold text-slate-700">Physical Stock Count & Audit Sessions</span>
                <p className="text-[11px] text-slate-500">
                  Compare recorded system stock with physical shelf count and reconcile discrepancies.
                </p>
              </div>
              <button
                onClick={handleOpenNewVerification}
                className="px-3 py-1.5 text-xs bg-emerald-700 hover:bg-emerald-800 text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
              >
                <Plus className="w-3.5 h-3.5" />
                Start Physical Audit Session
              </button>
            </div>

            <div className="flex-1 overflow-auto bg-white">
              <table className="w-full text-left text-xs border-collapse">
                <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-300 sticky top-0 z-10 shadow-2xs">
                  <tr>
                    <th className="py-2.5 px-3">Audit #</th>
                    <th className="py-2.5 px-3">Audit Date</th>
                    <th className="py-2.5 px-3">Category Scope</th>
                    <th className="py-2.5 px-3 text-right">Products Counted</th>
                    <th className="py-2.5 px-3 text-right font-bold text-red-700">Discrepancies Found</th>
                    <th className="py-2.5 px-3 text-right">Net Difference Qty</th>
                    <th className="py-2.5 px-3 text-center">Status</th>
                    <th className="py-2.5 px-3">Notes</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-sans">
                  {verifications.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="py-12 text-center text-slate-400">
                        No physical stock verification sessions executed yet.
                      </td>
                    </tr>
                  ) : (
                    verifications.map((ver) => (
                      <tr key={ver.id} className="hover:bg-slate-50 transition-colors">
                        <td className="py-2 px-3 font-mono font-semibold text-blue-900">
                          {ver.verificationNumber}
                        </td>
                        <td className="py-2 px-3 font-mono text-[11px] text-slate-500">
                          {new Date(ver.verificationDate).toLocaleDateString()}
                        </td>
                        <td className="py-2 px-3 text-slate-700">{ver.categoryName || 'All Categories'}</td>
                        <td className="py-2 px-3 text-right font-mono text-slate-600">
                          {ver.totalCountedProducts}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold text-red-700">
                          {ver.totalDiscrepancies}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold text-slate-800">
                          {ver.netDifferenceQuantity > 0
                            ? `+${ver.netDifferenceQuantity}`
                            : ver.netDifferenceQuantity}
                        </td>
                        <td className="py-2 px-3 text-center">
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
                            {ver.status}
                          </span>
                        </td>
                        <td className="py-2 px-3 text-slate-600">{ver.notes || '—'}</td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* ============================================================== */}
        {/* TAB 6: STOCK VALUATION & MARGIN */}
        {/* ============================================================== */}
        {activeTab === 'valuation' && (
          <div className="flex-1 flex flex-col overflow-hidden bg-white">
            {/* Header Toolbar */}
            <div className="bg-[#f5f8fa] border-b border-slate-200 px-4 py-2.5 flex items-center justify-between shrink-0">
              <div>
                <span className="text-xs font-bold text-slate-700">Stock Valuation & Profit Margin Report</span>
                <span className="text-xs text-slate-500 ml-2">
                  (FIFO Equivalent / Weighted Purchase Cost)
                </span>
              </div>
              <button
                onClick={handleExportValuationExcel}
                className="px-3 py-1.5 text-xs bg-emerald-700 hover:bg-emerald-800 text-white rounded font-medium flex items-center gap-1.5 cursor-pointer shadow-2xs"
              >
                <Download className="w-3.5 h-3.5" />
                Export Valuation (.xlsx)
              </button>
            </div>

            {/* Valuation Grid */}
            <div className="flex-1 overflow-auto bg-white">
              <table className="w-full text-left text-xs border-collapse">
                <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-300 sticky top-0 z-10 shadow-2xs">
                  <tr>
                    <th className="py-2.5 px-3">Product Code</th>
                    <th className="py-2.5 px-3">Barcode</th>
                    <th className="py-2.5 px-3">Product Name</th>
                    <th className="py-2.5 px-3">Category</th>
                    <th className="py-2.5 px-3 text-right">Stock Qty</th>
                    <th className="py-2.5 px-3 text-right">Cost Rate (₹)</th>
                    <th className="py-2.5 px-3 text-right font-bold bg-blue-50/50">Total Cost (₹)</th>
                    <th className="py-2.5 px-3 text-right">Selling Price (₹)</th>
                    <th className="py-2.5 px-3 text-right font-bold bg-emerald-50/50">Total Retail (₹)</th>
                    <th className="py-2.5 px-3 text-right font-bold text-purple-900">Gross Margin (₹)</th>
                    <th className="py-2.5 px-3 text-right">Margin %</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 font-sans">
                  {valuationData.map((v) => {
                    const marginPct =
                      v.stockValueSale > 0
                        ? ((v.potentialMargin / v.stockValueSale) * 100).toFixed(1)
                        : '0.0';
                    return (
                      <tr key={v.productId} className="hover:bg-slate-50 transition-colors">
                        <td className="py-2 px-3 font-mono text-[11px] font-semibold text-blue-900">
                          {v.productCode}
                        </td>
                        <td className="py-2 px-3 font-mono text-[11px] text-slate-500">{v.barcode}</td>
                        <td className="py-2 px-3 font-medium text-slate-800">{v.productName}</td>
                        <td className="py-2 px-3 text-slate-600">{v.categoryName}</td>
                        <td className="py-2 px-3 text-right font-mono font-bold text-slate-800">
                          {v.currentStock}
                        </td>
                        <td className="py-2 px-3 text-right font-mono text-slate-700">
                          ₹{v.purchaseRate.toFixed(2)}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold bg-blue-50/40 text-blue-950">
                          ₹{v.stockValueCost.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="py-2 px-3 text-right font-mono text-slate-700">
                          ₹{v.saleRate.toFixed(2)}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold bg-emerald-50/40 text-emerald-950">
                          ₹{v.stockValueSale.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="py-2 px-3 text-right font-mono font-bold text-purple-950">
                          ₹{v.potentialMargin.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="py-2 px-3 text-right font-mono text-slate-700 font-medium">
                          {marginPct}%
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {/* Valuation Summary Footer */}
            <div className="bg-[#f0f4f8] border-t border-slate-300 px-4 py-2.5 flex items-center justify-between text-xs font-bold text-slate-700 shrink-0">
              <span>Grand Total Inventory Valuation</span>
              <div className="flex items-center gap-6">
                <span>
                  Total Cost: ₹{metrics.stockValueAtCost.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
                <span className="text-emerald-800">
                  Total Retail Value: ₹
                  {metrics.stockValueAtSale.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
                <span className="text-purple-800">
                  Potential Profit: ₹
                  {metrics.potentialGrossMargin.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ============================================================== */}
      {/* MODAL: NEW / VIEW STOCK ADJUSTMENT */}
      {/* ============================================================== */}
      {isAdjModalOpen && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col border border-slate-300">
            {/* Header */}
            <div className="bg-[#1f4e79] text-white px-4 py-2.5 rounded-t-lg flex items-center justify-between">
              <h2 className="text-sm font-bold flex items-center gap-2">
                <Sliders className="w-4 h-4 text-blue-200" />
                {adjModalMode === 'create'
                  ? 'Record New Stock Adjustment Voucher'
                  : `Adjustment Voucher: ${selectedAdjustment?.adjustmentNumber}`}
              </h2>
              <button
                onClick={() => setIsAdjModalOpen(false)}
                className="text-white/80 hover:text-white text-lg font-bold cursor-pointer"
              >
                ✕
              </button>
            </div>

            {/* Modal Body */}
            <div className="p-4 overflow-y-auto flex-1 space-y-4">
              {adjError && (
                <div className="p-3 bg-red-50 border border-red-200 rounded text-xs text-red-700 flex items-center gap-2">
                  <AlertTriangle className="w-4 h-4 text-red-600 shrink-0" />
                  <span>{adjError}</span>
                </div>
              )}

              {adjModalMode === 'create' ? (
                <>
                  {/* Adjustment Fields */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                    <div>
                      <label className="block text-slate-700 font-semibold mb-1">
                        Adjustment Reason <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        value={adjReason}
                        onChange={(e) => setAdjReason(e.target.value)}
                        placeholder="e.g. Physical count discrepancy, Damaged boxes, Expired biscuits"
                        className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                      />
                    </div>
                    <div>
                      <label className="block text-slate-700 font-semibold mb-1">Notes / Approval</label>
                      <input
                        type="text"
                        value={adjNotes}
                        onChange={(e) => setAdjNotes(e.target.value)}
                        placeholder="e.g. Approved by Store Manager, Audit Team 1"
                        className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                      />
                    </div>
                  </div>

                  {/* Add Product Search Input */}
                  <div className="relative">
                    <label className="block text-xs text-slate-700 font-semibold mb-1">
                      Search & Add Products to Adjustment
                    </label>
                    <div className="relative">
                      <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" />
                      <input
                        type="text"
                        value={adjProductSearch}
                        onChange={(e) => setAdjProductSearch(e.target.value)}
                        placeholder="Type Product Code, Name, or scan barcode..."
                        className="w-full pl-8 pr-3 py-1.5 text-xs border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                      />
                    </div>

                    {adjProductCandidates.length > 0 && (
                      <div className="absolute left-0 right-0 top-full mt-1 bg-white border border-slate-300 rounded shadow-lg z-20 max-h-48 overflow-y-auto">
                        {adjProductCandidates.map((p) => (
                          <div
                            key={p.id}
                            onClick={() => handleAddAdjustmentItem(p)}
                            className="px-3 py-2 hover:bg-blue-50 cursor-pointer text-xs flex justify-between items-center border-b border-slate-100 last:border-0"
                          >
                            <div>
                              <span className="font-semibold text-slate-800">{p.nameEn}</span>
                              <span className="text-slate-400 font-mono ml-2">({p.productCode})</span>
                            </div>
                            <div className="text-slate-600 font-mono">
                              Current Stock: <span className="font-bold">{p.currentStock}</span>
                            </div>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Adjustment Items Table */}
                  <div className="border border-slate-200 rounded overflow-hidden">
                    <table className="w-full text-left text-xs border-collapse">
                      <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-200">
                        <tr>
                          <th className="py-2 px-2.5">Product</th>
                          <th className="py-2 px-2.5 text-right">System Stock</th>
                          <th className="py-2 px-2.5 text-right">Physical Count</th>
                          <th className="py-2 px-2.5 text-right font-bold">Difference</th>
                          <th className="py-2 px-2.5">Adjustment Type</th>
                          <th className="py-2 px-2.5 text-right">Cost Rate</th>
                          <th className="py-2 px-2.5 text-center">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {adjItems.length === 0 ? (
                          <tr>
                            <td colSpan={7} className="py-6 text-center text-slate-400">
                              No products added to this adjustment yet. Search above to add items.
                            </td>
                          </tr>
                        ) : (
                          adjItems.map((item, idx) => (
                            <tr key={item.id} className="hover:bg-slate-50">
                              <td className="py-2 px-2.5">
                                <div className="font-medium text-slate-800">{item.productName}</div>
                                <div className="text-[10px] text-slate-400 font-mono">{item.productCode}</div>
                              </td>
                              <td className="py-2 px-2.5 text-right font-mono text-slate-600">
                                {item.currentStock}
                              </td>
                              <td className="py-2 px-2.5 text-right">
                                <input
                                  type="number"
                                  min="0"
                                  step="any"
                                  value={item.physicalCount}
                                  onChange={(e) =>
                                    handleUpdateAdjItem(idx, 'physicalCount', e.target.value)
                                  }
                                  className="w-20 text-right px-1.5 py-0.5 border border-slate-300 rounded font-mono text-xs focus:border-blue-500 focus:outline-hidden"
                                />
                              </td>
                              <td
                                className={`py-2 px-2.5 text-right font-mono font-bold ${
                                  item.differenceQuantity > 0
                                    ? 'text-emerald-700'
                                    : item.differenceQuantity < 0
                                    ? 'text-red-700'
                                    : 'text-slate-600'
                                }`}
                              >
                                {item.differenceQuantity > 0
                                  ? `+${item.differenceQuantity}`
                                  : item.differenceQuantity}
                              </td>
                              <td className="py-2 px-2.5">
                                <select
                                  value={item.adjustmentType}
                                  onChange={(e) =>
                                    handleUpdateAdjItem(idx, 'adjustmentType', e.target.value)
                                  }
                                  className="px-1.5 py-0.5 text-xs border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                                >
                                  <option value="Physical Count Correction">Physical Count Correction</option>
                                  <option value="Increase Stock">Increase Stock</option>
                                  <option value="Decrease Stock">Decrease Stock</option>
                                  <option value="Damaged Goods">Damaged Goods</option>
                                  <option value="Expired Goods">Expired Goods</option>
                                  <option value="Other">Other Approved</option>
                                </select>
                              </td>
                              <td className="py-2 px-2.5 text-right font-mono text-slate-700">
                                ₹{item.unitCost.toFixed(2)}
                              </td>
                              <td className="py-2 px-2.5 text-center">
                                <button
                                  onClick={() => handleRemoveAdjItem(idx)}
                                  className="text-red-500 hover:text-red-700 p-1"
                                >
                                  <Trash2 className="w-3.5 h-3.5" />
                                </button>
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                </>
              ) : (
                /* View Mode */
                <div className="space-y-3">
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs bg-slate-50 p-3 rounded">
                    <div>
                      <span className="text-slate-500 block">Voucher Number</span>
                      <span className="font-bold text-slate-800 font-mono">
                        {selectedAdjustment?.adjustmentNumber}
                      </span>
                    </div>
                    <div>
                      <span className="text-slate-500 block">Status</span>
                      <span className="font-bold text-slate-800">{selectedAdjustment?.status}</span>
                    </div>
                    <div>
                      <span className="text-slate-500 block">Total Items</span>
                      <span className="font-bold text-slate-800">{selectedAdjustment?.totalItems}</span>
                    </div>
                    <div>
                      <span className="text-slate-500 block">Cost Impact</span>
                      <span className="font-bold text-slate-800">
                        ₹{selectedAdjustment?.totalCostImpact.toFixed(2)}
                      </span>
                    </div>
                  </div>

                  <div className="border border-slate-200 rounded overflow-hidden">
                    <table className="w-full text-left text-xs border-collapse">
                      <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-200">
                        <tr>
                          <th className="py-2 px-3">Product Code</th>
                          <th className="py-2 px-3">Product Name</th>
                          <th className="py-2 px-3 text-right">System Stock</th>
                          <th className="py-2 px-3 text-right">Counted</th>
                          <th className="py-2 px-3 text-right">Difference</th>
                          <th className="py-2 px-3">Type</th>
                          <th className="py-2 px-3 text-right">Cost Rate</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {selectedAdjustment?.items.map((it) => (
                          <tr key={it.id}>
                            <td className="py-2 px-3 font-mono text-[11px]">{it.productCode}</td>
                            <td className="py-2 px-3 font-medium">{it.productName}</td>
                            <td className="py-2 px-3 text-right font-mono">{it.currentStock}</td>
                            <td className="py-2 px-3 text-right font-mono">{it.physicalCount}</td>
                            <td
                              className={`py-2 px-3 text-right font-mono font-bold ${
                                it.differenceQuantity > 0 ? 'text-emerald-700' : 'text-red-700'
                              }`}
                            >
                              {it.differenceQuantity > 0 ? `+${it.differenceQuantity}` : it.differenceQuantity}
                            </td>
                            <td className="py-2 px-3">{it.adjustmentType}</td>
                            <td className="py-2 px-3 text-right font-mono">₹{it.unitCost.toFixed(2)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>

            {/* Modal Footer */}
            <div className="bg-slate-50 px-4 py-2.5 border-t border-slate-200 rounded-b-lg flex items-center justify-between">
              {adjModalMode === 'create' ? (
                <>
                  <div className="text-xs text-slate-600 font-mono">
                    Total Items: <span className="font-bold">{adjItems.length}</span> | Net Difference:{' '}
                    <span className="font-bold">
                      {adjItems.reduce((acc, i) => acc + i.differenceQuantity, 0)}
                    </span>
                  </div>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={handleSaveAdjDraft}
                      className="px-3 py-1.5 text-xs bg-slate-200 hover:bg-slate-300 text-slate-800 rounded font-medium cursor-pointer"
                    >
                      Save Draft
                    </button>
                    <button
                      onClick={handleConfirmAdjImmediately}
                      className="px-3 py-1.5 text-xs bg-emerald-700 hover:bg-emerald-800 text-white rounded font-medium cursor-pointer shadow-xs"
                    >
                      Confirm Adjustment
                    </button>
                  </div>
                </>
              ) : (
                <div className="flex justify-end w-full">
                  <button
                    onClick={() => setIsAdjModalOpen(false)}
                    className="px-3 py-1.5 text-xs bg-slate-200 hover:bg-slate-300 text-slate-800 rounded font-medium cursor-pointer"
                  >
                    Close
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ============================================================== */}
      {/* MODAL: PHYSICAL STOCK AUDIT / VERIFICATION COUNT SHEET */}
      {/* ============================================================== */}
      {isVerModalOpen && (
        <div className="fixed inset-0 bg-black/40 z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col border border-slate-300">
            <div className="bg-emerald-800 text-white px-4 py-2.5 rounded-t-lg flex items-center justify-between">
              <h2 className="text-sm font-bold flex items-center gap-2">
                <ClipboardCheck className="w-4 h-4 text-emerald-200" />
                Physical Stock Count Sheet & Discrepancy Reconciliation
              </h2>
              <button
                onClick={() => setIsVerModalOpen(false)}
                className="text-white/80 hover:text-white text-lg font-bold cursor-pointer"
              >
                ✕
              </button>
            </div>

            <div className="p-4 overflow-y-auto flex-1 space-y-3">
              {verError && (
                <div className="p-3 bg-red-50 border border-red-200 rounded text-xs text-red-700">
                  {verError}
                </div>
              )}

              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                <div>
                  <label className="block text-slate-700 font-semibold mb-1">Audit Scope / Category</label>
                  <select
                    value={verCategoryId}
                    onChange={(e) => {
                      setVerCategoryId(Number(e.target.value));
                      // reload verItems
                      const cid = Number(e.target.value);
                      let prods = allProducts.filter((p) => p.isActive);
                      if (cid > 0) prods = prods.filter((p) => p.categoryId === cid);
                      setVerItems(
                        prods.slice(0, 100).map((p) => ({
                          productId: p.id,
                          productCode: p.productCode,
                          barcode: p.barcode,
                          productName: p.nameEn,
                          categoryName: p.categoryName || 'General',
                          systemStockAtStart: p.currentStock,
                          physicalCount: p.currentStock,
                          unitCost: p.purchaseRate,
                        }))
                      );
                    }}
                    className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                  >
                    <option value={0}>All Categories</option>
                    {categories.map((c: Category) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-slate-700 font-semibold mb-1">Audit Notes / Verifier</label>
                  <input
                    type="text"
                    value={verNotes}
                    onChange={(e) => setVerNotes(e.target.value)}
                    placeholder="e.g. End of Month Physical Audit - Section A"
                    className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:border-blue-500 focus:outline-hidden"
                  />
                </div>
              </div>

              <div className="border border-slate-200 rounded overflow-hidden">
                <table className="w-full text-left text-xs border-collapse">
                  <thead className="bg-[#f0f4f8] text-slate-700 font-semibold border-b border-slate-200">
                    <tr>
                      <th className="py-2 px-3">Product Name</th>
                      <th className="py-2 px-3">Category</th>
                      <th className="py-2 px-3 text-right">System Stock</th>
                      <th className="py-2 px-3 text-right">Physical Count</th>
                      <th className="py-2 px-3 text-right font-bold">Discrepancy</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {verItems.map((it, idx) => {
                      const diff = it.physicalCount - it.systemStockAtStart;
                      return (
                        <tr key={it.productId} className="hover:bg-slate-50">
                          <td className="py-2 px-3">
                            <span className="font-medium text-slate-800">{it.productName}</span>
                            <span className="text-[10px] text-slate-400 font-mono ml-2">
                              ({it.productCode})
                            </span>
                          </td>
                          <td className="py-2 px-3 text-slate-600">{it.categoryName}</td>
                          <td className="py-2 px-3 text-right font-mono text-slate-600">
                            {it.systemStockAtStart}
                          </td>
                          <td className="py-2 px-3 text-right">
                            <input
                              type="number"
                              min="0"
                              step="any"
                              value={it.physicalCount}
                              onChange={(e) =>
                                handleUpdateVerCount(idx, parseFloat(e.target.value) || 0)
                              }
                              className="w-20 text-right px-1.5 py-0.5 border border-slate-300 rounded font-mono text-xs focus:border-blue-500 focus:outline-hidden"
                            />
                          </td>
                          <td
                            className={`py-2 px-3 text-right font-mono font-bold ${
                              diff === 0 ? 'text-slate-400' : diff > 0 ? 'text-emerald-700' : 'text-red-700'
                            }`}
                          >
                            {diff === 0 ? '0' : diff > 0 ? `+${diff}` : diff}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="bg-slate-50 px-4 py-2.5 border-t border-slate-200 rounded-b-lg flex items-center justify-between">
              <div className="text-xs text-slate-600 font-mono">
                Discrepancies:{' '}
                <span className="font-bold text-red-700">
                  {verItems.filter((i) => i.physicalCount !== i.systemStockAtStart).length} items
                </span>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setIsVerModalOpen(false)}
                  className="px-3 py-1.5 text-xs bg-slate-200 hover:bg-slate-300 text-slate-800 rounded font-medium cursor-pointer"
                >
                  Cancel
                </button>
                <button
                  onClick={handleConfirmVerificationSession}
                  className="px-3 py-1.5 text-xs bg-emerald-700 hover:bg-emerald-800 text-white rounded font-medium cursor-pointer shadow-xs"
                >
                  Confirm & Reconcile System Stock
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
