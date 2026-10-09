import React, { useState, useEffect, useMemo, useRef } from 'react';
import * as XLSX from 'xlsx';
import {
  Truck,
  Plus,
  Save,
  CheckCircle2,
  XCircle,
  Printer,
  History,
  Building,
  Search,
  Trash2,
  Edit,
  ArrowRight,
  Receipt,
  FileSpreadsheet,
  AlertTriangle,
  RotateCcw,
  Calendar,
  X,
  CreditCard,
  DollarSign,
  PackageCheck,
  Percent,
} from 'lucide-react';
import { db, PurchaseFilter } from '../services/dbEngine';
import { Product, Supplier, PurchaseHeader, PurchaseItem } from '../types';

interface InboundGoodsProps {
  initialSupplierId?: number;
  onRefreshData?: () => void;
  onNavigateToSuppliers?: () => void;
}

export const InboundGoods: React.FC<InboundGoodsProps> = ({
  initialSupplierId,
  onRefreshData,
  onNavigateToSuppliers,
}) => {
  // Navigation sub-tab: 'entry' (New Purchase Entry) | 'history' (Purchase Invoices History)
  const [subTab, setSubTab] = useState<'entry' | 'history'>('entry');

  // Active Purchase Form State
  const [purchaseId, setPurchaseId] = useState<number | null>(null);
  const [purchaseNumber, setPurchaseNumber] = useState<string>('');
  const [supplierId, setSupplierId] = useState<number | ''>(initialSupplierId || '');
  const [supplierInvoiceNumber, setSupplierInvoiceNumber] = useState<string>('');
  const [supplierInvoiceDate, setSupplierInvoiceDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [goodsReceivedDate, setGoodsReceivedDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [purchaseType, setPurchaseType] = useState<'Credit' | 'Cash'>('Credit');
  const [paymentDueDate, setPaymentDueDate] = useState<string>(
    new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10)
  );
  const [referenceNumber, setReferenceNumber] = useState<string>('');
  const [notes, setNotes] = useState<string>('');
  const [status, setStatus] = useState<'Draft' | 'Confirmed' | 'Cancelled'>('Draft');

  // Items State
  const [items, setItems] = useState<PurchaseItem[]>([]);

  // Additional charges & discounts
  const [additionalDiscount, setAdditionalDiscount] = useState<number>(0);
  const [additionalCharges, setAdditionalCharges] = useState<number>(0); // Freight / Packaging
  const [amountPaid, setAmountPaid] = useState<number>(0);

  // Line Item Input Form State
  const [productSearch, setProductSearch] = useState<string>('');
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null);
  const [searchDropdownOpen, setSearchDropdownOpen] = useState(false);
  const [inputQty, setInputQty] = useState<number>(1);
  const [inputFreeQty, setInputFreeQty] = useState<number>(0);
  const [inputRate, setInputRate] = useState<number>(0);
  const [inputDiscPct, setInputDiscPct] = useState<number>(0);
  const [inputTaxRate, setInputTaxRate] = useState<number>(5);
  const [inputBatch, setInputBatch] = useState<string>('');
  const [inputExpiry, setInputExpiry] = useState<string>('');

  // Modals
  const [isPrintModalOpen, setIsPrintModalOpen] = useState(false);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  // History Filter
  const [historyFilterTerm, setHistoryFilterTerm] = useState('');
  const [historyStatusFilter, setHistoryStatusFilter] = useState('All');
  const [historyPage, setHistoryPage] = useState(1);

  const searchInputRef = useRef<HTMLInputElement>(null);

  const showFeedback = (type: 'success' | 'error', message: string) => {
    setFeedback({ type, message });
    setTimeout(() => setFeedback(null), 4500);
  };

  // Init new purchase entry
  const initNewEntry = () => {
    setPurchaseId(null);
    setPurchaseNumber(db.generateNextPurchaseNumber());
    setSupplierId(initialSupplierId || (db.getAllActiveSuppliers()[0]?.id ?? ''));
    setSupplierInvoiceNumber('');
    setSupplierInvoiceDate(new Date().toISOString().slice(0, 10));
    setGoodsReceivedDate(new Date().toISOString().slice(0, 10));
    setPurchaseType('Credit');
    setPaymentDueDate(new Date(Date.now() + 30 * 86400000).toISOString().slice(0, 10));
    setReferenceNumber('');
    setNotes('');
    setStatus('Draft');
    setItems([]);
    setAdditionalDiscount(0);
    setAdditionalCharges(0);
    setAmountPaid(0);
    resetLineInputs();
  };

  useEffect(() => {
    initNewEntry();
  }, []);

  const resetLineInputs = () => {
    setProductSearch('');
    setSelectedProduct(null);
    setInputQty(1);
    setInputFreeQty(0);
    setInputRate(0);
    setInputDiscPct(0);
    setInputTaxRate(5);
    setInputBatch('');
    setInputExpiry('');
    setSearchDropdownOpen(false);
  };

  // Product Search results for autocomplete
  const searchResults = useMemo(() => {
    if (!productSearch.trim()) return [];
    return db
      .searchProducts({
        searchTerm: productSearch,
        pageNumber: 1,
        pageSize: 10,
        isActive: true,
      })
      .items;
  }, [productSearch]);

  const handleSelectProduct = (prod: Product) => {
    setSelectedProduct(prod);
    setProductSearch(prod.nameEn);
    setInputRate(prod.purchaseRate);
    setInputTaxRate(prod.taxRate);
    setSearchDropdownOpen(false);
  };

  // Add Item to Purchase Grid
  const handleAddLineItem = () => {
    if (!selectedProduct) {
      alert('Please search and select a product first.');
      return;
    }
    if (inputQty <= 0) {
      alert('Quantity must be greater than 0.');
      return;
    }
    if (inputRate < 0) {
      alert('Purchase rate cannot be negative.');
      return;
    }

    const lineGross = inputQty * inputRate;
    const lineDisc = (lineGross * inputDiscPct) / 100;
    const lineTaxable = Math.max(0, lineGross - lineDisc);
    const lineTax = (lineTaxable * inputTaxRate) / 100;
    const lineTotal = lineTaxable + lineTax;

    const newItem: PurchaseItem = {
      id: Date.now() + Math.floor(Math.random() * 1000),
      purchaseId: purchaseId || 0,
      productId: selectedProduct.id,
      productCode: selectedProduct.productCode,
      barcode: selectedProduct.barcode,
      productName: selectedProduct.nameEn,
      categoryName: selectedProduct.categoryName,
      quantity: inputQty,
      freeQuantity: inputFreeQty,
      purchaseRate: inputRate,
      discountPercent: inputDiscPct,
      discountAmount: lineDisc,
      taxRate: inputTaxRate,
      taxableAmount: lineTaxable,
      taxAmount: lineTax,
      lineTotal,
      batchNumber: inputBatch.trim(),
      expiryDate: inputExpiry ? inputExpiry : undefined,
    };

    setItems([...items, newItem]);
    resetLineInputs();
    if (searchInputRef.current) searchInputRef.current.focus();
  };

  const handleRemoveItem = (index: number) => {
    const next = [...items];
    next.splice(index, 1);
    setItems(next);
  };

  // Totals calculations
  const totals = useMemo(() => {
    let totalQty = 0;
    let totalFreeQty = 0;
    let grossAmount = 0;
    let itemDiscount = 0;
    let taxableAmount = 0;
    let taxAmount = 0;

    for (const it of items) {
      totalQty += it.quantity;
      totalFreeQty += it.freeQuantity;
      grossAmount += it.quantity * it.purchaseRate;
      itemDiscount += it.discountAmount;
      taxableAmount += it.taxableAmount;
      taxAmount += it.taxAmount;
    }

    const preRound = taxableAmount + taxAmount + additionalCharges - additionalDiscount;
    const grandTotal = Math.max(0, Math.round(preRound));
    const roundOff = parseFloat((grandTotal - preRound).toFixed(2));
    const balanceDue = purchaseType === 'Credit' ? Math.max(0, grandTotal - amountPaid) : 0;

    return {
      totalQty,
      totalFreeQty,
      grossAmount,
      itemDiscount,
      taxableAmount,
      taxAmount,
      roundOff,
      grandTotal,
      balanceDue,
    };
  }, [items, additionalDiscount, additionalCharges, amountPaid, purchaseType]);

  // Selected supplier object
  const currentSupplier = useMemo(() => {
    if (!supplierId) return null;
    return db.getSupplierById(Number(supplierId)) || null;
  }, [supplierId]);

  // Save Draft
  const handleSaveDraft = () => {
    if (!supplierId) {
      showFeedback('error', 'Please select a supplier.');
      return;
    }
    if (items.length === 0) {
      showFeedback('error', 'Please add at least 1 line item.');
      return;
    }

    const draftData: Partial<PurchaseHeader> = {
      id: purchaseId || undefined,
      purchaseNumber,
      supplierId: Number(supplierId),
      supplierInvoiceNumber,
      supplierInvoiceDate,
      goodsReceivedDate,
      purchaseType,
      paymentDueDate: purchaseType === 'Credit' ? paymentDueDate : undefined,
      referenceNumber,
      notes,
      items,
      additionalDiscount,
      additionalCharges,
      amountPaid: purchaseType === 'Cash' ? totals.grandTotal : amountPaid,
    };

    const res = db.saveDraftPurchase(draftData);
    if (res.success && res.purchase) {
      setPurchaseId(res.purchase.id);
      setStatus(res.purchase.status);
      showFeedback('success', `Draft Purchase #${res.purchase.purchaseNumber} saved successfully.`);
      if (onRefreshData) onRefreshData();
    } else {
      showFeedback('error', res.errors.join('; '));
    }
  };

  // Confirm Purchase & Update Stock (F10)
  const handleConfirmPurchase = () => {
    if (!supplierId) {
      showFeedback('error', 'Please select a supplier.');
      return;
    }
    if (items.length === 0) {
      showFeedback('error', 'Cannot confirm purchase without line items.');
      return;
    }

    if (
      !confirm(
        `Are you sure you want to CONFIRM purchase #${purchaseNumber}?\n\nThis will immediately:\n1. Increase stock for ${items.length} products by ${totals.totalQty + totals.totalFreeQty} units\n2. Post ₹${totals.grandTotal.toLocaleString()} to ledger\n3. Record ${purchaseType} transaction`
      )
    ) {
      return;
    }

    // First save draft if not yet saved
    let activeId = purchaseId;
    if (!activeId) {
      const draftRes = db.saveDraftPurchase({
        purchaseNumber,
        supplierId: Number(supplierId),
        supplierInvoiceNumber,
        supplierInvoiceDate,
        goodsReceivedDate,
        purchaseType,
        paymentDueDate: purchaseType === 'Credit' ? paymentDueDate : undefined,
        referenceNumber,
        notes,
        items,
        additionalDiscount,
        additionalCharges,
        amountPaid: purchaseType === 'Cash' ? totals.grandTotal : amountPaid,
      });

      if (!draftRes.success || !draftRes.purchase) {
        showFeedback('error', draftRes.errors.join('; '));
        return;
      }
      activeId = draftRes.purchase.id;
      setPurchaseId(activeId);
    }

    // Now confirm
    const confirmRes = db.confirmPurchase(activeId);
    if (confirmRes.success && confirmRes.purchase) {
      setStatus('Confirmed');
      showFeedback(
        'success',
        `Purchase #${confirmRes.purchase.purchaseNumber} CONFIRMED! Stock updated successfully.`
      );
      if (onRefreshData) onRefreshData();
    } else {
      showFeedback('error', confirmRes.error || 'Failed to confirm purchase.');
    }
  };

  // Cancel Purchase
  const handleCancelPurchase = () => {
    if (!purchaseId) return;
    if (
      !confirm(
        `Warning: Cancelling purchase #${purchaseNumber} will REVERT product stock and supplier ledger balances. Do you want to proceed?`
      )
    ) {
      return;
    }

    const res = db.cancelPurchase(purchaseId);
    if (res.success) {
      setStatus('Cancelled');
      showFeedback('success', `Purchase #${purchaseNumber} has been cancelled and stock reverted.`);
      if (onRefreshData) onRefreshData();
    } else {
      showFeedback('error', res.error || 'Failed to cancel purchase.');
    }
  };

  // Load existing purchase into editor from history
  const handleLoadPurchase = (pur: PurchaseHeader) => {
    setPurchaseId(pur.id);
    setPurchaseNumber(pur.purchaseNumber);
    setSupplierId(pur.supplierId);
    setSupplierInvoiceNumber(pur.supplierInvoiceNumber || '');
    setSupplierInvoiceDate(pur.supplierInvoiceDate?.slice(0, 10) || new Date().toISOString().slice(0, 10));
    setGoodsReceivedDate(pur.goodsReceivedDate?.slice(0, 10) || new Date().toISOString().slice(0, 10));
    setPurchaseType(pur.purchaseType === 'Cash' ? 'Cash' : 'Credit');
    setPaymentDueDate(pur.paymentDueDate?.slice(0, 10) || '');
    setReferenceNumber(pur.referenceNumber || '');
    setNotes(pur.notes || '');
    setStatus(pur.status as any);
    setItems(pur.items || []);
    setAdditionalDiscount(pur.additionalDiscount || 0);
    setAdditionalCharges(pur.additionalCharges || 0);
    setAmountPaid(pur.amountPaid || 0);
    setSubTab('entry');
  };

  // History query
  const historyResult = useMemo(() => {
    return db.searchPurchases({
      searchTerm: historyFilterTerm,
      status: historyStatusFilter,
      pageNumber: historyPage,
      pageSize: 20,
    });
  }, [historyFilterTerm, historyStatusFilter, historyPage, status, subTab]);

  return (
    <div className="flex-1 flex flex-col h-full bg-white select-none overflow-hidden">
      {/* 1. Module Header Bar with Sub-Tabs */}
      <div className="bg-[#1f4e79] text-white px-3 py-2 flex items-center justify-between shadow-xs">
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2">
            <Truck className="w-5 h-5 text-amber-300" />
            <h2 className="text-sm font-bold tracking-wide">INBOUND GOODS / PURCHASE ENTRY (F3)</h2>
          </div>

          <div className="flex bg-blue-900/60 p-0.5 rounded text-xs">
            <button
              onClick={() => setSubTab('entry')}
              className={`px-3 py-1 rounded font-medium transition-colors cursor-pointer ${
                subTab === 'entry' ? 'bg-white text-blue-900 shadow-xs' : 'text-blue-200 hover:text-white'
              }`}
            >
              Purchase Entry
            </button>
            <button
              onClick={() => setSubTab('history')}
              className={`px-3 py-1 rounded font-medium transition-colors cursor-pointer ${
                subTab === 'history' ? 'bg-white text-blue-900 shadow-xs' : 'text-blue-200 hover:text-white'
              }`}
            >
              Purchase History & Bills ({db.searchPurchases({ pageNumber: 1, pageSize: 1 }).totalCount})
            </button>
          </div>
        </div>

        <div className="flex items-center gap-2 text-xs">
          {onNavigateToSuppliers && (
            <button
              onClick={onNavigateToSuppliers}
              className="px-2.5 py-1 bg-white/10 hover:bg-white/20 text-white rounded font-medium flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <Building className="w-3.5 h-3.5 text-amber-300" />
              <span>Supplier Directory</span>
            </button>
          )}
        </div>
      </div>

      {/* Feedback banner */}
      {feedback && (
        <div
          className={`px-3 py-1.5 text-xs flex items-center justify-between border-b ${
            feedback.type === 'success'
              ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
              : 'bg-rose-50 text-rose-800 border-rose-200'
          }`}
        >
          <div className="flex items-center gap-2">
            {feedback.type === 'success' ? (
              <CheckCircle2 className="w-4 h-4 text-emerald-600 shrink-0" />
            ) : (
              <AlertTriangle className="w-4 h-4 text-rose-600 shrink-0" />
            )}
            <span>{feedback.message}</span>
          </div>
          <button onClick={() => setFeedback(null)} className="text-slate-400 hover:text-slate-600">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* Main Tab Content */}
      {subTab === 'entry' ? (
        <div className="flex-1 flex flex-col overflow-hidden">
          {/* Top Workflow Action Bar */}
          <div className="bg-[#f0f4f9] border-b border-[#c8d4e2] px-3 py-1.5 flex flex-wrap items-center justify-between gap-2 text-xs">
            <div className="flex items-center gap-1.5 flex-wrap">
              <button
                onClick={initNewEntry}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded font-medium cursor-pointer"
                title="Start a fresh blank purchase order (Ctrl+N)"
              >
                <Plus className="w-3.5 h-3.5 text-blue-600" />
                <span>New Entry</span>
              </button>

              <button
                onClick={handleSaveDraft}
                disabled={status === 'Confirmed' || status === 'Cancelled'}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded font-medium disabled:opacity-40 cursor-pointer"
                title="Save current purchase as draft (does not affect inventory)"
              >
                <Save className="w-3.5 h-3.5 text-indigo-600" />
                <span>Save as Draft</span>
              </button>

              <button
                onClick={handleConfirmPurchase}
                disabled={status === 'Confirmed' || status === 'Cancelled'}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-bold shadow-xs disabled:opacity-40 cursor-pointer"
                title="Confirm goods received and update inventory (F10)"
              >
                <PackageCheck className="w-4 h-4" />
                <span>Confirm & Update Stock</span>
                <span className="text-[10px] bg-white/20 px-1 py-0.2 rounded font-mono ml-0.5">F10</span>
              </button>

              {status === 'Confirmed' && (
                <button
                  onClick={handleCancelPurchase}
                  className="flex items-center gap-1.5 px-3 py-1.5 bg-rose-50 border border-rose-300 hover:bg-rose-100 text-rose-800 rounded font-medium cursor-pointer"
                  title="Cancel confirmed purchase and safely revert stock"
                >
                  <XCircle className="w-3.5 h-3.5 text-rose-600" />
                  <span>Cancel Purchase (Revert Stock)</span>
                </button>
              )}

              <div className="w-[1px] h-5 bg-slate-300 mx-1" />

              <button
                onClick={() => setIsPrintModalOpen(true)}
                disabled={items.length === 0}
                className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded font-medium disabled:opacity-40 cursor-pointer"
                title="Print Goods Received Note (GRN) or Tax Invoice"
              >
                <Printer className="w-3.5 h-3.5 text-slate-600" />
                <span>Print GRN / Invoice</span>
              </button>
            </div>

            {/* Status Pill Badge */}
            <div className="flex items-center gap-2">
              <span className="text-slate-500 font-medium">Status:</span>
              <span
                className={`px-2.5 py-0.5 rounded-full font-bold font-mono text-[11px] uppercase tracking-wider ${
                  status === 'Confirmed'
                    ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                    : status === 'Draft'
                    ? 'bg-amber-100 text-amber-900 border border-amber-300'
                    : 'bg-rose-100 text-rose-800 border border-rose-300'
                }`}
              >
                ● {status}
              </span>
            </div>
          </div>

          {/* 2. Purchase Header Form Grid */}
          <div className="bg-[#fafbfc] border-b border-[#d8e2ec] p-3 text-xs">
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-6 gap-2.5">
              <div>
                <label className="block text-slate-600 font-bold mb-1">Purchase #</label>
                <input
                  type="text"
                  readOnly
                  value={purchaseNumber}
                  className="w-full px-2 py-1.5 bg-slate-100 border border-slate-300 rounded font-mono font-bold text-blue-900"
                />
              </div>

              <div className="sm:col-span-2">
                <label className="block text-slate-700 font-bold mb-1">
                  Supplier <span className="text-rose-500">*</span>
                </label>
                <select
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={supplierId}
                  onChange={(e) => setSupplierId(e.target.value ? Number(e.target.value) : '')}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-semibold focus:outline-hidden focus:border-blue-500"
                >
                  <option value="">-- Select Supplier --</option>
                  {db.getAllActiveSuppliers().map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name} ({s.code}) — {s.city || s.state}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Supplier Inv #</label>
                <input
                  type="text"
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={supplierInvoiceNumber}
                  onChange={(e) => setSupplierInvoiceNumber(e.target.value)}
                  placeholder="INV-9981"
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Invoice Date</label>
                <input
                  type="date"
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={supplierInvoiceDate}
                  onChange={(e) => setSupplierInvoiceDate(e.target.value)}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-bold mb-1">Goods Received Date</label>
                <input
                  type="date"
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={goodsReceivedDate}
                  onChange={(e) => setGoodsReceivedDate(e.target.value)}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-semibold focus:outline-hidden focus:border-blue-500"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-bold mb-1">Payment Mode</label>
                <select
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={purchaseType}
                  onChange={(e) => setPurchaseType(e.target.value as any)}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-semibold focus:outline-hidden focus:border-blue-500"
                >
                  <option value="Credit">Credit (Pay Later)</option>
                  <option value="Cash">Cash (Immediate Paid)</option>
                </select>
              </div>

              {purchaseType === 'Credit' && (
                <div>
                  <label className="block text-slate-600 font-medium mb-1">Due Date</label>
                  <input
                    type="date"
                    disabled={status === 'Confirmed' || status === 'Cancelled'}
                    value={paymentDueDate}
                    onChange={(e) => setPaymentDueDate(e.target.value)}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
                  />
                </div>
              )}

              <div>
                <label className="block text-slate-600 font-medium mb-1">E-Way / Challan Ref</label>
                <input
                  type="text"
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={referenceNumber}
                  onChange={(e) => setReferenceNumber(e.target.value)}
                  placeholder="EWAY-881290"
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
                />
              </div>

              <div className="sm:col-span-2">
                <label className="block text-slate-600 font-medium mb-1">Remarks / Delivery Notes</label>
                <input
                  type="text"
                  disabled={status === 'Confirmed' || status === 'Cancelled'}
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  placeholder="e.g. Received via VRL Logistics, batch intact..."
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
                />
              </div>
            </div>

            {/* Supplier Quick Details Tag */}
            {currentSupplier && (
              <div className="mt-2 pt-2 border-t border-slate-200 flex flex-wrap items-center gap-4 text-[11px] text-slate-600">
                <span>
                  GSTIN: <strong className="font-mono text-slate-800">{currentSupplier.gstin || 'Unregistered'}</strong>
                </span>
                <span>
                  Phone: <strong className="font-mono text-slate-800">{currentSupplier.mobile || '—'}</strong>
                </span>
                <span>
                  Payment Terms: <strong className="text-slate-800">{currentSupplier.paymentTerms}</strong>
                </span>
                <span className="ml-auto">
                  Current Outstanding Payable:{' '}
                  <strong className={`font-mono font-bold ${currentSupplier.currentBalance > 0 ? 'text-rose-700' : 'text-emerald-700'}`}>
                    ₹{currentSupplier.currentBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                  </strong>
                </span>
              </div>
            )}
          </div>

          {/* 3. Product Line Item Fast Entry Strip */}
          {status !== 'Confirmed' && status !== 'Cancelled' && (
            <div className="bg-[#eef4fa] border-b border-[#c8d4e2] p-2.5 text-xs">
              <div className="grid grid-cols-12 gap-2 items-end">
                {/* Product Search & Dropdown */}
                <div className="col-span-12 md:col-span-4 relative">
                  <label className="block text-slate-700 font-bold mb-1">
                    Scan Barcode / Search Product <span className="text-rose-500">*</span>
                  </label>
                  <div className="relative">
                    <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
                    <input
                      ref={searchInputRef}
                      type="text"
                      value={productSearch}
                      onChange={(e) => {
                        setProductSearch(e.target.value);
                        setSearchDropdownOpen(true);
                      }}
                      onFocus={() => setSearchDropdownOpen(true)}
                      placeholder="Scan Barcode (e.g. 890103...) or type product name..."
                      className="w-full pl-8 pr-2 py-1.5 bg-white border border-slate-300 rounded focus:outline-hidden focus:border-blue-500 font-medium"
                    />
                  </div>

                  {/* Autocomplete Dropdown */}
                  {searchDropdownOpen && searchResults.length > 0 && (
                    <div className="absolute left-0 right-0 top-full mt-1 bg-white border border-slate-300 rounded shadow-xl z-20 max-h-56 overflow-y-auto">
                      {searchResults.map((p) => (
                        <div
                          key={p.id}
                          onClick={() => handleSelectProduct(p)}
                          className="px-3 py-2 hover:bg-blue-50 cursor-pointer border-b border-slate-100 flex items-center justify-between"
                        >
                          <div>
                            <div className="font-semibold text-slate-800">{p.nameEn}</div>
                            <div className="text-[10px] text-slate-500 font-mono">
                              Code: {p.productCode} · Barcode: {p.barcode} · Stock: {p.currentStock} {p.unit}
                            </div>
                          </div>
                          <div className="text-right">
                            <div className="font-mono font-bold text-blue-700">₹{p.purchaseRate}</div>
                            <div className="text-[10px] text-slate-400">MRP: ₹{p.mrp}</div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                <div className="col-span-4 md:col-span-1">
                  <label className="block text-slate-700 font-bold mb-1">Qty</label>
                  <input
                    type="number"
                    min="1"
                    value={inputQty}
                    onChange={(e) => setInputQty(Math.max(1, parseInt(e.target.value) || 0))}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono font-bold text-center"
                  />
                </div>

                <div className="col-span-4 md:col-span-1">
                  <label className="block text-slate-600 font-medium mb-1">Free Qty</label>
                  <input
                    type="number"
                    min="0"
                    value={inputFreeQty}
                    onChange={(e) => setInputFreeQty(Math.max(0, parseInt(e.target.value) || 0))}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono text-center text-emerald-700 font-bold"
                  />
                </div>

                <div className="col-span-4 md:col-span-1">
                  <label className="block text-slate-700 font-bold mb-1">Rate (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={inputRate}
                    onChange={(e) => setInputRate(Math.max(0, parseFloat(e.target.value) || 0))}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono font-bold text-right"
                  />
                </div>

                <div className="col-span-4 md:col-span-1">
                  <label className="block text-slate-600 font-medium mb-1">Disc %</label>
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="100"
                    value={inputDiscPct}
                    onChange={(e) => setInputDiscPct(Math.max(0, parseFloat(e.target.value) || 0))}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono text-right"
                  />
                </div>

                <div className="col-span-4 md:col-span-1">
                  <label className="block text-slate-600 font-medium mb-1">GST %</label>
                  <select
                    value={inputTaxRate}
                    onChange={(e) => setInputTaxRate(Number(e.target.value))}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono"
                  >
                    <option value={0}>0%</option>
                    <option value={5}>5%</option>
                    <option value={12}>12%</option>
                    <option value={18}>18%</option>
                    <option value={28}>28%</option>
                  </select>
                </div>

                <div className="col-span-4 md:col-span-1">
                  <label className="block text-slate-600 font-medium mb-1">Batch #</label>
                  <input
                    type="text"
                    value={inputBatch}
                    onChange={(e) => setInputBatch(e.target.value)}
                    placeholder="B-2026"
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono"
                  />
                </div>

                <div className="col-span-6 md:col-span-1">
                  <label className="block text-slate-600 font-medium mb-1">Expiry</label>
                  <input
                    type="date"
                    value={inputExpiry}
                    onChange={(e) => setInputExpiry(e.target.value)}
                    className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded text-[11px]"
                  />
                </div>

                <div className="col-span-6 md:col-span-1">
                  <button
                    onClick={handleAddLineItem}
                    className="w-full py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-bold transition-colors shadow-xs cursor-pointer"
                  >
                    + Add
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* 4. Line Items Grid */}
          <div className="flex-1 flex overflow-hidden">
            <div className="flex-1 overflow-auto custom-scrollbar">
              <table className="w-full text-left border-collapse text-xs">
                <thead className="bg-[#1f4e79] text-white sticky top-0 z-10 select-none shadow-xs">
                  <tr>
                    <th className="py-2 px-2 border-b border-r border-blue-900/40 w-10 text-center">#</th>
                    <th className="py-2 px-2.5 border-b border-r border-blue-900/40 w-24">Barcode</th>
                    <th className="py-2 px-3 border-b border-r border-blue-900/40">Product Description</th>
                    <th className="py-2 px-2 border-b border-r border-blue-900/40 text-center w-16">Qty</th>
                    <th className="py-2 px-2 border-b border-r border-blue-900/40 text-center w-16">Free</th>
                    <th className="py-2 px-2.5 border-b border-r border-blue-900/40 text-right w-20">Rate (₹)</th>
                    <th className="py-2 px-2 border-b border-r border-blue-900/40 text-right w-16">Disc %</th>
                    <th className="py-2 px-2.5 border-b border-r border-blue-900/40 text-right w-20">Taxable</th>
                    <th className="py-2 px-2 border-b border-r border-blue-900/40 text-center w-14">GST %</th>
                    <th className="py-2 px-2.5 border-b border-r border-blue-900/40 text-right w-20">GST (₹)</th>
                    <th className="py-2 px-3 border-b border-r border-blue-900/40 text-right w-24">Total (₹)</th>
                    <th className="py-2 px-2 border-b border-r border-blue-900/40 w-24">Batch / Exp</th>
                    {status !== 'Confirmed' && status !== 'Cancelled' && (
                      <th className="py-2 px-2 border-b text-center w-12">Del</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200">
                  {items.length === 0 ? (
                    <tr>
                      <td colSpan={13} className="py-12 text-center text-slate-400">
                        <div className="flex flex-col items-center justify-center gap-1.5">
                          <Receipt className="w-8 h-8 text-slate-300" />
                          <p className="font-semibold text-slate-600">No products added to this purchase bill yet.</p>
                          <p className="text-[11px] text-slate-400">
                            Search a product or scan a barcode above to add line items.
                          </p>
                        </div>
                      </td>
                    </tr>
                  ) : (
                    items.map((item, idx) => (
                      <tr key={item.id} className={idx % 2 === 1 ? 'bg-slate-50/60' : 'bg-white'}>
                        <td className="py-1.5 px-2 text-center font-mono text-slate-400">{idx + 1}</td>
                        <td className="py-1.5 px-2.5 font-mono text-slate-600">{item.barcode}</td>
                        <td className="py-1.5 px-3 font-semibold text-slate-800">
                          {item.productName}
                          <span className="block font-normal text-[10px] text-slate-500">
                            Code: {item.productCode} · {item.categoryName}
                          </span>
                        </td>
                        <td className="py-1.5 px-2 text-center font-mono font-bold text-slate-800">{item.quantity}</td>
                        <td className="py-1.5 px-2 text-center font-mono text-emerald-700 font-bold">
                          {item.freeQuantity > 0 ? `+${item.freeQuantity}` : '—'}
                        </td>
                        <td className="py-1.5 px-2.5 text-right font-mono">{item.purchaseRate.toFixed(2)}</td>
                        <td className="py-1.5 px-2 text-right font-mono text-slate-600">
                          {item.discountPercent > 0 ? `${item.discountPercent}%` : '0%'}
                        </td>
                        <td className="py-1.5 px-2.5 text-right font-mono font-medium text-slate-700">
                          {item.taxableAmount.toFixed(2)}
                        </td>
                        <td className="py-1.5 px-2 text-center font-mono text-slate-600">{item.taxRate}%</td>
                        <td className="py-1.5 px-2.5 text-right font-mono text-slate-700">{item.taxAmount.toFixed(2)}</td>
                        <td className="py-1.5 px-3 text-right font-mono font-bold text-blue-900">
                          ₹{item.lineTotal.toFixed(2)}
                        </td>
                        <td className="py-1.5 px-2 font-mono text-[11px] text-slate-600">
                          {item.batchNumber ? (
                            <div>
                              <span>{item.batchNumber}</span>
                              {item.expiryDate && (
                                <span className="block text-[10px] text-slate-400">{item.expiryDate}</span>
                              )}
                            </div>
                          ) : (
                            '—'
                          )}
                        </td>
                        {status !== 'Confirmed' && status !== 'Cancelled' && (
                          <td className="py-1.5 px-2 text-center">
                            <button
                              onClick={() => handleRemoveItem(idx)}
                              className="text-slate-400 hover:text-rose-600 p-1 rounded hover:bg-rose-50 cursor-pointer"
                              title="Remove item"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </td>
                        )}
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>

            {/* 5. Summary / Calculation Panel (Docked on Right) */}
            <div className="w-80 shrink-0 bg-[#f4f7fb] border-l border-[#d8e2ec] p-3 flex flex-col justify-between overflow-y-auto text-xs space-y-3">
              <div className="space-y-2">
                <div className="text-[11px] font-bold uppercase tracking-wider text-slate-500 border-b border-slate-200 pb-1">
                  Purchase Invoice Breakdown
                </div>

                <div className="flex justify-between items-center text-slate-600">
                  <span>Total Items:</span>
                  <span className="font-mono font-semibold text-slate-900">{items.length} lines</span>
                </div>

                <div className="flex justify-between items-center text-slate-600">
                  <span>Total Quantity:</span>
                  <span className="font-mono font-bold text-slate-900">
                    {totals.totalQty} {totals.totalFreeQty > 0 && `(+${totals.totalFreeQty} Free)`}
                  </span>
                </div>

                <div className="flex justify-between items-center text-slate-600">
                  <span>Gross Amount:</span>
                  <span className="font-mono font-medium">₹{totals.grossAmount.toFixed(2)}</span>
                </div>

                <div className="flex justify-between items-center text-slate-600">
                  <span>Item Discounts:</span>
                  <span className="font-mono text-emerald-700 font-medium">-₹{totals.itemDiscount.toFixed(2)}</span>
                </div>

                <div className="flex justify-between items-center text-slate-600">
                  <span>Taxable Value:</span>
                  <span className="font-mono font-bold text-slate-800">₹{totals.taxableAmount.toFixed(2)}</span>
                </div>

                <div className="flex justify-between items-center text-slate-600">
                  <span>Total GST / Tax:</span>
                  <span className="font-mono text-slate-800 font-semibold">+₹{totals.taxAmount.toFixed(2)}</span>
                </div>

                {/* Additional Discount Input */}
                <div className="flex justify-between items-center pt-1 border-t border-slate-200">
                  <span className="text-slate-600">Addl. Discount:</span>
                  <div className="flex items-center gap-1 w-28">
                    <span className="text-slate-400">₹</span>
                    <input
                      type="number"
                      min="0"
                      disabled={status === 'Confirmed' || status === 'Cancelled'}
                      value={additionalDiscount}
                      onChange={(e) => setAdditionalDiscount(Math.max(0, parseFloat(e.target.value) || 0))}
                      className="w-full px-1.5 py-0.5 bg-white border border-slate-300 rounded font-mono text-right"
                    />
                  </div>
                </div>

                {/* Freight / Additional Charges Input */}
                <div className="flex justify-between items-center">
                  <span className="text-slate-600">Freight & Charges:</span>
                  <div className="flex items-center gap-1 w-28">
                    <span className="text-slate-400">₹</span>
                    <input
                      type="number"
                      min="0"
                      disabled={status === 'Confirmed' || status === 'Cancelled'}
                      value={additionalCharges}
                      onChange={(e) => setAdditionalCharges(Math.max(0, parseFloat(e.target.value) || 0))}
                      className="w-full px-1.5 py-0.5 bg-white border border-slate-300 rounded font-mono text-right"
                    />
                  </div>
                </div>

                <div className="flex justify-between items-center text-slate-500 text-[11px]">
                  <span>Round Off:</span>
                  <span className="font-mono">
                    {totals.roundOff >= 0 ? `+₹${totals.roundOff}` : `-₹${Math.abs(totals.roundOff)}`}
                  </span>
                </div>
              </div>

              {/* Big Grand Total Box */}
              <div className="bg-[#1f4e79] text-white p-3 rounded-lg shadow-md space-y-2">
                <div className="text-[11px] uppercase tracking-wider text-blue-200 font-semibold">Grand Total Payable</div>
                <div className="font-mono text-2xl font-black text-amber-300">
                  ₹{totals.grandTotal.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </div>

                {purchaseType === 'Credit' ? (
                  <div className="pt-2 border-t border-blue-800 space-y-1.5">
                    <div className="flex justify-between items-center text-xs">
                      <span className="text-blue-200">Amount Paid (Part):</span>
                      <div className="flex items-center gap-1 w-24">
                        <span className="text-blue-300">₹</span>
                        <input
                          type="number"
                          min="0"
                          disabled={status === 'Confirmed' || status === 'Cancelled'}
                          value={amountPaid}
                          onChange={(e) => setAmountPaid(Math.max(0, parseFloat(e.target.value) || 0))}
                          className="w-full px-1 py-0.5 bg-white text-slate-900 rounded font-mono text-right text-xs"
                        />
                      </div>
                    </div>
                    <div className="flex justify-between items-center text-xs font-bold">
                      <span className="text-amber-200">Balance Due:</span>
                      <span className="font-mono text-sm text-rose-300">
                        ₹{totals.balanceDue.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                  </div>
                ) : (
                  <div className="pt-1 text-[11px] text-emerald-300 font-medium">
                    ✓ Immediate Cash Purchase (Paid in full)
                  </div>
                )}
              </div>

              {/* Confirm Action Button */}
              {status !== 'Confirmed' && status !== 'Cancelled' && (
                <button
                  onClick={handleConfirmPurchase}
                  disabled={items.length === 0}
                  className="w-full py-2 bg-emerald-600 hover:bg-emerald-700 disabled:opacity-40 text-white rounded-lg font-bold shadow-sm transition-colors text-center cursor-pointer"
                >
                  Confirm & Update Stock (F10)
                </button>
              )}
            </div>
          </div>
        </div>
      ) : (
        /* PURCHASE HISTORY SUB-TAB */
        <div className="flex-1 flex flex-col overflow-hidden">
          {/* History Filters */}
          <div className="bg-[#fafbfc] border-b border-slate-200 px-3 py-2 flex items-center justify-between gap-3 text-xs">
            <div className="flex items-center gap-2 flex-1 max-w-xl">
              <div className="relative flex-1">
                <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
                <input
                  type="text"
                  value={historyFilterTerm}
                  onChange={(e) => {
                    setHistoryFilterTerm(e.target.value);
                    setHistoryPage(1);
                  }}
                  placeholder="Search by Purchase #, Supplier Name, Invoice #, Reference..."
                  className="w-full pl-8 pr-2 py-1.5 bg-white border border-slate-300 rounded font-mono text-xs placeholder:font-sans"
                />
              </div>

              <select
                value={historyStatusFilter}
                onChange={(e) => {
                  setHistoryStatusFilter(e.target.value);
                  setHistoryPage(1);
                }}
                className="px-2 py-1.5 bg-white border border-slate-300 rounded"
              >
                <option value="All">All Statuses</option>
                <option value="Draft">Drafts Only</option>
                <option value="Confirmed">Confirmed Only</option>
                <option value="Cancelled">Cancelled Only</option>
              </select>
            </div>

            <button
              onClick={() => {
                initNewEntry();
                setSubTab('entry');
              }}
              className="px-3 py-1.5 bg-[#106ebe] text-white rounded font-medium flex items-center gap-1.5 cursor-pointer"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>+ New Purchase Entry</span>
            </button>
          </div>

          {/* History Grid */}
          <div className="flex-1 overflow-auto custom-scrollbar">
            <table className="w-full text-left border-collapse text-xs">
              <thead className="bg-[#1f4e79] text-white sticky top-0 z-10 shadow-xs">
                <tr>
                  <th className="py-2 px-2.5 w-28">Purchase #</th>
                  <th className="py-2 px-3">Supplier Name</th>
                  <th className="py-2 px-2.5 w-24">Inv #</th>
                  <th className="py-2 px-2.5 w-24">Received Date</th>
                  <th className="py-2 px-2 text-center w-16">Lines</th>
                  <th className="py-2 px-2 text-center w-20">Type</th>
                  <th className="py-2 px-3 text-right w-24">Grand Total</th>
                  <th className="py-2 px-2.5 text-right w-24">Balance Due</th>
                  <th className="py-2 px-2.5 text-center w-24">Status</th>
                  <th className="py-2 px-2.5 text-center w-28">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {historyResult.items.length === 0 ? (
                  <tr>
                    <td colSpan={10} className="py-12 text-center text-slate-400">
                      No purchase entries found matching filter.
                    </td>
                  </tr>
                ) : (
                  historyResult.items.map((pur, idx) => (
                    <tr
                      key={pur.id}
                      onClick={() => handleLoadPurchase(pur)}
                      className={`cursor-pointer hover:bg-blue-50/60 ${idx % 2 === 1 ? 'bg-slate-50/50' : 'bg-white'}`}
                    >
                      <td className="py-2 px-2.5 font-mono font-bold text-blue-900">{pur.purchaseNumber}</td>
                      <td className="py-2 px-3 font-semibold text-slate-800">{pur.supplierName}</td>
                      <td className="py-2 px-2.5 font-mono text-slate-600">{pur.supplierInvoiceNumber || '—'}</td>
                      <td className="py-2 px-2.5 text-slate-600">
                        {new Date(pur.goodsReceivedDate).toLocaleDateString()}
                      </td>
                      <td className="py-2 px-2 text-center font-mono">{pur.items?.length || 0}</td>
                      <td className="py-2 px-2 text-center">
                        <span
                          className={`px-1.5 py-0.5 rounded text-[10px] font-medium ${
                            pur.purchaseType === 'Cash'
                              ? 'bg-emerald-100 text-emerald-800'
                              : 'bg-blue-100 text-blue-800'
                          }`}
                        >
                          {pur.purchaseType}
                        </span>
                      </td>
                      <td className="py-2 px-3 text-right font-mono font-bold text-slate-900">
                        ₹{pur.grandTotal.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                      </td>
                      <td className="py-2 px-2.5 text-right font-mono font-bold text-rose-700">
                        {pur.balanceDue > 0
                          ? `₹${pur.balanceDue.toLocaleString('en-IN', { minimumFractionDigits: 2 })}`
                          : '₹0.00'}
                      </td>
                      <td className="py-2 px-2.5 text-center">
                        <span
                          className={`px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider ${
                            pur.status === 'Confirmed'
                              ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                              : pur.status === 'Draft'
                              ? 'bg-amber-100 text-amber-900 border border-amber-300'
                              : 'bg-rose-100 text-rose-800 border border-rose-300'
                          }`}
                        >
                          {pur.status}
                        </span>
                      </td>
                      <td className="py-2 px-2.5 text-center" onClick={(e) => e.stopPropagation()}>
                        <button
                          onClick={() => handleLoadPurchase(pur)}
                          className="px-2 py-1 bg-white border border-slate-300 hover:bg-slate-100 rounded text-blue-700 font-medium"
                        >
                          View / Edit
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* PRINT / INVOICE MODAL */}
      {isPrintModalOpen && (
        <PurchasePrintModal
          purchaseNumber={purchaseNumber}
          supplierName={currentSupplier?.name || 'Selected Supplier'}
          supplierInvoiceNumber={supplierInvoiceNumber}
          goodsReceivedDate={goodsReceivedDate}
          purchaseType={purchaseType}
          items={items}
          totals={totals}
          onClose={() => setIsPrintModalOpen(false)}
        />
      )}
    </div>
  );
};

// -----------------------------------------------------------------------------------------
// MODAL: Professional Goods Received Note & Invoice Preview
// -----------------------------------------------------------------------------------------
interface PurchasePrintModalProps {
  purchaseNumber: string;
  supplierName: string;
  supplierInvoiceNumber: string;
  goodsReceivedDate: string;
  purchaseType: string;
  items: PurchaseItem[];
  totals: any;
  onClose: () => void;
}

const PurchasePrintModal: React.FC<PurchasePrintModalProps> = ({
  purchaseNumber,
  supplierName,
  supplierInvoiceNumber,
  goodsReceivedDate,
  purchaseType,
  items,
  totals,
  onClose,
}) => {
  const [printFormat, setPrintFormat] = useState<'A4' | 'thermal'>('A4');

  const handlePrint = () => {
    window.print();
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-3">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-3xl flex flex-col max-h-[92vh] overflow-hidden text-xs">
        <div className="bg-[#1f4e79] text-white px-4 py-2.5 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Printer className="w-4 h-4 text-emerald-300" />
            <h3 className="font-bold text-sm">Goods Received Note (GRN) & Purchase Voucher</h3>
          </div>

          <div className="flex items-center gap-2">
            <div className="bg-blue-900/60 p-0.5 rounded text-[11px] flex">
              <button
                onClick={() => setPrintFormat('A4')}
                className={`px-2 py-0.5 rounded font-medium ${printFormat === 'A4' ? 'bg-white text-blue-900' : 'text-blue-200'}`}
              >
                Standard A4
              </button>
              <button
                onClick={() => setPrintFormat('thermal')}
                className={`px-2 py-0.5 rounded font-medium ${printFormat === 'thermal' ? 'bg-white text-blue-900' : 'text-blue-200'}`}
              >
                3-inch Thermal
              </button>
            </div>
            <button onClick={onClose} className="text-white/80 hover:text-white ml-2">
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Printable Canvas */}
        <div className="flex-1 overflow-y-auto p-6 bg-slate-100 flex justify-center">
          <div
            className={`bg-white border border-slate-300 shadow-sm p-6 space-y-4 text-slate-800 ${
              printFormat === 'A4' ? 'w-full max-w-2xl' : 'w-80 font-mono text-[11px]'
            }`}
          >
            {/* Header */}
            <div className="text-center border-b border-slate-300 pb-3 space-y-0.5">
              <h2 className="text-base font-bold text-slate-900">SRI MURUGAN SUPER MARKET</h2>
              <p className="text-[11px] text-slate-600">124 Bazaar Road, T. Nagar, Chennai - 600017</p>
              <p className="text-[10px] text-slate-500 font-mono">GSTIN: 33AAAAA0000A1Z5 · Ph: 044-24345678</p>
              <div className="pt-1.5 font-bold uppercase tracking-wider text-xs text-blue-900">
                GOODS RECEIVED VOUCHER (INBOUND)
              </div>
            </div>

            {/* Voucher Metadata */}
            <div className="grid grid-cols-2 gap-2 text-xs border-b border-slate-200 pb-3">
              <div>
                <p>
                  <strong>GRN Number:</strong> <span className="font-mono">{purchaseNumber}</span>
                </p>
                <p>
                  <strong>Supplier:</strong> {supplierName}
                </p>
                <p>
                  <strong>Inv #:</strong> {supplierInvoiceNumber || 'N/A'}
                </p>
              </div>
              <div className="text-right">
                <p>
                  <strong>Received Date:</strong> {goodsReceivedDate}
                </p>
                <p>
                  <strong>Terms:</strong> {purchaseType}
                </p>
                <p>
                  <strong>Terminal:</strong> #01 (Offline)
                </p>
              </div>
            </div>

            {/* Items Table */}
            <table className="w-full text-left border-collapse text-[11px]">
              <thead className="border-b border-slate-300 font-bold">
                <tr>
                  <th className="py-1">#</th>
                  <th className="py-1">Item Description</th>
                  <th className="py-1 text-center">Qty</th>
                  <th className="py-1 text-right">Rate</th>
                  <th className="py-1 text-right">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 font-mono">
                {items.map((it, idx) => (
                  <tr key={idx}>
                    <td className="py-1 font-sans">{idx + 1}</td>
                    <td className="py-1 font-sans font-medium">
                      {it.productName}
                      {it.freeQuantity > 0 && (
                        <span className="block text-[10px] text-emerald-700">+{it.freeQuantity} Free Bonus</span>
                      )}
                    </td>
                    <td className="py-1 text-center">{it.quantity}</td>
                    <td className="py-1 text-right">₹{it.purchaseRate.toFixed(2)}</td>
                    <td className="py-1 text-right font-bold">₹{it.lineTotal.toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {/* Summary */}
            <div className="border-t border-slate-300 pt-2 space-y-1 text-xs">
              <div className="flex justify-between font-mono">
                <span>Taxable Amount:</span>
                <span>₹{totals.taxableAmount.toFixed(2)}</span>
              </div>
              <div className="flex justify-between font-mono">
                <span>GST Tax Total:</span>
                <span>₹{totals.taxAmount.toFixed(2)}</span>
              </div>
              <div className="flex justify-between font-mono text-sm font-bold border-t border-slate-200 pt-1">
                <span>Grand Total:</span>
                <span>₹{totals.grandTotal.toFixed(2)}</span>
              </div>
              {totals.balanceDue > 0 && (
                <div className="flex justify-between font-mono text-rose-700 font-bold">
                  <span>Balance Payable:</span>
                  <span>₹{totals.balanceDue.toFixed(2)}</span>
                </div>
              )}
            </div>

            {/* Footer Signatures */}
            <div className="pt-8 grid grid-cols-2 text-center text-xs text-slate-600 border-t border-slate-200">
              <div>
                <div className="border-t border-slate-400 w-32 mx-auto pt-1 font-medium">Store Keeper</div>
              </div>
              <div>
                <div className="border-t border-slate-400 w-32 mx-auto pt-1 font-medium">Authorized Manager</div>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="bg-slate-100 px-4 py-2.5 border-t border-slate-200 flex justify-between items-center">
          <span className="text-slate-500 text-[11px]">System.Drawing.Printing Compliant</span>
          <div className="flex gap-2">
            <button
              onClick={onClose}
              className="px-4 py-1.5 bg-white border border-slate-300 rounded font-medium cursor-pointer"
            >
              Close
            </button>
            <button
              onClick={handlePrint}
              className="px-4 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-bold flex items-center gap-1.5 shadow-xs cursor-pointer"
            >
              <Printer className="w-4 h-4" />
              <span>Print Voucher</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
