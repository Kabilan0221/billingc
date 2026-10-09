import React, { useState, useEffect, useMemo, useRef } from 'react';
import * as XLSX from 'xlsx';
import {
  Plus,
  Edit2,
  UserX,
  UserCheck,
  Download,
  Upload,
  Search,
  RotateCcw,
  BookOpen,
  DollarSign,
  AlertCircle,
  CheckCircle,
  FileSpreadsheet,
  X,
  Phone,
  Mail,
  Building,
  CreditCard,
  ChevronLeft,
  ChevronRight,
  ShieldAlert,
} from 'lucide-react';
import { db, SupplierFilter } from '../services/dbEngine';
import { Supplier, SupplierLedgerEntry } from '../types';

interface SupplierManagementProps {
  onRefreshData?: () => void;
  onNavigateToInbound?: (supplierId?: number) => void;
}

export const SupplierManagement: React.FC<SupplierManagementProps> = ({
  onRefreshData,
  onNavigateToInbound,
}) => {
  // State
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<'active' | 'inactive' | 'all'>('active');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [selectedSupplierId, setSelectedSupplierId] = useState<number | null>(null);

  // Dialogs
  const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);
  const [editingSupplier, setEditingSupplier] = useState<Partial<Supplier> | null>(null);
  const [isImportDialogOpen, setIsImportDialogOpen] = useState(false);
  const [isLedgerDialogOpen, setIsLedgerDialogOpen] = useState(false);
  const [isPaymentDialogOpen, setIsPaymentDialogOpen] = useState(false);

  // Notification message
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const showFeedback = (type: 'success' | 'error', message: string) => {
    setFeedback({ type, message });
    setTimeout(() => setFeedback(null), 4000);
  };

  // Query suppliers from SQLite Indexed Engine
  const filter: SupplierFilter = useMemo(() => {
    return {
      searchTerm,
      isActive: statusFilter === 'all' ? undefined : statusFilter === 'active',
      sortBy: 'name',
      sortDescending: false,
      pageNumber: currentPage,
      pageSize,
    };
  }, [searchTerm, statusFilter, currentPage, pageSize]);

  const pagedResult = useMemo(() => {
    return db.searchSuppliers(filter);
  }, [filter, editingSupplier, isImportDialogOpen, isPaymentDialogOpen]);

  const selectedSupplier = useMemo(() => {
    if (!selectedSupplierId) return pagedResult.items[0] || null;
    return db.getSupplierById(selectedSupplierId) || null;
  }, [selectedSupplierId, pagedResult.items]);

  // Set default selection
  useEffect(() => {
    if (pagedResult.items.length > 0 && !selectedSupplierId) {
      setSelectedSupplierId(pagedResult.items[0].id);
    }
  }, [pagedResult.items, selectedSupplierId]);

  // Handlers
  const handleAddNew = () => {
    setEditingSupplier({
      code: db.generateNextSupplierCode(),
      name: '',
      contactPerson: '',
      mobile: '',
      altMobile: '',
      whatsapp: '',
      email: '',
      gstin: '',
      pan: '',
      address: '',
      city: 'Chennai',
      state: 'Tamil Nadu',
      stateCode: '33',
      pinCode: '600001',
      openingBalance: 0,
      currentBalance: 0,
      paymentTerms: '30 Days Net',
      notes: '',
      isActive: true,
    });
    setIsEditDialogOpen(true);
  };

  const handleEdit = (supplier: Supplier) => {
    setEditingSupplier({ ...supplier });
    setIsEditDialogOpen(true);
  };

  const handleToggleActive = (supplier: Supplier) => {
    if (supplier.isActive) {
      if (confirm(`Are you sure you want to deactivate supplier "${supplier.name}" (${supplier.code})?`)) {
        db.deactivateSupplier(supplier.id);
        showFeedback('success', `Supplier '${supplier.name}' deactivated successfully.`);
        if (onRefreshData) onRefreshData();
      }
    } else {
      db.activateSupplier(supplier.id);
      showFeedback('success', `Supplier '${supplier.name}' re-activated.`);
      if (onRefreshData) onRefreshData();
    }
  };

  const handleDelete = (supplier: Supplier) => {
    if (confirm(`Do you wish to delete supplier "${supplier.name}"?`)) {
      const res = db.deleteSupplier(supplier.id);
      if (res.success) {
        showFeedback('success', `Supplier deleted.`);
        setSelectedSupplierId(null);
        if (onRefreshData) onRefreshData();
      } else {
        showFeedback('error', res.error || 'Failed to delete supplier.');
      }
    }
  };

  const handleExportExcel = () => {
    const all = db.getAllSuppliers();
    const rows = all.map((s) => ({
      'Supplier Code': s.code,
      'Supplier Name': s.name,
      'Contact Person': s.contactPerson || '',
      'Mobile Number': s.mobile || '',
      'Alt Mobile': s.altMobile || '',
      'WhatsApp': s.whatsapp || '',
      'Email': s.email || '',
      'GSTIN': s.gstin || '',
      'PAN': s.pan || '',
      'Address': s.address || '',
      'City': s.city || '',
      'State': s.state || '',
      'PIN Code': s.pinCode || '',
      'Payment Terms': s.paymentTerms || '',
      'Opening Balance (₹)': s.openingBalance,
      'Current Balance (₹)': s.currentBalance,
      'Status': s.isActive ? 'Active' : 'Inactive',
      'Notes': s.notes || '',
    }));

    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Suppliers');
    const filename = `ShopBilling_Suppliers_${new Date().toISOString().slice(0, 10)}.xlsx`;
    XLSX.writeFile(wb, filename);
    showFeedback('success', `Exported ${all.length} suppliers to ${filename}`);
  };

  return (
    <div className="flex-1 flex flex-col h-full bg-white select-none overflow-hidden">
      {/* 1. Main Action Toolbar (WinForms Style) */}
      <div className="bg-[#f0f4f9] border-b border-[#c8d4e2] px-3 py-2 flex flex-wrap items-center justify-between gap-2 text-xs">
        <div className="flex items-center gap-1.5 flex-wrap">
          <button
            onClick={handleAddNew}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-medium shadow-xs transition-colors cursor-pointer"
            title="Create a new supplier profile (F2)"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>+ Add Supplier</span>
            <span className="text-[10px] bg-white/20 px-1 py-0.2 rounded font-mono ml-0.5">F2</span>
          </button>

          <button
            onClick={() => selectedSupplier && handleEdit(selectedSupplier)}
            disabled={!selectedSupplier}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded font-medium transition-colors disabled:opacity-40 cursor-pointer"
            title="Edit selected supplier"
          >
            <Edit2 className="w-3.5 h-3.5 text-blue-600" />
            <span>Edit</span>
          </button>

          <button
            onClick={() => selectedSupplier && handleToggleActive(selectedSupplier)}
            disabled={!selectedSupplier}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded font-medium transition-colors disabled:opacity-40 border cursor-pointer ${
              selectedSupplier?.isActive
                ? 'bg-white border-slate-300 hover:bg-rose-50 text-rose-700'
                : 'bg-white border-slate-300 hover:bg-emerald-50 text-emerald-700'
            }`}
            title="Activate / Deactivate supplier"
          >
            {selectedSupplier?.isActive ? (
              <>
                <UserX className="w-3.5 h-3.5 text-rose-600" />
                <span>Deactivate</span>
              </>
            ) : (
              <>
                <UserCheck className="w-3.5 h-3.5 text-emerald-600" />
                <span>Re-Activate</span>
              </>
            )}
          </button>

          <div className="w-[1px] h-5 bg-slate-300 mx-1" />

          <button
            onClick={() => selectedSupplier && setIsLedgerDialogOpen(true)}
            disabled={!selectedSupplier}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded font-medium transition-colors disabled:opacity-40 cursor-pointer"
            title="View statement and transaction ledger"
          >
            <BookOpen className="w-3.5 h-3.5 text-indigo-600" />
            <span>Ledger & Statement</span>
          </button>

          <button
            onClick={() => selectedSupplier && setIsPaymentDialogOpen(true)}
            disabled={!selectedSupplier}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded font-medium transition-colors disabled:opacity-40 cursor-pointer"
            title="Record supplier payment"
          >
            <DollarSign className="w-3.5 h-3.5 text-emerald-600" />
            <span>Make Payment</span>
          </button>

          {onNavigateToInbound && (
            <button
              onClick={() => onNavigateToInbound(selectedSupplier?.id)}
              disabled={!selectedSupplier}
              className="flex items-center gap-1.5 px-3 py-1.5 bg-amber-50 border border-amber-300 hover:bg-amber-100 text-amber-900 rounded font-medium transition-colors disabled:opacity-40 cursor-pointer"
              title="Record inbound goods invoice from this supplier"
            >
              <Building className="w-3.5 h-3.5 text-amber-700" />
              <span>+ Purchase Entry (F3)</span>
            </button>
          )}
        </div>

        <div className="flex items-center gap-1.5">
          <button
            onClick={() => setIsImportDialogOpen(true)}
            className="flex items-center gap-1.5 px-2.5 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded transition-colors cursor-pointer"
            title="Import suppliers from Excel spreadsheet"
          >
            <Upload className="w-3.5 h-3.5 text-emerald-600" />
            <span>Import Excel</span>
          </button>

          <button
            onClick={handleExportExcel}
            className="flex items-center gap-1.5 px-2.5 py-1.5 bg-white border border-slate-300 hover:bg-slate-50 text-slate-700 rounded transition-colors cursor-pointer"
            title="Export supplier list to Microsoft Excel"
          >
            <Download className="w-3.5 h-3.5 text-emerald-600" />
            <span>Export Excel</span>
          </button>
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
              <CheckCircle className="w-4 h-4 text-emerald-600 shrink-0" />
            ) : (
              <AlertCircle className="w-4 h-4 text-rose-600 shrink-0" />
            )}
            <span>{feedback.message}</span>
          </div>
          <button onClick={() => setFeedback(null)} className="text-slate-400 hover:text-slate-600">
            <X className="w-3.5 h-3.5" />
          </button>
        </div>
      )}

      {/* 2. Search & Filter Bar */}
      <div className="bg-[#fafbfc] border-b border-[#e2e8f0] px-3 py-2 flex flex-wrap items-center justify-between gap-3 text-xs">
        <div className="flex items-center gap-2 flex-1 max-w-2xl">
          <div className="relative flex-1">
            <Search className="w-3.5 h-3.5 text-slate-400 absolute left-2.5 top-1/2 -translate-y-1/2" />
            <input
              type="text"
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setCurrentPage(1);
              }}
              placeholder="Search by Supplier Name, Code, Mobile, GSTIN, Contact Person, City..."
              className="w-full pl-8 pr-2 py-1.5 bg-white border border-slate-300 rounded focus:outline-hidden focus:border-blue-500 font-mono text-xs placeholder:font-sans"
            />
            {searchTerm && (
              <button
                onClick={() => setSearchTerm('')}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>

          <div className="flex items-center gap-1.5 shrink-0">
            <span className="text-slate-500 font-medium">Status:</span>
            <select
              value={statusFilter}
              onChange={(e) => {
                setStatusFilter(e.target.value as any);
                setCurrentPage(1);
              }}
              className="px-2 py-1.5 bg-white border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
            >
              <option value="active">Active Only</option>
              <option value="inactive">Inactive Only</option>
              <option value="all">All Suppliers</option>
            </select>
          </div>

          <button
            onClick={() => {
              setSearchTerm('');
              setStatusFilter('active');
              setCurrentPage(1);
            }}
            className="flex items-center gap-1 px-2.5 py-1.5 bg-white border border-slate-300 hover:bg-slate-100 rounded text-slate-600"
            title="Reset filters"
          >
            <RotateCcw className="w-3 h-3" />
            <span>Reset</span>
          </button>
        </div>

        {/* Quick Balance Summary pill */}
        <div className="flex items-center gap-3 text-xs text-slate-600">
          <span className="bg-slate-100 border border-slate-200 px-2.5 py-1 rounded">
            Total Suppliers: <strong className="text-slate-900 font-mono">{pagedResult.totalCount}</strong>
          </span>
          <span className="bg-rose-50 border border-rose-200 text-rose-800 px-2.5 py-1 rounded">
            Total Payable:{' '}
            <strong className="font-mono text-rose-900">
              ₹
              {db
                .getAllSuppliers()
                .reduce((sum, s) => sum + (s.currentBalance > 0 ? s.currentBalance : 0), 0)
                .toLocaleString('en-IN', { minimumFractionDigits: 2 })}
            </strong>
          </span>
        </div>
      </div>

      {/* 3. Main DataGridView & Details Split View */}
      <div className="flex-1 flex overflow-hidden">
        {/* Table View */}
        <div className="flex-1 flex flex-col overflow-hidden border-r border-[#d8e2ec]">
          <div className="flex-1 overflow-auto custom-scrollbar">
            <table className="w-full text-left border-collapse text-xs">
              <thead className="bg-[#1f4e79] text-white sticky top-0 z-10 select-none shadow-xs">
                <tr>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40 w-12 text-center">#</th>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40 w-24">Code</th>
                  <th className="py-2 px-3 border-b border-r border-blue-900/40">Supplier Name</th>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40">Contact Person</th>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40">Mobile</th>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40 font-mono">GSTIN</th>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40">City / State</th>
                  <th className="py-2 px-2.5 border-b border-r border-blue-900/40">Payment Terms</th>
                  <th className="py-2 px-3 border-b border-r border-blue-900/40 text-right">Balance Due (₹)</th>
                  <th className="py-2 px-2.5 border-b text-center w-20">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {pagedResult.items.length === 0 ? (
                  <tr>
                    <td colSpan={10} className="py-12 text-center text-slate-400">
                      <div className="flex flex-col items-center justify-center gap-2">
                        <Building className="w-8 h-8 text-slate-300" />
                        <p className="font-medium text-slate-600">No suppliers found matching your query.</p>
                        <p className="text-[11px] text-slate-400">
                          Click &quot;+ Add Supplier&quot; to register a new vendor.
                        </p>
                      </div>
                    </td>
                  </tr>
                ) : (
                  pagedResult.items.map((supplier, idx) => {
                    const isSelected = selectedSupplierId === supplier.id;
                    const rowNum = (pagedResult.pageNumber - 1) * pagedResult.pageSize + idx + 1;
                    const hasBalance = supplier.currentBalance > 0;

                    return (
                      <tr
                        key={supplier.id}
                        onClick={() => setSelectedSupplierId(supplier.id)}
                        onDoubleClick={() => handleEdit(supplier)}
                        className={`cursor-pointer transition-colors ${
                          isSelected
                            ? 'bg-[#cce8ff] text-slate-900 font-medium'
                            : idx % 2 === 1
                            ? 'bg-[#fcfdfd] hover:bg-blue-50/50'
                            : 'bg-white hover:bg-blue-50/50'
                        }`}
                      >
                        <td className="py-1.5 px-2.5 text-center text-slate-400 font-mono">{rowNum}</td>
                        <td className="py-1.5 px-2.5 font-mono font-semibold text-blue-900">{supplier.code}</td>
                        <td className="py-1.5 px-3 font-semibold text-slate-800">
                          {supplier.name}
                          {supplier.notes && (
                            <span className="block text-[10px] font-normal text-slate-500 truncate max-w-xs">
                              {supplier.notes}
                            </span>
                          )}
                        </td>
                        <td className="py-1.5 px-2.5 text-slate-700">{supplier.contactPerson || '—'}</td>
                        <td className="py-1.5 px-2.5 font-mono text-slate-700">{supplier.mobile || '—'}</td>
                        <td className="py-1.5 px-2.5 font-mono text-[11px] text-slate-600">
                          {supplier.gstin ? (
                            <span className="bg-slate-100 px-1 py-0.5 rounded border border-slate-200">
                              {supplier.gstin}
                            </span>
                          ) : (
                            <span className="text-slate-400 italic">Unregistered</span>
                          )}
                        </td>
                        <td className="py-1.5 px-2.5 text-slate-600">
                          {supplier.city ? `${supplier.city}, ${supplier.state}` : supplier.state || '—'}
                        </td>
                        <td className="py-1.5 px-2.5 text-slate-600">{supplier.paymentTerms || '—'}</td>
                        <td className="py-1.5 px-3 text-right font-mono font-bold">
                          {hasBalance ? (
                            <span className="text-rose-700 bg-rose-50 px-1.5 py-0.5 rounded border border-rose-200">
                              ₹{supplier.currentBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                            </span>
                          ) : (
                            <span className="text-emerald-700">₹0.00</span>
                          )}
                        </td>
                        <td className="py-1.5 px-2.5 text-center">
                          {supplier.isActive ? (
                            <span className="inline-block px-1.5 py-0.5 rounded text-[10px] font-medium bg-emerald-100 text-emerald-800 border border-emerald-200">
                              Active
                            </span>
                          ) : (
                            <span className="inline-block px-1.5 py-0.5 rounded text-[10px] font-medium bg-slate-100 text-slate-600 border border-slate-200">
                              Inactive
                            </span>
                          )}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination Strip (WinForms Status Style) */}
          <div className="bg-[#f0f4f9] border-t border-[#c8d4e2] px-3 py-1.5 flex items-center justify-between text-xs text-slate-600 select-none">
            <div className="flex items-center gap-3">
              <span>
                Showing{' '}
                <strong className="text-slate-800">
                  {pagedResult.items.length > 0 ? (pagedResult.pageNumber - 1) * pagedResult.pageSize + 1 : 0}
                </strong>{' '}
                to{' '}
                <strong className="text-slate-800">
                  {Math.min(pagedResult.pageNumber * pagedResult.pageSize, pagedResult.totalCount)}
                </strong>{' '}
                of <strong className="text-slate-800">{pagedResult.totalCount}</strong> records
              </span>
              <span className="text-slate-400">|</span>
              <span className="font-mono text-[11px] text-slate-500">
                Query Time: {pagedResult.executionTimeMs} ms (Indexed)
              </span>
            </div>

            <div className="flex items-center gap-2">
              <span className="text-slate-500">Page size:</span>
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setCurrentPage(1);
                }}
                className="bg-white border border-slate-300 rounded px-1.5 py-0.5 text-xs"
              >
                <option value={15}>15</option>
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>

              <div className="flex items-center gap-1 ml-2">
                <button
                  disabled={currentPage <= 1}
                  onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                  className="px-2 py-0.5 bg-white border border-slate-300 rounded hover:bg-slate-100 disabled:opacity-40"
                >
                  <ChevronLeft className="w-3.5 h-3.5" />
                </button>
                <span className="font-mono px-2 font-medium">
                  {currentPage} / {Math.max(1, pagedResult.totalPages)}
                </span>
                <button
                  disabled={currentPage >= pagedResult.totalPages}
                  onClick={() => setCurrentPage((p) => Math.min(pagedResult.totalPages, p + 1))}
                  className="px-2 py-0.5 bg-white border border-slate-300 rounded hover:bg-slate-100 disabled:opacity-40"
                >
                  <ChevronRight className="w-3.5 h-3.5" />
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Selected Supplier Details Side Panel (Desktop Property Inspector) */}
        {selectedSupplier ? (
          <div className="w-80 shrink-0 bg-[#f8fafc] flex flex-col overflow-y-auto custom-scrollbar border-l border-slate-200 p-3 space-y-4 text-xs">
            {/* Header Card */}
            <div className="bg-white rounded border border-slate-200 p-3 shadow-xs space-y-1.5">
              <div className="flex items-center justify-between">
                <span className="font-mono font-bold text-blue-700 bg-blue-50 px-2 py-0.5 rounded border border-blue-200">
                  {selectedSupplier.code}
                </span>
                {selectedSupplier.isActive ? (
                  <span className="px-1.5 py-0.5 rounded text-[10px] bg-emerald-100 text-emerald-800 font-medium">
                    Active
                  </span>
                ) : (
                  <span className="px-1.5 py-0.5 rounded text-[10px] bg-slate-200 text-slate-700 font-medium">
                    Inactive
                  </span>
                )}
              </div>
              <h3 className="text-sm font-bold text-slate-900 leading-tight">{selectedSupplier.name}</h3>
              {selectedSupplier.contactPerson && (
                <p className="text-slate-600">Contact: {selectedSupplier.contactPerson}</p>
              )}
            </div>

            {/* Financial Card */}
            <div className="bg-white rounded border border-slate-200 p-3 shadow-xs space-y-2">
              <div className="text-[11px] font-bold uppercase tracking-wider text-slate-500">Account Summary</div>
              <div className="flex justify-between items-center py-1 border-b border-slate-100">
                <span className="text-slate-600">Opening Balance:</span>
                <span className="font-mono font-medium">
                  ₹{selectedSupplier.openingBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
              </div>
              <div className="flex justify-between items-center py-1">
                <span className="text-slate-700 font-medium">Current Payable:</span>
                <span className={`font-mono text-sm font-bold ${selectedSupplier.currentBalance > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
                  ₹{selectedSupplier.currentBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                </span>
              </div>
              <div className="flex justify-between items-center py-1 text-slate-600 border-t border-slate-100 pt-1">
                <span>Payment Terms:</span>
                <span className="font-medium text-slate-800">{selectedSupplier.paymentTerms || 'Not Set'}</span>
              </div>
              <div className="pt-2 flex gap-1.5">
                <button
                  onClick={() => setIsPaymentDialogOpen(true)}
                  className="flex-1 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-medium text-center shadow-xs cursor-pointer"
                >
                  Pay Vendor
                </button>
                <button
                  onClick={() => setIsLedgerDialogOpen(true)}
                  className="flex-1 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 border border-slate-300 rounded font-medium text-center cursor-pointer"
                >
                  View Ledger
                </button>
              </div>
            </div>

            {/* Contact Details Card */}
            <div className="bg-white rounded border border-slate-200 p-3 shadow-xs space-y-2">
              <div className="text-[11px] font-bold uppercase tracking-wider text-slate-500">Contact & Address</div>
              <div className="space-y-1.5 text-slate-700">
                {selectedSupplier.mobile && (
                  <div className="flex items-center gap-2">
                    <Phone className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                    <span className="font-mono">{selectedSupplier.mobile}</span>
                  </div>
                )}
                {selectedSupplier.email && (
                  <div className="flex items-center gap-2">
                    <Mail className="w-3.5 h-3.5 text-slate-400 shrink-0" />
                    <span className="truncate">{selectedSupplier.email}</span>
                  </div>
                )}
                {selectedSupplier.address && (
                  <div className="pt-1 text-slate-600 border-t border-slate-100">
                    <p>{selectedSupplier.address}</p>
                    <p>
                      {selectedSupplier.city}, {selectedSupplier.state} - {selectedSupplier.pinCode}
                    </p>
                  </div>
                )}
              </div>
            </div>

            {/* Tax & Statutory */}
            <div className="bg-white rounded border border-slate-200 p-3 shadow-xs space-y-1.5">
              <div className="text-[11px] font-bold uppercase tracking-wider text-slate-500">Statutory / GST</div>
              <div className="flex justify-between items-center">
                <span className="text-slate-500">GSTIN:</span>
                <span className="font-mono font-semibold text-slate-800">
                  {selectedSupplier.gstin || 'Unregistered'}
                </span>
              </div>
              {selectedSupplier.pan && (
                <div className="flex justify-between items-center">
                  <span className="text-slate-500">PAN:</span>
                  <span className="font-mono text-slate-800">{selectedSupplier.pan}</span>
                </div>
              )}
            </div>

            {/* Quick Actions at Bottom */}
            <div className="pt-2 space-y-1.5">
              <button
                onClick={() => handleEdit(selectedSupplier)}
                className="w-full py-1.5 bg-blue-50 border border-blue-200 hover:bg-blue-100 text-blue-800 rounded font-medium flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <Edit2 className="w-3.5 h-3.5" />
                <span>Edit Full Profile</span>
              </button>

              <button
                onClick={() => handleDelete(selectedSupplier)}
                className="w-full py-1.5 bg-rose-50 border border-rose-200 hover:bg-rose-100 text-rose-700 rounded font-medium flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <ShieldAlert className="w-3.5 h-3.5" />
                <span>Delete Supplier (Check DB)</span>
              </button>
            </div>
          </div>
        ) : null}
      </div>

      {/* 4. Add / Edit Supplier Dialog Modal */}
      {isEditDialogOpen && editingSupplier && (
        <SupplierEditDialogModal
          supplier={editingSupplier}
          onClose={() => setIsEditDialogOpen(false)}
          onSave={(saved) => {
            setIsEditDialogOpen(false);
            setSelectedSupplierId(saved.id);
            showFeedback('success', `Supplier '${saved.name}' (${saved.code}) saved successfully.`);
            if (onRefreshData) onRefreshData();
          }}
        />
      )}

      {/* 5. Excel Import Dialog Modal */}
      {isImportDialogOpen && (
        <SupplierExcelImportModal
          onClose={() => setIsImportDialogOpen(false)}
          onComplete={(count) => {
            setIsImportDialogOpen(false);
            showFeedback('success', `Successfully imported ${count} suppliers.`);
            if (onRefreshData) onRefreshData();
          }}
        />
      )}

      {/* 6. Supplier Ledger Statement Modal */}
      {isLedgerDialogOpen && selectedSupplier && (
        <SupplierLedgerModal
          supplier={selectedSupplier}
          onClose={() => setIsLedgerDialogOpen(false)}
          onOpenPayment={() => {
            setIsLedgerDialogOpen(false);
            setIsPaymentDialogOpen(true);
          }}
        />
      )}

      {/* 7. Record Payment Modal */}
      {isPaymentDialogOpen && selectedSupplier && (
        <SupplierPaymentModal
          supplier={selectedSupplier}
          onClose={() => setIsPaymentDialogOpen(false)}
          onPaymentSuccess={() => {
            setIsPaymentDialogOpen(false);
            showFeedback('success', `Payment recorded successfully.`);
            if (onRefreshData) onRefreshData();
          }}
        />
      )}
    </div>
  );
};

// -----------------------------------------------------------------------------------------
// MODAL: Supplier Edit Dialog
// -----------------------------------------------------------------------------------------
interface SupplierEditDialogModalProps {
  supplier: Partial<Supplier>;
  onClose: () => void;
  onSave: (saved: Supplier) => void;
}

const SupplierEditDialogModal: React.FC<SupplierEditDialogModalProps> = ({ supplier, onClose, onSave }) => {
  const [formData, setFormData] = useState<Partial<Supplier>>({ ...supplier });
  const [errors, setErrors] = useState<string[]>([]);

  const handleChange = (field: keyof Supplier, value: any) => {
    setFormData((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrors([]);

    const res = db.saveSupplier(formData);
    if (res.success && res.supplier) {
      onSave(res.supplier);
    } else {
      setErrors(res.errors);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-3">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-2xl flex flex-col max-h-[92vh] overflow-hidden">
        {/* Title */}
        <div className="bg-[#1f4e79] text-white px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Building className="w-4 h-4 text-blue-200" />
            <h3 className="font-bold text-sm">
              {formData.id ? `Edit Supplier: ${formData.name}` : 'New Supplier Registration'}
            </h3>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-4 space-y-4 text-xs">
          {errors.length > 0 && (
            <div className="p-3 bg-rose-50 border border-rose-200 rounded text-rose-800 space-y-1">
              <div className="font-bold flex items-center gap-1.5">
                <AlertCircle className="w-4 h-4 text-rose-600" />
                <span>Validation Errors:</span>
              </div>
              <ul className="list-disc list-inside space-y-0.5 pl-2">
                {errors.map((err, i) => (
                  <li key={i}>{err}</li>
                ))}
              </ul>
            </div>
          )}

          {/* Core Identification */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="block text-slate-700 font-bold mb-1">
                Supplier Code <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={formData.code || ''}
                onChange={(e) => handleChange('code', e.target.value.toUpperCase())}
                placeholder="SUP001"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono uppercase bg-slate-50 focus:bg-white focus:outline-hidden focus:border-blue-500"
                required
              />
            </div>

            <div className="md:col-span-2">
              <label className="block text-slate-700 font-bold mb-1">
                Supplier / Business Name <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                value={formData.name || ''}
                onChange={(e) => handleChange('name', e.target.value)}
                placeholder="e.g. Sri Meenakshi Trading Co"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500 font-semibold"
                required
              />
            </div>
          </div>

          {/* Contact Details */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
            <div>
              <label className="block text-slate-700 font-medium mb-1">Contact Person</label>
              <input
                type="text"
                value={formData.contactPerson || ''}
                onChange={(e) => handleChange('contactPerson', e.target.value)}
                placeholder="e.g. K. Ramasamy"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">Mobile Number (10 digits)</label>
              <input
                type="tel"
                maxLength={10}
                value={formData.mobile || ''}
                onChange={(e) => handleChange('mobile', e.target.value.replace(/[^0-9]/g, ''))}
                placeholder="9840123456"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">Alternate / Landline</label>
              <input
                type="tel"
                value={formData.altMobile || ''}
                onChange={(e) => handleChange('altMobile', e.target.value)}
                placeholder="0452-2345678"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-slate-700 font-medium mb-1">WhatsApp Number</label>
              <input
                type="tel"
                maxLength={10}
                value={formData.whatsapp || ''}
                onChange={(e) => handleChange('whatsapp', e.target.value.replace(/[^0-9]/g, ''))}
                placeholder="9840123456"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">Email Address</label>
              <input
                type="email"
                value={formData.email || ''}
                onChange={(e) => handleChange('email', e.target.value)}
                placeholder="sales@vendor.com"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          {/* Statutory (GSTIN & PAN) */}
          <div className="bg-slate-50 p-3 rounded border border-slate-200 grid grid-cols-1 md:grid-cols-2 gap-3">
            <div>
              <label className="block text-slate-700 font-bold mb-1">
                GSTIN (15 Alphanumeric)
              </label>
              <input
                type="text"
                maxLength={15}
                value={formData.gstin || ''}
                onChange={(e) => {
                  const val = e.target.value.toUpperCase();
                  handleChange('gstin', val);
                  if (val.length >= 12 && !formData.pan) {
                    handleChange('pan', val.substring(2, 12));
                  }
                }}
                placeholder="33AABCS1429B1Z1"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono uppercase bg-white focus:outline-hidden focus:border-blue-500"
              />
              <span className="text-[10px] text-slate-500">e.g. 33 (State Code) + 10-char PAN + 1Z1</span>
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">PAN Number</label>
              <input
                type="text"
                maxLength={10}
                value={formData.pan || ''}
                onChange={(e) => handleChange('pan', e.target.value.toUpperCase())}
                placeholder="AABCS1429B"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono uppercase bg-white focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          {/* Address */}
          <div>
            <label className="block text-slate-700 font-medium mb-1">Door No & Street Address</label>
            <input
              type="text"
              value={formData.address || ''}
              onChange={(e) => handleChange('address', e.target.value)}
              placeholder="e.g. 45 East Veli Street"
              className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
            />
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
            <div>
              <label className="block text-slate-700 font-medium mb-1">City</label>
              <input
                type="text"
                value={formData.city || ''}
                onChange={(e) => handleChange('city', e.target.value)}
                placeholder="Madurai"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">State</label>
              <input
                type="text"
                value={formData.state || ''}
                onChange={(e) => handleChange('state', e.target.value)}
                placeholder="Tamil Nadu"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">State Code</label>
              <input
                type="text"
                maxLength={2}
                value={formData.stateCode || ''}
                onChange={(e) => handleChange('stateCode', e.target.value)}
                placeholder="33"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
              />
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">PIN Code</label>
              <input
                type="text"
                maxLength={6}
                value={formData.pinCode || ''}
                onChange={(e) => handleChange('pinCode', e.target.value)}
                placeholder="625001"
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500"
              />
            </div>
          </div>

          {/* Commercial & Terms */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3 border-t border-slate-200 pt-3">
            <div>
              <label className="block text-slate-700 font-medium mb-1">Payment Terms</label>
              <select
                value={formData.paymentTerms || '30 Days Net'}
                onChange={(e) => handleChange('paymentTerms', e.target.value)}
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded bg-white focus:outline-hidden focus:border-blue-500"
              >
                <option value="Immediate Cash">Immediate Cash</option>
                <option value="7 Days Net">7 Days Net</option>
                <option value="15 Days Net">15 Days Net</option>
                <option value="30 Days Net">30 Days Net</option>
                <option value="45 Days Net">45 Days Net</option>
                <option value="60 Days Net">60 Days Net</option>
              </select>
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">Opening Balance (₹)</label>
              <input
                type="number"
                step="0.01"
                disabled={!!formData.id}
                value={formData.openingBalance ?? 0}
                onChange={(e) => handleChange('openingBalance', parseFloat(e.target.value) || 0)}
                className="w-full px-2.5 py-1.5 border border-slate-300 rounded font-mono focus:outline-hidden focus:border-blue-500 disabled:bg-slate-100"
              />
              {formData.id && <span className="text-[10px] text-slate-500">Edit via Payment/Ledger</span>}
            </div>

            <div className="flex items-center gap-2 pt-5">
              <input
                type="checkbox"
                id="supplierActive"
                checked={formData.isActive ?? true}
                onChange={(e) => handleChange('isActive', e.target.checked)}
                className="w-4 h-4 text-blue-600 rounded"
              />
              <label htmlFor="supplierActive" className="text-slate-700 font-semibold cursor-pointer">
                Active Supplier
              </label>
            </div>
          </div>

          <div>
            <label className="block text-slate-700 font-medium mb-1">Notes / Internal Remarks</label>
            <textarea
              rows={2}
              value={formData.notes || ''}
              onChange={(e) => handleChange('notes', e.target.value)}
              placeholder="e.g. Preferred delivery morning slots, authorized distributor..."
              className="w-full px-2.5 py-1.5 border border-slate-300 rounded focus:outline-hidden focus:border-blue-500"
            />
          </div>

          {/* Footer Actions */}
          <div className="bg-slate-50 -mx-4 -mb-4 px-4 py-3 border-t border-slate-200 flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-1.5 border border-slate-300 rounded hover:bg-slate-100 font-medium cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-5 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-medium shadow-xs cursor-pointer"
            >
              Save Supplier
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

// -----------------------------------------------------------------------------------------
// MODAL: Supplier Excel Import
// -----------------------------------------------------------------------------------------
interface SupplierExcelImportModalProps {
  onClose: () => void;
  onComplete: (count: number) => void;
}

const SupplierExcelImportModal: React.FC<SupplierExcelImportModalProps> = ({ onClose, onComplete }) => {
  const [parsedRows, setParsedRows] = useState<any[]>([]);
  const [importSummary, setImportSummary] = useState<{ imported: number; failed: number; errors: any[] } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleDownloadTemplate = () => {
    const templateData = [
      {
        'Supplier Code': 'SUP101',
        'Supplier Name': 'Sample Super Spices Co',
        'Contact Person': 'R. Rajesh',
        'Mobile Number': '9876543210',
        'Email': 'rajesh@superspices.com',
        'GSTIN': '33AABCS1234A1Z5',
        'Address': '12 Market Lane',
        'City': 'Madurai',
        'State': 'Tamil Nadu',
        'PIN Code': '625001',
        'Payment Terms': '30 Days Net',
        'Opening Balance': 0,
      },
    ];

    const ws = XLSX.utils.json_to_sheet(templateData);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Template');
    XLSX.writeFile(wb, 'Supplier_Import_Template.xlsx');
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (evt) => {
      try {
        const bstr = evt.target?.result;
        const wb = XLSX.read(bstr, { type: 'binary' });
        const wsname = wb.SheetNames[0];
        const ws = wb.Sheets[wsname];
        const data = XLSX.utils.sheet_to_json(ws);
        setParsedRows(data);
        setImportSummary(null);
      } catch (err) {
        alert('Failed to parse Excel file. Please ensure it is a valid .xlsx or .xls file.');
      }
    };
    reader.readAsBinaryString(file);
  };

  const handleProcessImport = () => {
    const formatted: Partial<Supplier>[] = parsedRows.map((r) => ({
      code: r['Supplier Code'] || r['Code'],
      name: r['Supplier Name'] || r['Name'],
      contactPerson: r['Contact Person'],
      mobile: r['Mobile Number'] ? String(r['Mobile Number']) : undefined,
      email: r['Email'],
      gstin: r['GSTIN'],
      address: r['Address'],
      city: r['City'],
      state: r['State'] || 'Tamil Nadu',
      pinCode: r['PIN Code'] ? String(r['PIN Code']) : undefined,
      paymentTerms: r['Payment Terms'] || '30 Days Net',
      openingBalance: parseFloat(r['Opening Balance']) || 0,
    }));

    const result = db.bulkImportSuppliers(formatted);
    setImportSummary(result);
    if (result.imported > 0 && result.failed === 0) {
      setTimeout(() => onComplete(result.imported), 1500);
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-3">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-2xl flex flex-col max-h-[85vh] overflow-hidden text-xs">
        <div className="bg-[#1f4e79] text-white px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FileSpreadsheet className="w-4 h-4 text-emerald-300" />
            <h3 className="font-bold text-sm">Bulk Import Suppliers via Excel</h3>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-4 flex-1 overflow-y-auto space-y-4">
          <div className="flex items-center justify-between bg-slate-50 p-3 rounded border border-slate-200">
            <div>
              <div className="font-bold text-slate-800">Download Official Excel Template</div>
              <p className="text-[11px] text-slate-500">Includes correct headers and validation formats</p>
            </div>
            <button
              onClick={handleDownloadTemplate}
              className="px-3 py-1.5 bg-white border border-slate-300 hover:bg-slate-100 rounded text-slate-700 font-medium flex items-center gap-1.5 cursor-pointer"
            >
              <Download className="w-3.5 h-3.5 text-blue-600" />
              <span>Download .xlsx</span>
            </button>
          </div>

          <div className="border-2 border-dashed border-slate-300 rounded-lg p-6 text-center space-y-2 hover:bg-slate-50">
            <Upload className="w-8 h-8 text-slate-400 mx-auto" />
            <p className="font-medium text-slate-700">Select an Excel file to import</p>
            <input
              type="file"
              accept=".xlsx, .xls"
              ref={fileInputRef}
              onChange={handleFileUpload}
              className="hidden"
            />
            <button
              onClick={() => fileInputRef.current?.click()}
              className="px-4 py-1.5 bg-[#106ebe] text-white rounded font-medium cursor-pointer"
            >
              Browse File...
            </button>
          </div>

          {parsedRows.length > 0 && !importSummary && (
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="font-bold text-slate-800">Preview ({parsedRows.length} rows loaded)</span>
                <button
                  onClick={handleProcessImport}
                  className="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-bold cursor-pointer"
                >
                  Confirm & Import to SQLite Database
                </button>
              </div>
              <div className="max-h-48 overflow-auto border border-slate-200 rounded">
                <table className="w-full text-left text-[11px]">
                  <thead className="bg-slate-100 text-slate-700 sticky top-0">
                    <tr>
                      <th className="p-1.5">Code</th>
                      <th className="p-1.5">Supplier Name</th>
                      <th className="p-1.5">Mobile</th>
                      <th className="p-1.5">City</th>
                      <th className="p-1.5">GSTIN</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {parsedRows.slice(0, 10).map((row, idx) => (
                      <tr key={idx}>
                        <td className="p-1.5 font-mono">{row['Supplier Code'] || 'Auto'}</td>
                        <td className="p-1.5 font-medium">{row['Supplier Name']}</td>
                        <td className="p-1.5 font-mono">{row['Mobile Number']}</td>
                        <td className="p-1.5">{row['City']}</td>
                        <td className="p-1.5 font-mono">{row['GSTIN']}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {importSummary && (
            <div className="p-4 bg-slate-50 border border-slate-200 rounded space-y-3">
              <h4 className="font-bold text-slate-900">Import Report Summary</h4>
              <div className="grid grid-cols-2 gap-3">
                <div className="p-2.5 bg-emerald-50 border border-emerald-200 rounded text-emerald-800 font-bold">
                  Imported Successfully: {importSummary.imported}
                </div>
                <div className="p-2.5 bg-rose-50 border border-rose-200 rounded text-rose-800 font-bold">
                  Failed: {importSummary.failed}
                </div>
              </div>

              {importSummary.errors.length > 0 && (
                <div className="space-y-1">
                  <div className="font-semibold text-rose-700">Error Details:</div>
                  <div className="max-h-36 overflow-y-auto space-y-1 border border-rose-200 bg-white p-2 rounded">
                    {importSummary.errors.map((e, i) => (
                      <div key={i} className="text-[11px] text-rose-600">
                        Row {e.row} ({e.name}): {e.error}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <div className="bg-slate-100 px-4 py-2.5 border-t border-slate-200 flex justify-end">
          <button onClick={onClose} className="px-4 py-1.5 bg-white border border-slate-300 rounded font-medium">
            Close
          </button>
        </div>
      </div>
    </div>
  );
};

// -----------------------------------------------------------------------------------------
// MODAL: Supplier Ledger & Statement
// -----------------------------------------------------------------------------------------
interface SupplierLedgerModalProps {
  supplier: Supplier;
  onClose: () => void;
  onOpenPayment: () => void;
}

const SupplierLedgerModal: React.FC<SupplierLedgerModalProps> = ({ supplier, onClose, onOpenPayment }) => {
  const ledger = useMemo(() => db.getSupplierLedger(supplier.id), [supplier.id]);
  const payments = useMemo(() => db.getSupplierPayments(supplier.id), [supplier.id]);

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-3">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-3xl flex flex-col max-h-[88vh] overflow-hidden text-xs">
        {/* Header */}
        <div className="bg-[#1f4e79] text-white px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <BookOpen className="w-4 h-4 text-amber-300" />
            <div>
              <h3 className="font-bold text-sm">Account Ledger Statement</h3>
              <p className="text-[11px] text-blue-200">
                {supplier.name} ({supplier.code}) · GSTIN: {supplier.gstin || 'Unregistered'}
              </p>
            </div>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Balance Card Strip */}
        <div className="bg-[#fafbfc] border-b border-slate-200 px-4 py-2.5 flex items-center justify-between">
          <div className="flex items-center gap-6">
            <div>
              <span className="text-slate-500">Opening Balance:</span>
              <span className="ml-1 font-mono font-bold">
                ₹{supplier.openingBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </span>
            </div>
            <div>
              <span className="text-slate-500">Net Payable:</span>
              <span className={`ml-1 font-mono text-sm font-bold ${supplier.currentBalance > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
                ₹{supplier.currentBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </span>
            </div>
          </div>

          <button
            onClick={onOpenPayment}
            className="px-3 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-medium flex items-center gap-1 cursor-pointer"
          >
            <DollarSign className="w-3.5 h-3.5" />
            <span>Record Payment</span>
          </button>
        </div>

        {/* Ledger Table */}
        <div className="flex-1 overflow-auto p-3">
          <table className="w-full text-left border-collapse text-xs">
            <thead className="bg-slate-100 text-slate-700 sticky top-0 border-b border-slate-200">
              <tr>
                <th className="p-2 w-24">Date</th>
                <th className="p-2 w-28">Ref #</th>
                <th className="p-2">Transaction Type / Notes</th>
                <th className="p-2 text-right w-24">Debit (Paid)</th>
                <th className="p-2 text-right w-24">Credit (Bill)</th>
                <th className="p-2 text-right w-28">Running Bal (₹)</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 font-mono">
              {ledger.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-400 font-sans">
                    No ledger transactions recorded yet.
                  </td>
                </tr>
              ) : (
                ledger.map((entry) => (
                  <tr key={entry.id} className="hover:bg-slate-50">
                    <td className="p-2 text-slate-600">
                      {new Date(entry.transactionDate).toLocaleDateString()}
                    </td>
                    <td className="p-2 font-bold text-blue-700">{entry.referenceId}</td>
                    <td className="p-2 font-sans">
                      <span className="font-semibold text-slate-800">{entry.transactionType}</span>
                      {entry.notes && <span className="block text-slate-500 text-[11px]">{entry.notes}</span>}
                    </td>
                    <td className="p-2 text-right text-emerald-700 font-bold">
                      {entry.debit > 0 ? `₹${entry.debit.toFixed(2)}` : '—'}
                    </td>
                    <td className="p-2 text-right text-rose-700 font-bold">
                      {entry.credit > 0 ? `₹${entry.credit.toFixed(2)}` : '—'}
                    </td>
                    <td className="p-2 text-right font-bold text-slate-900">
                      ₹{entry.balanceAfter.toFixed(2)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="bg-slate-100 px-4 py-2 border-t border-slate-200 flex justify-end">
          <button onClick={onClose} className="px-4 py-1.5 bg-white border border-slate-300 rounded font-medium">
            Close
          </button>
        </div>
      </div>
    </div>
  );
};

// -----------------------------------------------------------------------------------------
// MODAL: Record Supplier Payment
// -----------------------------------------------------------------------------------------
interface SupplierPaymentModalProps {
  supplier: Supplier;
  onClose: () => void;
  onPaymentSuccess: () => void;
}

const SupplierPaymentModal: React.FC<SupplierPaymentModalProps> = ({ supplier, onClose, onPaymentSuccess }) => {
  const [amount, setAmount] = useState<number>(supplier.currentBalance > 0 ? supplier.currentBalance : 0);
  const [paymentMethod, setPaymentMethod] = useState<'Cash' | 'Bank Transfer' | 'UPI' | 'Cheque'>('Bank Transfer');
  const [referenceNumber, setReferenceNumber] = useState('');
  const [notes, setNotes] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (amount <= 0) {
      setError('Payment amount must be greater than zero.');
      return;
    }

    const res = db.recordSupplierPayment({
      supplierId: supplier.id,
      amount,
      paymentMethod,
      referenceNumber,
      notes,
      paymentDate: new Date().toISOString(),
    });

    if (res.success) {
      onPaymentSuccess();
    } else {
      setError(res.errors.join('; '));
    }
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/60 flex items-center justify-center p-3">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-md flex flex-col overflow-hidden text-xs">
        <div className="bg-[#1f4e79] text-white px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <DollarSign className="w-4 h-4 text-emerald-300" />
            <h3 className="font-bold text-sm">Make Vendor Payment</h3>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-4 space-y-3">
          {error && (
            <div className="p-2 bg-rose-50 border border-rose-200 rounded text-rose-700">
              {error}
            </div>
          )}

          <div className="bg-slate-50 p-2.5 rounded border border-slate-200">
            <div className="text-slate-500 font-medium">Paying to:</div>
            <div className="font-bold text-slate-900 text-sm">{supplier.name}</div>
            <div className="flex justify-between items-center pt-1 text-slate-600">
              <span>Current Outstanding:</span>
              <span className="font-mono font-bold text-rose-700">
                ₹{supplier.currentBalance.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </span>
            </div>
          </div>

          <div>
            <label className="block text-slate-700 font-bold mb-1">
              Payment Amount (₹) <span className="text-rose-500">*</span>
            </label>
            <input
              type="number"
              step="0.01"
              min="0.01"
              value={amount}
              onChange={(e) => setAmount(parseFloat(e.target.value) || 0)}
              className="w-full px-3 py-2 border border-slate-300 rounded font-mono text-base font-bold text-slate-900 focus:outline-hidden focus:border-blue-500"
              required
            />
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="block text-slate-700 font-medium mb-1">Payment Method</label>
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value as any)}
                className="w-full px-2 py-1.5 border border-slate-300 rounded bg-white"
              >
                <option value="Bank Transfer">Bank Transfer / NEFT</option>
                <option value="Cash">Cash</option>
                <option value="UPI">UPI</option>
                <option value="Cheque">Cheque</option>
              </select>
            </div>

            <div>
              <label className="block text-slate-700 font-medium mb-1">Ref / UTR / Cheque #</label>
              <input
                type="text"
                value={referenceNumber}
                onChange={(e) => setReferenceNumber(e.target.value)}
                placeholder="UTR-99120..."
                className="w-full px-2 py-1.5 border border-slate-300 rounded font-mono"
              />
            </div>
          </div>

          <div>
            <label className="block text-slate-700 font-medium mb-1">Remarks / Note</label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="e.g. Part payment against invoice..."
              className="w-full px-2 py-1.5 border border-slate-300 rounded"
            />
          </div>

          <div className="pt-2 flex justify-end gap-2 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-1.5 border border-slate-300 rounded hover:bg-slate-100 font-medium cursor-pointer"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded font-bold shadow-xs cursor-pointer"
            >
              Post Payment
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
