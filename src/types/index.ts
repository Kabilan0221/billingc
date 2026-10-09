export interface Product {
  id: number;
  productCode: string;
  barcode: string;
  nameEn: string;
  nameTa: string;
  categoryId: number | null;
  categoryName?: string;
  subcategoryId: number | null;
  subcategoryName?: string;
  brandId: number | null;
  brandName?: string;
  unit: string;
  hsnCode?: string;
  purchaseRate: number;
  saleRate: number;
  wholesaleRate: number;
  mrp: number;
  taxRate: number;
  openingStock: number;
  currentStock: number;
  reorderLevel: number;
  maxStockLevel: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface Category {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  productCount?: number;
}

export interface Subcategory {
  id: number;
  categoryId: number;
  code: string;
  name: string;
  isActive: boolean;
  productCount?: number;
}

export interface Brand {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  productCount?: number;
}

export interface AuditLog {
  id: number;
  entity: string;
  entityId: string;
  action: 'INSERT' | 'UPDATE' | 'DEACTIVATE' | 'ACTIVATE' | 'IMPORT' | 'RESTORE';
  details: string;
  timestamp: string;
}

export interface ProductFilter {
  searchTerm?: string;
  categoryId?: number;
  subcategoryId?: number;
  brandId?: number;
  isActive?: boolean;
  isLowStockOnly?: boolean;
  sortBy?: 'nameEn' | 'barcode' | 'productCode' | 'saleRate' | 'currentStock';
  sortDescending?: boolean;
  pageNumber: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  executionTimeMs: number;
}

export interface Supplier {
  id: number;
  code: string;
  name: string;
  contactPerson?: string;
  mobile?: string;
  altMobile?: string;
  whatsapp?: string;
  email?: string;
  gstin?: string;
  pan?: string;
  address?: string;
  city?: string;
  state?: string;
  stateCode?: string;
  pinCode?: string;
  openingBalance: number;
  currentBalance: number;
  paymentTerms?: string;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface PurchaseItem {
  id: number;
  purchaseId: number;
  productId: number;
  productCode: string;
  barcode: string;
  productName: string;
  categoryName?: string;
  quantity: number;
  freeQuantity: number;
  purchaseRate: number;
  discountPercent: number;
  discountAmount: number;
  taxRate: number;
  taxableAmount: number;
  taxAmount: number;
  lineTotal: number;
  batchNumber?: string;
  expiryDate?: string;
}

export interface PurchaseHeader {
  id: number;
  purchaseNumber: string;
  supplierId: number;
  supplierName: string;
  supplierInvoiceNumber?: string;
  supplierInvoiceDate?: string;
  goodsReceivedDate: string;
  purchaseType: 'Cash' | 'Credit';
  paymentDueDate?: string;
  referenceNumber?: string;
  notes?: string;
  status: 'Draft' | 'Confirmed' | 'Cancelled';
  totalQty: number;
  totalFreeQty: number;
  grossAmount: number;
  itemDiscount: number;
  additionalDiscount: number;
  taxableAmount: number;
  taxAmount: number;
  additionalCharges: number;
  roundOff: number;
  grandTotal: number;
  amountPaid: number;
  balanceDue: number;
  createdBy?: string;
  confirmedAt?: string;
  cancelledAt?: string;
  createdAt: string;
  updatedAt: string;
  items: PurchaseItem[];
}

export interface SupplierPayment {
  id: number;
  paymentNumber: string;
  supplierId: number;
  supplierName: string;
  purchaseId?: number;
  purchaseNumber?: string;
  paymentDate: string;
  paymentMethod: 'Cash' | 'Bank Transfer' | 'UPI' | 'Cheque' | 'Other';
  amount: number;
  referenceNumber?: string;
  notes?: string;
  createdAt: string;
}

export interface StockMovement {
  id: number;
  productId: number;
  productCode: string;
  productName: string;
  referenceType: 'PURCHASE_CONFIRM' | 'PURCHASE_CANCEL' | 'ADJUSTMENT' | 'OPENING_STOCK';
  referenceId: string;
  quantity: number;
  quantityBefore: number;
  quantityAfter: number;
  notes?: string;
  createdAt: string;
}

export interface SupplierLedgerEntry {
  id: number;
  supplierId: number;
  transactionType: 'OPENING_BALANCE' | 'PURCHASE' | 'PAYMENT' | 'PURCHASE_CANCEL';
  referenceId: string;
  debit: number;
  credit: number;
  balanceAfter: number;
  notes?: string;
  transactionDate: string;
}

export interface StockAdjustmentItem {
  id: number;
  adjustmentId: number;
  productId: number;
  productCode: string;
  barcode: string;
  productName: string;
  categoryName?: string;
  currentStock: number;
  physicalCount: number;
  differenceQuantity: number;
  adjustmentType: 'Increase Stock' | 'Decrease Stock' | 'Damaged Goods' | 'Expired Goods' | 'Physical Count Correction' | 'Other';
  unitCost: number;
  reason?: string;
  notes?: string;
}

export interface StockAdjustment {
  id: number;
  adjustmentNumber: string;
  adjustmentDate: string;
  reason: string;
  notes?: string;
  status: 'Draft' | 'Confirmed' | 'Cancelled';
  totalItems: number;
  totalDifferenceQuantity: number;
  totalCostImpact: number;
  createdBy?: string;
  confirmedAt?: string;
  cancelledAt?: string;
  createdAt: string;
  updatedAt: string;
  items: StockAdjustmentItem[];
}

export interface PhysicalStockVerificationItem {
  id: number;
  verificationId: number;
  productId: number;
  productCode: string;
  barcode: string;
  productName: string;
  categoryName?: string;
  systemStockAtStart: number;
  systemStockAtConfirm: number;
  physicalCount: number;
  differenceQuantity: number;
  discrepancyResolved: boolean;
  notes?: string;
}

export interface PhysicalStockVerification {
  id: number;
  verificationNumber: string;
  verificationDate: string;
  categoryId?: number;
  categoryName?: string;
  status: 'Draft' | 'Confirmed' | 'Cancelled';
  totalCountedProducts: number;
  totalDiscrepancies: number;
  netDifferenceQuantity: number;
  notes?: string;
  createdBy?: string;
  confirmedAt?: string;
  createdAt: string;
  updatedAt: string;
  items: PhysicalStockVerificationItem[];
}

export interface CurrentStockSummary {
  productId: number;
  productCode: string;
  barcode: string;
  productName: string;
  categoryName: string;
  brandName: string;
  unit: string;
  openingStock: number;
  purchasedQuantity: number;
  freeQuantityReceived: number;
  soldQuantity: number;
  returnedQuantity: number;
  adjustmentQuantity: number;
  currentStock: number;
  reorderLevel: number;
  maxStockLevel: number;
  purchaseRate: number;
  saleRate: number;
  stockValuePurchase: number;
  stockValueSale: number;
  stockStatus: 'In Stock' | 'Low Stock' | 'Out of Stock' | 'Excess Stock';
}

export interface StockDashboardMetrics {
  totalActiveProducts: number;
  totalStockQuantity: number;
  outOfStockCount: number;
  lowStockCount: number;
  excessStockCount: number;
  adjustedProductsCount: number;
  stockValueAtCost: number;
  stockValueAtSale: number;
  potentialGrossMargin: number;
  recentMovements: StockMovement[];
}

