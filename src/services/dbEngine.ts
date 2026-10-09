import {
  Product,
  Category,
  Subcategory,
  Brand,
  AuditLog,
  ProductFilter,
  PagedResult,
  Supplier,
  PurchaseHeader,
  PurchaseItem,
  SupplierPayment,
  StockMovement,
  SupplierLedgerEntry,
  StockAdjustment,
  StockAdjustmentItem,
  PhysicalStockVerification,
  PhysicalStockVerificationItem,
  CurrentStockSummary,
  StockDashboardMetrics,
} from '../types';
import {
  INITIAL_CATEGORIES,
  INITIAL_SUBCATEGORIES,
  INITIAL_BRANDS,
  INITIAL_PRODUCTS,
  INITIAL_SUPPLIERS,
} from '../data/initialData';

export interface SupplierFilter {
  searchTerm?: string;
  isActive?: boolean;
  sortBy?: 'name' | 'code' | 'city' | 'currentBalance';
  sortDescending?: boolean;
  pageNumber: number;
  pageSize: number;
}

export interface PurchaseFilter {
  searchTerm?: string;
  supplierId?: number;
  status?: string; // 'All' | 'Draft' | 'Confirmed' | 'Cancelled'
  fromDate?: string;
  toDate?: string;
  pageNumber: number;
  pageSize: number;
}

class SQLiteIndexedEngine {
  private products: Product[] = [];
  private categories: Category[] = [];
  private subcategories: Subcategory[] = [];
  private brands: Brand[] = [];
  private auditLogs: AuditLog[] = [];

  // Phase 2 Entities
  private suppliers: Supplier[] = [];
  private purchases: PurchaseHeader[] = [];
  private supplierPayments: SupplierPayment[] = [];
  private stockMovements: StockMovement[] = [];
  private supplierLedger: SupplierLedgerEntry[] = [];

  // Phase 3 Entities
  private stockAdjustments: StockAdjustment[] = [];
  private physicalVerifications: PhysicalStockVerification[] = [];

  // B-Tree / Hash Indexes
  private barcodeIndex = new Map<string, Product>();
  private codeIndex = new Map<string, Product>();
  private idIndex = new Map<number, Product>();
  private categoryIndex = new Map<number, Set<Product>>();
  private brandIndex = new Map<number, Set<Product>>();

  // Supplier & Purchase Indexes
  private supplierIdIndex = new Map<number, Supplier>();
  private supplierCodeIndex = new Map<string, Supplier>();
  private purchaseIdIndex = new Map<number, PurchaseHeader>();
  private purchaseNumberIndex = new Map<string, PurchaseHeader>();

  private is50kLoaded = false;
  private currentNextId = 11;
  private nextSupplierId = 6;
  private nextPurchaseId = 3;
  private nextPaymentId = 2;
  private nextMovementId = 3;
  private nextLedgerId = 3;

  constructor() {
    this.resetToDefaults();
  }

  public resetToDefaults() {
    this.categories = JSON.parse(JSON.stringify(INITIAL_CATEGORIES));
    this.subcategories = JSON.parse(JSON.stringify(INITIAL_SUBCATEGORIES));
    this.brands = JSON.parse(JSON.stringify(INITIAL_BRANDS));
    this.products = JSON.parse(JSON.stringify(INITIAL_PRODUCTS));
    this.suppliers = JSON.parse(JSON.stringify(INITIAL_SUPPLIERS));

    // Initial Sample Confirmed Purchase Entry (PUR-2026-0001)
    const initialPurchase1: PurchaseHeader = {
      id: 1,
      purchaseNumber: 'PUR-2026-0001',
      supplierId: 1,
      supplierName: 'Sri Meenakshi Trading Co',
      supplierInvoiceNumber: 'INV-8821',
      supplierInvoiceDate: '2026-01-12T10:30:00Z',
      goodsReceivedDate: '2026-01-13T14:00:00Z',
      purchaseType: 'Credit',
      paymentDueDate: '2026-02-12T00:00:00Z',
      referenceNumber: 'LR-44210',
      notes: 'Initial opening stock replenishment batch for staples',
      status: 'Confirmed',
      totalQty: 25,
      totalFreeQty: 2,
      grossAmount: 7000,
      itemDiscount: 200,
      additionalDiscount: 0,
      taxableAmount: 6800,
      taxAmount: 0,
      additionalCharges: 150, // Freight
      roundOff: 0,
      grandTotal: 6950,
      amountPaid: 2450,
      balanceDue: 4500,
      createdBy: 'Admin',
      confirmedAt: '2026-01-13T14:15:00Z',
      createdAt: '2026-01-13T10:00:00Z',
      updatedAt: '2026-01-13T14:15:00Z',
      items: [
        {
          id: 1,
          purchaseId: 1,
          productId: 1,
          productCode: 'PRD0001',
          barcode: '8901030012345',
          productName: 'Ponni Boiled Rice 5kg',
          categoryName: 'Groceries & Staples',
          quantity: 20,
          freeQuantity: 2,
          purchaseRate: 280,
          discountPercent: 2,
          discountAmount: 112,
          taxRate: 0,
          taxableAmount: 5488,
          taxAmount: 0,
          lineTotal: 5488,
          batchNumber: 'PBR-2026-01',
          expiryDate: '2026-12-31',
        },
        {
          id: 2,
          purchaseId: 1,
          productId: 3,
          productCode: 'PRD0003',
          barcode: '8901030012347',
          productName: 'Tata Salt Iodized 1kg',
          categoryName: 'Groceries & Staples',
          quantity: 5,
          freeQuantity: 0,
          purchaseRate: 22,
          discountPercent: 0,
          discountAmount: 0,
          taxRate: 0,
          taxableAmount: 110,
          taxAmount: 0,
          lineTotal: 110,
          batchNumber: 'TS-JAN26',
        },
      ],
    };

    // Initial Sample Draft Purchase Entry (PUR-2026-0002)
    const initialPurchase2: PurchaseHeader = {
      id: 2,
      purchaseNumber: 'PUR-2026-0002',
      supplierId: 3,
      supplierName: 'Hindustan Consumer Supplies',
      supplierInvoiceNumber: 'HCS/TI/4491',
      supplierInvoiceDate: '2026-01-18T11:00:00Z',
      goodsReceivedDate: '2026-01-19T09:30:00Z',
      purchaseType: 'Credit',
      paymentDueDate: '2026-02-05T00:00:00Z',
      referenceNumber: 'EWAY-9988112',
      notes: 'Monthly personal care replenishment (Draft verification pending)',
      status: 'Draft',
      totalQty: 50,
      totalFreeQty: 5,
      grossAmount: 4250,
      itemDiscount: 150,
      additionalDiscount: 100,
      taxableAmount: 4000,
      taxAmount: 720, // 18% GST
      additionalCharges: 100,
      roundOff: 0,
      grandTotal: 4820,
      amountPaid: 0,
      balanceDue: 4820,
      createdBy: 'StoreManager',
      createdAt: '2026-01-19T09:30:00Z',
      updatedAt: '2026-01-19T09:30:00Z',
      items: [
        {
          id: 3,
          purchaseId: 2,
          productId: 7,
          productCode: 'PRD0007',
          barcode: '8901030012351',
          productName: 'Lifebuoy Total Soap 125g',
          categoryName: 'Personal Care & Hygiene',
          quantity: 50,
          freeQuantity: 5,
          purchaseRate: 30,
          discountPercent: 5,
          discountAmount: 75,
          taxRate: 18,
          taxableAmount: 1425,
          taxAmount: 256.5,
          lineTotal: 1681.5,
          batchNumber: 'LB-2026A',
          expiryDate: '2028-06-30',
        },
      ],
    };

    this.purchases = [initialPurchase1, initialPurchase2];

    // Seed stock movement ledger from confirmed purchase
    this.stockMovements = [
      {
        id: 1,
        productId: 1,
        productCode: 'PRD0001',
        productName: 'Ponni Boiled Rice 5kg',
        referenceType: 'PURCHASE_CONFIRM',
        referenceId: 'PUR-2026-0001',
        quantity: 22,
        quantityBefore: 26,
        quantityAfter: 48,
        notes: 'Goods received from Sri Meenakshi Trading Co (INV-8821)',
        createdAt: '2026-01-13T14:15:00Z',
      },
      {
        id: 2,
        productId: 3,
        productCode: 'PRD0003',
        productName: 'Tata Salt Iodized 1kg',
        referenceType: 'PURCHASE_CONFIRM',
        referenceId: 'PUR-2026-0001',
        quantity: 5,
        quantityBefore: 65,
        quantityAfter: 70,
        notes: 'Goods received from Sri Meenakshi Trading Co (INV-8821)',
        createdAt: '2026-01-13T14:15:00Z',
      },
    ];

    // Seed Supplier Ledger & Payments
    this.supplierPayments = [
      {
        id: 1,
        paymentNumber: 'PAY-2026-0001',
        supplierId: 1,
        supplierName: 'Sri Meenakshi Trading Co',
        purchaseId: 1,
        purchaseNumber: 'PUR-2026-0001',
        paymentDate: '2026-01-13T14:30:00Z',
        paymentMethod: 'Bank Transfer',
        amount: 2450,
        referenceNumber: 'NEFT-AXIS-9921',
        notes: 'Advance part-payment against bill PUR-2026-0001',
        createdAt: '2026-01-13T14:30:00Z',
      },
    ];

    this.supplierLedger = [
      {
        id: 1,
        supplierId: 1,
        transactionType: 'PURCHASE',
        referenceId: 'PUR-2026-0001',
        debit: 0,
        credit: 6950,
        balanceAfter: 6950,
        notes: 'Inbound purchase goods bill PUR-2026-0001',
        transactionDate: '2026-01-13T14:15:00Z',
      },
      {
        id: 2,
        supplierId: 1,
        transactionType: 'PAYMENT',
        referenceId: 'PAY-2026-0001',
        debit: 2450,
        credit: 0,
        balanceAfter: 4500,
        notes: 'Bank Transfer payment ref: NEFT-AXIS-9921',
        transactionDate: '2026-01-13T14:30:00Z',
      },
    ];

    this.auditLogs = [
      {
        id: 1,
        entity: 'System',
        entityId: '0',
        action: 'INSERT',
        details: 'Initial SQLite database initialized with version 4 migrations (Suppliers & Inbound Goods active)',
        timestamp: new Date().toISOString(),
      },
    ];

    this.is50kLoaded = false;
    this.currentNextId = 11;
    this.nextSupplierId = 6;
    this.nextPurchaseId = 3;
    this.nextPaymentId = 2;
    this.nextMovementId = 3;
    this.nextLedgerId = 3;

    this.rebuildIndexes();
  }

  public rebuildIndexes() {
    this.barcodeIndex.clear();
    this.codeIndex.clear();
    this.idIndex.clear();
    this.categoryIndex.clear();
    this.brandIndex.clear();

    for (const p of this.products) {
      this.idIndex.set(p.id, p);
      if (p.barcode) this.barcodeIndex.set(p.barcode.trim().toLowerCase(), p);
      if (p.productCode) this.codeIndex.set(p.productCode.trim().toLowerCase(), p);

      if (p.categoryId) {
        if (!this.categoryIndex.has(p.categoryId)) this.categoryIndex.set(p.categoryId, new Set());
        this.categoryIndex.get(p.categoryId)!.add(p);
      }
      if (p.brandId) {
        if (!this.brandIndex.has(p.brandId)) this.brandIndex.set(p.brandId, new Set());
        this.brandIndex.get(p.brandId)!.add(p);
      }
    }

    // Suppliers Index
    this.supplierIdIndex.clear();
    this.supplierCodeIndex.clear();
    for (const s of this.suppliers) {
      this.supplierIdIndex.set(s.id, s);
      if (s.code) this.supplierCodeIndex.set(s.code.trim().toUpperCase(), s);
    }

    // Purchases Index
    this.purchaseIdIndex.clear();
    this.purchaseNumberIndex.clear();
    for (const pur of this.purchases) {
      this.purchaseIdIndex.set(pur.id, pur);
      if (pur.purchaseNumber) this.purchaseNumberIndex.set(pur.purchaseNumber.trim().toUpperCase(), pur);
    }
  }

  // -------------------------------------------------------------
  // PRODUCTS METHODS
  // -------------------------------------------------------------
  public searchProducts(filter: ProductFilter): PagedResult<Product> {
    const t0 = performance.now();

    let candidateList: Product[] = [];
    const term = filter.searchTerm?.trim().toLowerCase();

    // 1. Fast index lookup if exact barcode or code
    if (term && this.barcodeIndex.has(term)) {
      candidateList = [this.barcodeIndex.get(term)!];
    } else if (term && this.codeIndex.has(term)) {
      candidateList = [this.codeIndex.get(term)!];
    } else {
      let pool: Product[] = this.products;

      if (filter.categoryId && filter.categoryId > 0 && this.categoryIndex.has(filter.categoryId)) {
        pool = Array.from(this.categoryIndex.get(filter.categoryId)!);
      }

      candidateList = [];
      for (let i = 0; i < pool.length; i++) {
        const p = pool[i];

        if (filter.isActive !== undefined && p.isActive !== filter.isActive) continue;
        if (filter.subcategoryId && filter.subcategoryId > 0 && p.subcategoryId !== filter.subcategoryId) continue;
        if (filter.brandId && filter.brandId > 0 && p.brandId !== filter.brandId) continue;
        if (filter.isLowStockOnly && p.currentStock > p.reorderLevel) continue;

        if (term) {
          const matchEn = p.nameEn.toLowerCase().includes(term);
          const matchTa = p.nameTa ? p.nameTa.toLowerCase().includes(term) : false;
          const matchBar = p.barcode.toLowerCase().includes(term);
          const matchCode = p.productCode.toLowerCase().includes(term);

          if (!matchEn && !matchTa && !matchBar && !matchCode) continue;
        }

        candidateList.push(p);
      }
    }

    // Sort
    const sortBy = filter.sortBy || 'nameEn';
    const isDesc = !!filter.sortDescending;
    candidateList.sort((a, b) => {
      let valA: any = a[sortBy];
      let valB: any = b[sortBy];
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();
      if (valA < valB) return isDesc ? 1 : -1;
      if (valA > valB) return isDesc ? -1 : 1;
      return 0;
    });

    const totalCount = candidateList.length;
    const pageNumber = Math.max(1, filter.pageNumber);
    const pageSize = Math.max(1, filter.pageSize);
    const totalPages = Math.ceil(totalCount / pageSize);

    const startIndex = (pageNumber - 1) * pageSize;
    const items = candidateList.slice(startIndex, startIndex + pageSize);

    const t1 = performance.now();
    const executionTimeMs = parseFloat((t1 - t0).toFixed(2));

    return {
      items,
      totalCount,
      pageNumber,
      pageSize,
      totalPages,
      executionTimeMs,
    };
  }

  public getProductById(id: number): Product | undefined {
    return this.idIndex.get(id);
  }

  public getProductByBarcode(barcode: string): Product | undefined {
    return this.barcodeIndex.get(barcode.trim().toLowerCase());
  }

  public saveProduct(product: Partial<Product>): { success: boolean; errors: string[]; product?: Product } {
    const errors: string[] = [];

    if (!product.nameEn || product.nameEn.trim().length < 2) {
      errors.push('Product Name (English) is required (min 2 chars).');
    }
    if (!product.productCode || product.productCode.trim().length < 2) {
      errors.push('Product Code is required.');
    }
    if (!product.barcode || product.barcode.trim().length < 3) {
      errors.push('Barcode is required.');
    }
    if ((product.purchaseRate ?? 0) < 0) errors.push('Purchase Rate cannot be negative.');
    if ((product.saleRate ?? 0) < 0) errors.push('Sale Rate cannot be negative.');
    if ((product.mrp ?? 0) < 0) errors.push('MRP cannot be negative.');
    if ((product.mrp ?? 0) > 0 && (product.saleRate ?? 0) > (product.mrp ?? 0)) {
      errors.push(`Sale Rate (₹${product.saleRate}) cannot exceed MRP (₹${product.mrp}).`);
    }

    const cleanBarcode = product.barcode?.trim().toLowerCase();
    const cleanCode = product.productCode?.trim().toLowerCase();

    // Duplicate checks
    if (cleanBarcode) {
      const existing = this.barcodeIndex.get(cleanBarcode);
      if (existing && existing.id !== product.id) {
        errors.push(`Barcode '${product.barcode}' is already assigned to '${existing.nameEn}'.`);
      }
    }

    if (cleanCode) {
      const existing = this.codeIndex.get(cleanCode);
      if (existing && existing.id !== product.id) {
        errors.push(`Product Code '${product.productCode}' already exists.`);
      }
    }

    if (errors.length > 0) return { success: false, errors };

    const cat = this.categories.find((c) => c.id === product.categoryId);
    const subcat = this.subcategories.find((s) => s.id === product.subcategoryId);
    const brand = this.brands.find((b) => b.id === product.brandId);

    if (product.id) {
      const index = this.products.findIndex((p) => p.id === product.id);
      if (index === -1) return { success: false, errors: ['Product not found.'] };

      const updated: Product = {
        ...this.products[index],
        ...product,
        categoryName: cat?.name || this.products[index].categoryName,
        subcategoryName: subcat?.name || this.products[index].subcategoryName,
        brandName: brand?.name || this.products[index].brandName,
        updatedAt: new Date().toISOString(),
      } as Product;

      this.products[index] = updated;
      this.rebuildIndexes();

      this.logAudit('Product', updated.id.toString(), 'UPDATE', `Updated product '${updated.nameEn}'`);
      return { success: true, errors: [], product: updated };
    } else {
      const newProd: Product = {
        id: this.currentNextId++,
        productCode: product.productCode!.trim(),
        barcode: product.barcode!.trim(),
        nameEn: product.nameEn!.trim(),
        nameTa: product.nameTa?.trim() || '',
        categoryId: product.categoryId || null,
        categoryName: cat?.name || '',
        subcategoryId: product.subcategoryId || null,
        subcategoryName: subcat?.name || '',
        brandId: product.brandId || null,
        brandName: brand?.name || '',
        unit: product.unit || 'PCS',
        hsnCode: product.hsnCode || '',
        purchaseRate: Number(product.purchaseRate) || 0,
        saleRate: Number(product.saleRate) || 0,
        wholesaleRate: Number(product.wholesaleRate) || 0,
        mrp: Number(product.mrp) || 0,
        taxRate: Number(product.taxRate) || 0,
        openingStock: Number(product.openingStock) || 0,
        currentStock: Number(product.currentStock ?? product.openingStock) || 0,
        reorderLevel: Number(product.reorderLevel) || 5,
        maxStockLevel: Number(product.maxStockLevel) || 1000,
        isActive: product.isActive !== undefined ? product.isActive : true,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      this.products.unshift(newProd);
      this.rebuildIndexes();

      this.logAudit('Product', newProd.id.toString(), 'INSERT', `Created product '${newProd.nameEn}' (Code: ${newProd.productCode})`);
      return { success: true, errors: [], product: newProd };
    }
  }

  public deactivateProduct(id: number): boolean {
    const p = this.idIndex.get(id);
    if (!p) return false;
    p.isActive = false;
    p.updatedAt = new Date().toISOString();
    this.logAudit('Product', id.toString(), 'DEACTIVATE', `Deactivated product '${p.nameEn}'`);
    return true;
  }

  public activateProduct(id: number): boolean {
    const p = this.idIndex.get(id);
    if (!p) return false;
    p.isActive = true;
    p.updatedAt = new Date().toISOString();
    this.logAudit('Product', id.toString(), 'ACTIVATE', `Re-activated product '${p.nameEn}'`);
    return true;
  }

  public bulkImportProducts(newItems: Partial<Product>[], updateDuplicates: boolean = false): { imported: number; failed: number; errors: any[] } {
    let imported = 0;
    let failed = 0;
    const errors: any[] = [];

    for (let i = 0; i < newItems.length; i++) {
      const row = newItems[i];
      const res = this.saveProduct(row);
      if (res.success) {
        imported++;
      } else {
        failed++;
        errors.push({ row: i + 1, code: row.productCode, barcode: row.barcode, error: res.errors.join('; ') });
      }
    }

    this.logAudit('Excel', 'BulkImport', 'IMPORT', `Imported ${imported} products with ${failed} failures.`);
    return { imported, failed, errors };
  }

  public load50kBenchmarkDataset(): { durationMs: number; count: number } {
    const t0 = performance.now();

    const sampleBatch: Product[] = [];
    const prefixesEn = ['Tata', 'Aachi', 'Britannia', 'Parle', 'HUL', 'Nestle', 'Amul', 'ITC', 'Dabur', 'Everest'];
    const prefixesTa = ['டாடா', 'ஆச்சி', 'பிரிட்டானியா', 'பார்லே', 'ஹெச்ச்யூஎல்', 'நெஸ்லே', 'அமுல்', 'ஐடிசி', 'டாபர்', 'எவரெஸ்ட்'];
    const itemsEn = ['Ponni Rice', 'Chilli Powder', 'Tea Powder', 'Salt', 'Ghee', 'Biscuits', 'Soap', 'Toothpaste', 'Sunflower Oil', 'Toor Dal'];
    const itemsTa = ['பொன்னி அரிசி', 'மிளகாய் தூள்', 'தேயிலை தூள்', 'உப்பு', 'நெய்', 'பிஸ்கட்', 'சோப்பு', 'பல்பசை', 'சூரியகாந்தி எண்ணெய்', 'துவரம் பருப்பு'];
    const units = ['PCS', 'KG', 'PACK', 'BAG', 'LTR'];

    for (let i = 1; i <= 50000; i++) {
      const pIdx = i % prefixesEn.length;
      const itIdx = (i * 3) % itemsEn.length;
      const catId = (i % 6) + 1;
      const brandId = (i % 8) + 1;
      const unit = units[i % units.length];

      const pur = 20 + (i % 450);
      const sale = Math.round(pur * 1.25);
      const mrp = Math.round(sale * 1.08);

      sampleBatch.push({
        id: this.currentNextId++,
        productCode: `PRD${i.toString().padStart(6, '0')}`,
        barcode: `890${i.toString().padStart(10, '0')}`,
        nameEn: `${prefixesEn[pIdx]} ${itemsEn[itIdx]} #${i}`,
        nameTa: `${prefixesTa[pIdx]} ${itemsTa[itIdx]} #${i}`,
        categoryId: catId,
        categoryName: this.categories.find((c) => c.id === catId)?.name || 'Groceries',
        subcategoryId: (i % 8) + 1,
        subcategoryName: this.subcategories.find((s) => s.id === ((i % 8) + 1))?.name || 'Staples',
        brandId: brandId,
        brandName: this.brands.find((b) => b.id === brandId)?.name || 'General',
        unit,
        purchaseRate: pur,
        saleRate: sale,
        wholesaleRate: Math.round(sale * 0.94),
        mrp,
        taxRate: (i % 4 === 0) ? 0 : (i % 4 === 1) ? 5 : 18,
        openingStock: 50 + (i % 200),
        currentStock: (i % 15 === 0) ? 3 : 50 + (i % 190),
        reorderLevel: 10,
        maxStockLevel: 500,
        isActive: true,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      });
    }

    this.products = sampleBatch;
    this.rebuildIndexes();
    this.is50kLoaded = true;

    const t1 = performance.now();
    const durationMs = parseFloat((t1 - t0).toFixed(1));

    this.logAudit('Benchmark', '50k', 'INSERT', `Indexed 50,000 products in ${durationMs}ms`);
    return { durationMs, count: 50000 };
  }

  public getCategories() { return this.categories; }
  public getSubcategories(catId?: number) {
    if (!catId) return this.subcategories;
    return this.subcategories.filter((s) => s.categoryId === catId);
  }
  public getBrands() { return this.brands; }
  public getAuditLogs() { return this.auditLogs.slice(0, 100); }
  public getAllProducts() { return this.products; }

  public addCategory(name: string, code?: string): Category {
    const newCat: Category = {
      id: Date.now(),
      name,
      code: code || `CAT-${Math.floor(1000 + Math.random() * 9000)}`,
      isActive: true,
      productCount: 0,
    };
    this.categories.push(newCat);
    this.logAudit('Category', newCat.id.toString(), 'INSERT', `Added category '${name}'`);
    return newCat;
  }

  public addSubcategory(categoryId: number, name: string, code?: string): Subcategory {
    const newSub: Subcategory = {
      id: Date.now(),
      categoryId,
      name,
      code: code || `SUB-${Math.floor(1000 + Math.random() * 9000)}`,
      isActive: true,
    };
    this.subcategories.push(newSub);
    this.logAudit('Subcategory', newSub.id.toString(), 'INSERT', `Added subcategory '${name}'`);
    return newSub;
  }

  public addBrand(name: string, code?: string): Brand {
    const newBrand: Brand = {
      id: Date.now(),
      name,
      code: code || `BR-${Math.floor(1000 + Math.random() * 9000)}`,
      isActive: true,
    };
    this.brands.push(newBrand);
    this.logAudit('Brand', newBrand.id.toString(), 'INSERT', `Added brand '${name}'`);
    return newBrand;
  }

  // -------------------------------------------------------------
  // MODULE 1: SUPPLIER MANAGEMENT METHODS
  // -------------------------------------------------------------
  public getAllSuppliers(): Supplier[] {
    return this.suppliers;
  }

  public getAllActiveSuppliers(): Supplier[] {
    return this.suppliers.filter((s) => s.isActive);
  }

  public getSupplierById(id: number): Supplier | undefined {
    return this.supplierIdIndex.get(id);
  }

  public getSupplierByCode(code: string): Supplier | undefined {
    return this.supplierCodeIndex.get(code.trim().toUpperCase());
  }

  public generateNextSupplierCode(): string {
    const prefix = 'SUP';
    let maxNum = 0;
    for (const s of this.suppliers) {
      if (s.code && s.code.startsWith(prefix)) {
        const numPart = parseInt(s.code.substring(prefix.length), 10);
        if (!isNaN(numPart) && numPart > maxNum) maxNum = numPart;
      }
    }
    return `${prefix}${(maxNum + 1).toString().padStart(3, '0')}`;
  }

  public searchSuppliers(filter: SupplierFilter): PagedResult<Supplier> {
    const t0 = performance.now();
    const term = filter.searchTerm?.trim().toLowerCase();

    let list = this.suppliers.filter((s) => {
      if (filter.isActive !== undefined && s.isActive !== filter.isActive) return false;

      if (term) {
        const matchName = s.name.toLowerCase().includes(term);
        const matchCode = s.code.toLowerCase().includes(term);
        const matchMobile = s.mobile ? s.mobile.toLowerCase().includes(term) : false;
        const matchGstin = s.gstin ? s.gstin.toLowerCase().includes(term) : false;
        const matchCity = s.city ? s.city.toLowerCase().includes(term) : false;
        const matchPerson = s.contactPerson ? s.contactPerson.toLowerCase().includes(term) : false;

        if (!matchName && !matchCode && !matchMobile && !matchGstin && !matchCity && !matchPerson) {
          return false;
        }
      }
      return true;
    });

    // Sorting
    const sortBy = filter.sortBy || 'name';
    const isDesc = !!filter.sortDescending;
    list.sort((a, b) => {
      let valA: any = a[sortBy];
      let valB: any = b[sortBy];
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();
      if (valA < valB) return isDesc ? 1 : -1;
      if (valA > valB) return isDesc ? -1 : 1;
      return 0;
    });

    const totalCount = list.length;
    const pageNumber = Math.max(1, filter.pageNumber);
    const pageSize = Math.max(1, filter.pageSize);
    const totalPages = Math.ceil(totalCount / pageSize);

    const startIndex = (pageNumber - 1) * pageSize;
    const items = list.slice(startIndex, startIndex + pageSize);

    const t1 = performance.now();
    const executionTimeMs = parseFloat((t1 - t0).toFixed(2));

    return {
      items,
      totalCount,
      pageNumber,
      pageSize,
      totalPages,
      executionTimeMs,
    };
  }

  public saveSupplier(supplier: Partial<Supplier>): { success: boolean; errors: string[]; supplier?: Supplier } {
    const errors: string[] = [];

    // Required Field Validation
    if (!supplier.name || supplier.name.trim().length < 2) {
      errors.push('Supplier Name is required (minimum 2 characters).');
    }

    // Code Generation & Duplicate Validation
    let code = supplier.code?.trim().toUpperCase();
    if (!code) {
      code = this.generateNextSupplierCode();
    }

    const existingByCode = this.supplierCodeIndex.get(code);
    if (existingByCode && existingByCode.id !== supplier.id) {
      errors.push(`Supplier Code '${code}' already exists. Please provide a unique code.`);
    }

    // Mobile Number Format Validation (if provided, must be 10 digits)
    if (supplier.mobile && supplier.mobile.trim().length > 0) {
      const cleanMobile = supplier.mobile.replace(/[^0-9]/g, '');
      if (cleanMobile.length !== 10) {
        errors.push(`Mobile number must be a valid 10-digit phone number. Received: '${supplier.mobile}'`);
      }
    }

    // GSTIN Format Validation (if provided, standard Indian 15-character format)
    if (supplier.gstin && supplier.gstin.trim().length > 0) {
      const cleanGst = supplier.gstin.trim().toUpperCase();
      const gstRegex = /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$/;
      if (!gstRegex.test(cleanGst)) {
        errors.push(`GSTIN '${supplier.gstin}' does not match standard 15-digit GST format (e.g. 33AAAAA0000A1Z5).`);
      }
    }

    if (errors.length > 0) return { success: false, errors };

    if (supplier.id) {
      // Update existing supplier
      const index = this.suppliers.findIndex((s) => s.id === supplier.id);
      if (index === -1) return { success: false, errors: ['Supplier not found.'] };

      const updated: Supplier = {
        ...this.suppliers[index],
        ...supplier,
        code,
        name: supplier.name!.trim(),
        updatedAt: new Date().toISOString(),
      };

      this.suppliers[index] = updated;
      this.rebuildIndexes();

      this.logAudit('Supplier', updated.id.toString(), 'UPDATE', `Updated supplier '${updated.name}' (Code: ${updated.code})`);
      return { success: true, errors: [], supplier: updated };
    } else {
      // Insert new supplier
      const openBal = Number(supplier.openingBalance) || 0;
      const newSup: Supplier = {
        id: this.nextSupplierId++,
        code,
        name: supplier.name!.trim(),
        contactPerson: supplier.contactPerson?.trim() || '',
        mobile: supplier.mobile?.trim() || '',
        altMobile: supplier.altMobile?.trim() || '',
        whatsapp: supplier.whatsapp?.trim() || '',
        email: supplier.email?.trim() || '',
        gstin: supplier.gstin?.trim().toUpperCase() || '',
        pan: supplier.pan?.trim().toUpperCase() || '',
        address: supplier.address?.trim() || '',
        city: supplier.city?.trim() || '',
        state: supplier.state?.trim() || 'Tamil Nadu',
        stateCode: supplier.stateCode?.trim() || '33',
        pinCode: supplier.pinCode?.trim() || '',
        openingBalance: openBal,
        currentBalance: openBal,
        paymentTerms: supplier.paymentTerms?.trim() || '30 Days Net',
        notes: supplier.notes?.trim() || '',
        isActive: supplier.isActive !== undefined ? supplier.isActive : true,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      // If opening balance > 0, record initial ledger entry
      if (openBal > 0) {
        this.supplierLedger.push({
          id: this.nextLedgerId++,
          supplierId: newSup.id,
          transactionType: 'OPENING_BALANCE',
          referenceId: `OPN-${newSup.code}`,
          debit: 0,
          credit: openBal,
          balanceAfter: openBal,
          notes: 'Opening balance payable registered',
          transactionDate: newSup.createdAt,
        });
      }

      this.suppliers.unshift(newSup);
      this.rebuildIndexes();

      this.logAudit('Supplier', newSup.id.toString(), 'INSERT', `Registered supplier '${newSup.name}' (Code: ${newSup.code})`);
      return { success: true, errors: [], supplier: newSup };
    }
  }

  public deactivateSupplier(id: number): boolean {
    const s = this.supplierIdIndex.get(id);
    if (!s) return false;
    s.isActive = false;
    s.updatedAt = new Date().toISOString();
    this.logAudit('Supplier', id.toString(), 'DEACTIVATE', `Deactivated supplier '${s.name}'`);
    return true;
  }

  public activateSupplier(id: number): boolean {
    const s = this.supplierIdIndex.get(id);
    if (!s) return false;
    s.isActive = true;
    s.updatedAt = new Date().toISOString();
    this.logAudit('Supplier', id.toString(), 'ACTIVATE', `Re-activated supplier '${s.name}'`);
    return true;
  }

  public hasSupplierTransactions(id: number): boolean {
    const hasPurchases = this.purchases.some((p) => p.supplierId === id);
    const hasPayments = this.supplierPayments.some((p) => p.supplierId === id);
    return hasPurchases || hasPayments;
  }

  public deleteSupplier(id: number): { success: boolean; error?: string } {
    if (this.hasSupplierTransactions(id)) {
      return {
        success: false,
        error: 'Cannot delete supplier with existing purchase bills or payment history. Deactivate the supplier instead to maintain audit integrity.',
      };
    }
    const idx = this.suppliers.findIndex((s) => s.id === id);
    if (idx === -1) return { success: false, error: 'Supplier not found.' };

    const name = this.suppliers[idx].name;
    this.suppliers.splice(idx, 1);
    this.rebuildIndexes();
    this.logAudit('Supplier', id.toString(), 'DEACTIVATE', `Deleted supplier '${name}'`);
    return { success: true };
  }

  public bulkImportSuppliers(newSuppliers: Partial<Supplier>[]): { imported: number; failed: number; errors: any[] } {
    let imported = 0;
    let failed = 0;
    const errors: any[] = [];

    for (let i = 0; i < newSuppliers.length; i++) {
      const row = newSuppliers[i];
      const res = this.saveSupplier(row);
      if (res.success) {
        imported++;
      } else {
        failed++;
        errors.push({
          row: i + 1,
          name: row.name || 'Unknown',
          code: row.code || 'Auto',
          error: res.errors.join('; '),
        });
      }
    }

    this.logAudit('Excel', 'SupplierBulkImport', 'IMPORT', `Imported ${imported} suppliers with ${failed} failures.`);
    return { imported, failed, errors };
  }

  // -------------------------------------------------------------
  // MODULE 2: INBOUND GOODS / PURCHASE ENTRY METHODS
  // -------------------------------------------------------------
  public generateNextPurchaseNumber(): string {
    const year = new Date().getFullYear();
    const prefix = `PUR-${year}-`;
    let maxSeq = 0;

    for (const pur of this.purchases) {
      if (pur.purchaseNumber && pur.purchaseNumber.startsWith(prefix)) {
        const seq = parseInt(pur.purchaseNumber.substring(prefix.length), 10);
        if (!isNaN(seq) && seq > maxSeq) maxSeq = seq;
      }
    }
    return `${prefix}${(maxSeq + 1).toString().padStart(4, '0')}`;
  }

  public searchPurchases(filter: PurchaseFilter): PagedResult<PurchaseHeader> {
    const t0 = performance.now();
    const term = filter.searchTerm?.trim().toLowerCase();

    let list = this.purchases.filter((p) => {
      if (filter.supplierId && p.supplierId !== filter.supplierId) return false;
      if (filter.status && filter.status !== 'All' && p.status !== filter.status) return false;

      if (filter.fromDate) {
        const from = new Date(filter.fromDate).getTime();
        const pDate = new Date(p.goodsReceivedDate).getTime();
        if (pDate < from) return false;
      }

      if (filter.toDate) {
        const to = new Date(filter.toDate).getTime() + 86400000;
        const pDate = new Date(p.goodsReceivedDate).getTime();
        if (pDate > to) return false;
      }

      if (term) {
        const matchNum = p.purchaseNumber.toLowerCase().includes(term);
        const matchSup = p.supplierName.toLowerCase().includes(term);
        const matchInv = p.supplierInvoiceNumber ? p.supplierInvoiceNumber.toLowerCase().includes(term) : false;
        const matchRef = p.referenceNumber ? p.referenceNumber.toLowerCase().includes(term) : false;

        if (!matchNum && !matchSup && !matchInv && !matchRef) return false;
      }

      return true;
    });

    // Sort by goodsReceivedDate DESC
    list.sort((a, b) => new Date(b.goodsReceivedDate).getTime() - new Date(a.goodsReceivedDate).getTime());

    const totalCount = list.length;
    const pageNumber = Math.max(1, filter.pageNumber);
    const pageSize = Math.max(1, filter.pageSize);
    const totalPages = Math.ceil(totalCount / pageSize);

    const startIndex = (pageNumber - 1) * pageSize;
    const items = list.slice(startIndex, startIndex + pageSize);

    const t1 = performance.now();
    const executionTimeMs = parseFloat((t1 - t0).toFixed(2));

    return {
      items,
      totalCount,
      pageNumber,
      pageSize,
      totalPages,
      executionTimeMs,
    };
  }

  public getPurchaseById(id: number): PurchaseHeader | undefined {
    return this.purchaseIdIndex.get(id);
  }

  public saveDraftPurchase(header: Partial<PurchaseHeader>): { success: boolean; errors: string[]; purchase?: PurchaseHeader } {
    const errors: string[] = [];

    if (!header.supplierId) {
      errors.push('Supplier must be selected.');
    }
    if (!header.items || header.items.length === 0) {
      errors.push('At least one product line item is required.');
    }

    if (errors.length > 0) return { success: false, errors };

    const supplier = this.supplierIdIndex.get(header.supplierId!);
    const supplierName = supplier ? supplier.name : 'Unknown Supplier';

    // Recalculate totals
    let totalQty = 0;
    let totalFreeQty = 0;
    let grossAmount = 0;
    let itemDiscount = 0;
    let taxableAmount = 0;
    let taxAmount = 0;

    const validatedItems: PurchaseItem[] = header.items!.map((it, idx) => {
      const qty = Math.max(0, Number(it.quantity) || 0);
      const freeQty = Math.max(0, Number(it.freeQuantity) || 0);
      const rate = Math.max(0, Number(it.purchaseRate) || 0);
      const discPct = Math.max(0, Number(it.discountPercent) || 0);
      const taxRate = Math.max(0, Number(it.taxRate) || 0);

      const lineGross = qty * rate;
      const lineDisc = (lineGross * discPct) / 100;
      const lineTaxable = Math.max(0, lineGross - lineDisc);
      const lineTax = (lineTaxable * taxRate) / 100;
      const lineTotal = lineTaxable + lineTax;

      totalQty += qty;
      totalFreeQty += freeQty;
      grossAmount += lineGross;
      itemDiscount += lineDisc;
      taxableAmount += lineTaxable;
      taxAmount += lineTax;

      return {
        id: it.id || idx + 1,
        purchaseId: header.id || 0,
        productId: it.productId,
        productCode: it.productCode,
        barcode: it.barcode,
        productName: it.productName,
        categoryName: it.categoryName || '',
        quantity: qty,
        freeQuantity: freeQty,
        purchaseRate: rate,
        discountPercent: discPct,
        discountAmount: lineDisc,
        taxRate,
        taxableAmount: lineTaxable,
        taxAmount: lineTax,
        lineTotal,
        batchNumber: it.batchNumber || '',
        expiryDate: it.expiryDate || undefined,
      };
    });

    const addlDisc = Math.max(0, Number(header.additionalDiscount) || 0);
    const addlCharges = Math.max(0, Number(header.additionalCharges) || 0);
    const preRound = taxableAmount + taxAmount + addlCharges - addlDisc;
    const grandTotal = Math.round(preRound);
    const roundOff = parseFloat((grandTotal - preRound).toFixed(2));
    const amtPaid = Math.max(0, Number(header.amountPaid) || 0);
    const balanceDue = Math.max(0, grandTotal - amtPaid);

    if (header.id) {
      // Update existing draft
      const idx = this.purchases.findIndex((p) => p.id === header.id);
      if (idx === -1) return { success: false, errors: ['Purchase entry not found.'] };

      if (this.purchases[idx].status === 'Confirmed') {
        return { success: false, errors: ['Cannot edit a confirmed purchase. Create a new purchase or cancel this one.'] };
      }

      const updated: PurchaseHeader = {
        ...this.purchases[idx],
        ...header,
        supplierName,
        items: validatedItems,
        totalQty,
        totalFreeQty,
        grossAmount,
        itemDiscount,
        additionalDiscount: addlDisc,
        taxableAmount,
        taxAmount,
        additionalCharges: addlCharges,
        roundOff,
        grandTotal,
        amountPaid: amtPaid,
        balanceDue,
        updatedAt: new Date().toISOString(),
      };

      this.purchases[idx] = updated;
      this.rebuildIndexes();

      this.logAudit('Purchase', updated.purchaseNumber, 'UPDATE', `Updated draft purchase #${updated.purchaseNumber}`);
      return { success: true, errors: [], purchase: updated };
    } else {
      // Create new draft
      const purNum = header.purchaseNumber || this.generateNextPurchaseNumber();
      const newPur: PurchaseHeader = {
        id: this.nextPurchaseId++,
        purchaseNumber: purNum,
        supplierId: header.supplierId!,
        supplierName,
        supplierInvoiceNumber: header.supplierInvoiceNumber?.trim() || '',
        supplierInvoiceDate: header.supplierInvoiceDate || undefined,
        goodsReceivedDate: header.goodsReceivedDate || new Date().toISOString(),
        purchaseType: (header.purchaseType === 'Cash' ? 'Cash' : 'Credit'),
        paymentDueDate: header.paymentDueDate || undefined,
        referenceNumber: header.referenceNumber?.trim() || '',
        notes: header.notes?.trim() || '',
        status: 'Draft',
        totalQty,
        totalFreeQty,
        grossAmount,
        itemDiscount,
        additionalDiscount: addlDisc,
        taxableAmount,
        taxAmount,
        additionalCharges: addlCharges,
        roundOff,
        grandTotal,
        amountPaid: amtPaid,
        balanceDue,
        createdBy: 'Admin',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        items: validatedItems,
      };

      this.purchases.unshift(newPur);
      this.rebuildIndexes();

      this.logAudit('Purchase', newPur.purchaseNumber, 'INSERT', `Created draft purchase #${newPur.purchaseNumber}`);
      return { success: true, errors: [], purchase: newPur };
    }
  }

  public confirmPurchase(purchaseId: number): { success: boolean; error?: string; purchase?: PurchaseHeader } {
    const purchase = this.purchaseIdIndex.get(purchaseId);
    if (!purchase) return { success: false, error: 'Purchase entry not found.' };

    if (purchase.status === 'Confirmed') {
      return { success: false, error: 'Purchase is already confirmed.' };
    }
    if (purchase.status === 'Cancelled') {
      return { success: false, error: 'Cannot confirm a cancelled purchase.' };
    }

    if (!purchase.items || purchase.items.length === 0) {
      return { success: false, error: 'Cannot confirm purchase with 0 line items.' };
    }

    const supplier = this.supplierIdIndex.get(purchase.supplierId);
    if (!supplier) return { success: false, error: 'Associated supplier not found.' };

    // 1. Atomically update product stock and write stock movement ledger
    for (const item of purchase.items) {
      const prod = this.idIndex.get(item.productId);
      if (prod) {
        const addedQty = item.quantity + item.freeQuantity;
        const before = prod.currentStock;
        const after = before + addedQty;

        prod.currentStock = after;
        prod.updatedAt = new Date().toISOString();

        // Also update purchaseRate in product master if current rate changed
        if (item.purchaseRate > 0) {
          prod.purchaseRate = item.purchaseRate;
        }

        this.stockMovements.unshift({
          id: this.nextMovementId++,
          productId: prod.id,
          productCode: prod.productCode,
          productName: prod.nameEn,
          referenceType: 'PURCHASE_CONFIRM',
          referenceId: purchase.purchaseNumber,
          quantity: addedQty,
          quantityBefore: before,
          quantityAfter: after,
          notes: `Purchase received: ${item.quantity} + ${item.freeQuantity} free (Bill: ${purchase.purchaseNumber})`,
          createdAt: new Date().toISOString(),
        });
      }
    }

    // 2. Update Supplier Balance & Ledger
    if (purchase.purchaseType === 'Credit') {
      // Credit purchase increases supplier's current balance by balanceDue
      const oldBal = supplier.currentBalance;
      const newBal = oldBal + purchase.balanceDue;
      supplier.currentBalance = newBal;
      supplier.updatedAt = new Date().toISOString();

      this.supplierLedger.unshift({
        id: this.nextLedgerId++,
        supplierId: supplier.id,
        transactionType: 'PURCHASE',
        referenceId: purchase.purchaseNumber,
        debit: 0,
        credit: purchase.balanceDue,
        balanceAfter: newBal,
        notes: `Credit Purchase #${purchase.purchaseNumber} (Inv #${purchase.supplierInvoiceNumber || 'N/A'})`,
        transactionDate: new Date().toISOString(),
      });
    }

    // 3. If cash paid on confirmation, record payment entry
    if (purchase.amountPaid > 0) {
      const payNum = `PAY-${new Date().getFullYear()}-${this.nextPaymentId.toString().padStart(4, '0')}`;
      this.supplierPayments.unshift({
        id: this.nextPaymentId++,
        paymentNumber: payNum,
        supplierId: supplier.id,
        supplierName: supplier.name,
        purchaseId: purchase.id,
        purchaseNumber: purchase.purchaseNumber,
        paymentDate: new Date().toISOString(),
        paymentMethod: purchase.purchaseType === 'Cash' ? 'Cash' : 'Bank Transfer',
        amount: purchase.amountPaid,
        referenceNumber: `REC-${purchase.purchaseNumber}`,
        notes: `Immediate payment recorded on bill confirmation`,
        createdAt: new Date().toISOString(),
      });
    }

    // 4. Update purchase header status
    purchase.status = 'Confirmed';
    purchase.confirmedAt = new Date().toISOString();
    purchase.updatedAt = new Date().toISOString();

    this.rebuildIndexes();

    this.logAudit(
      'Purchase',
      purchase.purchaseNumber,
      'UPDATE',
      `Confirmed purchase #${purchase.purchaseNumber} · Stock updated for ${purchase.items.length} items · Total ₹${purchase.grandTotal}`
    );

    return { success: true, purchase };
  }

  public cancelPurchase(purchaseId: number): { success: boolean; error?: string } {
    const purchase = this.purchaseIdIndex.get(purchaseId);
    if (!purchase) return { success: false, error: 'Purchase entry not found.' };

    if (purchase.status !== 'Confirmed') {
      return { success: false, error: 'Only confirmed purchases can be cancelled.' };
    }

    const supplier = this.supplierIdIndex.get(purchase.supplierId);

    // 1. Revert product stocks safely
    for (const item of purchase.items) {
      const prod = this.idIndex.get(item.productId);
      if (prod) {
        const revertQty = item.quantity + item.freeQuantity;
        const before = prod.currentStock;
        const after = Math.max(0, before - revertQty);

        prod.currentStock = after;
        prod.updatedAt = new Date().toISOString();

        this.stockMovements.unshift({
          id: this.nextMovementId++,
          productId: prod.id,
          productCode: prod.productCode,
          productName: prod.nameEn,
          referenceType: 'PURCHASE_CANCEL',
          referenceId: purchase.purchaseNumber,
          quantity: -revertQty,
          quantityBefore: before,
          quantityAfter: after,
          notes: `Purchase cancelled reversion: -${revertQty} items (Bill: ${purchase.purchaseNumber})`,
          createdAt: new Date().toISOString(),
        });
      }
    }

    // 2. Revert Supplier Balance if Credit
    if (purchase.purchaseType === 'Credit' && supplier) {
      const oldBal = supplier.currentBalance;
      const newBal = Math.max(0, oldBal - purchase.balanceDue);
      supplier.currentBalance = newBal;
      supplier.updatedAt = new Date().toISOString();

      this.supplierLedger.unshift({
        id: this.nextLedgerId++,
        supplierId: supplier.id,
        transactionType: 'PURCHASE_CANCEL',
        referenceId: `REV-${purchase.purchaseNumber}`,
        debit: purchase.balanceDue,
        credit: 0,
        balanceAfter: newBal,
        notes: `Reversal for cancelled purchase #${purchase.purchaseNumber}`,
        transactionDate: new Date().toISOString(),
      });
    }

    purchase.status = 'Cancelled';
    purchase.cancelledAt = new Date().toISOString();
    purchase.updatedAt = new Date().toISOString();

    this.rebuildIndexes();

    this.logAudit(
      'Purchase',
      purchase.purchaseNumber,
      'UPDATE',
      `Cancelled purchase #${purchase.purchaseNumber} · Stock and supplier balances safely reverted`
    );

    return { success: true };
  }

  public recordSupplierPayment(payment: Partial<SupplierPayment>): { success: boolean; errors: string[]; payment?: SupplierPayment } {
    const errors: string[] = [];
    if (!payment.supplierId) errors.push('Supplier is required.');
    if (!payment.amount || payment.amount <= 0) errors.push('Payment amount must be greater than zero.');

    const supplier = this.supplierIdIndex.get(payment.supplierId!);
    if (!supplier) errors.push('Supplier not found.');

    if (errors.length > 0) return { success: false, errors };

    const payNum = `PAY-${new Date().getFullYear()}-${this.nextPaymentId.toString().padStart(4, '0')}`;
    const amount = Number(payment.amount);

    const newPay: SupplierPayment = {
      id: this.nextPaymentId++,
      paymentNumber: payNum,
      supplierId: supplier!.id,
      supplierName: supplier!.name,
      purchaseId: payment.purchaseId || undefined,
      purchaseNumber: payment.purchaseNumber || undefined,
      paymentDate: payment.paymentDate || new Date().toISOString(),
      paymentMethod: payment.paymentMethod || 'Cash',
      amount,
      referenceNumber: payment.referenceNumber?.trim() || '',
      notes: payment.notes?.trim() || '',
      createdAt: new Date().toISOString(),
    };

    // Update supplier current balance
    const oldBal = supplier!.currentBalance;
    const newBal = Math.max(0, oldBal - amount);
    supplier!.currentBalance = newBal;
    supplier!.updatedAt = new Date().toISOString();

    // Ledger entry
    this.supplierLedger.unshift({
      id: this.nextLedgerId++,
      supplierId: supplier!.id,
      transactionType: 'PAYMENT',
      referenceId: payNum,
      debit: amount,
      credit: 0,
      balanceAfter: newBal,
      notes: `Payment made via ${newPay.paymentMethod} (Ref: ${newPay.referenceNumber || 'N/A'})`,
      transactionDate: newPay.paymentDate,
    });

    this.supplierPayments.unshift(newPay);

    this.logAudit('SupplierPayment', payNum, 'INSERT', `Recorded payment of ₹${amount} to '${supplier!.name}' (${newPay.paymentMethod})`);
    return { success: true, errors: [], payment: newPay };
  }

  public getSupplierLedger(supplierId: number): SupplierLedgerEntry[] {
    return this.supplierLedger.filter((l) => l.supplierId === supplierId);
  }

  public getSupplierPayments(supplierId?: number): SupplierPayment[] {
    if (!supplierId) return this.supplierPayments;
    return this.supplierPayments.filter((p) => p.supplierId === supplierId);
  }

  public getStockMovements(productId?: number): StockMovement[] {
    if (!productId) return this.stockMovements;
    return this.stockMovements.filter((m) => m.productId === productId);
  }

  public getMetrics() {
    const total = this.products.filter((p) => p.isActive).length;
    const lowStock = this.products.filter((p) => p.isActive && p.currentStock <= p.reorderLevel).length;
    const outOfStock = this.products.filter((p) => p.isActive && p.currentStock <= 0).length;
    const excessStock = this.products.filter((p) => p.isActive && p.maxStockLevel > 0 && p.currentStock > p.maxStockLevel).length;
    const totalSuppliers = this.suppliers.filter((s) => s.isActive).length;
    const pendingDrafts = this.purchases.filter((p) => p.status === 'Draft').length;
    const totalPurchasesConfirmed = this.purchases.filter((p) => p.status === 'Confirmed').length;
    const totalPayableToSuppliers = this.suppliers.reduce((acc, s) => acc + (s.currentBalance > 0 ? s.currentBalance : 0), 0);
    const totalStockQuantity = this.products.reduce((acc, p) => acc + (p.isActive ? p.currentStock : 0), 0);
    const stockValueAtCost = this.products.reduce((acc, p) => acc + (p.isActive ? p.currentStock * p.purchaseRate : 0), 0);
    const stockValueAtSale = this.products.reduce((acc, p) => acc + (p.isActive ? p.currentStock * p.saleRate : 0), 0);

    return {
      total,
      lowStock,
      outOfStock,
      excessStock,
      totalStockQuantity,
      stockValueAtCost,
      stockValueAtSale,
      is50kLoaded: this.is50kLoaded,
      totalSuppliers,
      pendingDrafts,
      totalPurchasesConfirmed,
      totalPayableToSuppliers,
    };
  }

  // -------------------------------------------------------------
  // PHASE 3: STOCK MANAGEMENT & RECONCILIATION METHODS
  // -------------------------------------------------------------
  public getStockDashboardMetrics(): StockDashboardMetrics {
    const active = this.products.filter((p) => p.isActive);
    const totalStockQuantity = active.reduce((sum, p) => sum + p.currentStock, 0);
    const outOfStockCount = active.filter((p) => p.currentStock <= 0).length;
    const lowStockCount = active.filter((p) => p.currentStock > 0 && p.currentStock <= p.reorderLevel).length;
    const excessStockCount = active.filter((p) => p.maxStockLevel > 0 && p.currentStock > p.maxStockLevel).length;
    const stockValueAtCost = active.reduce((sum, p) => sum + p.currentStock * p.purchaseRate, 0);
    const stockValueAtSale = active.reduce((sum, p) => sum + p.currentStock * p.saleRate, 0);
    const potentialGrossMargin = stockValueAtSale - stockValueAtCost;

    // Distinct products adjusted
    const adjustedProdIds = new Set<number>();
    for (const adj of this.stockAdjustments) {
      if (adj.status === 'Confirmed') {
        for (const item of adj.items) {
          adjustedProdIds.add(item.productId);
        }
      }
    }

    return {
      totalActiveProducts: active.length,
      totalStockQuantity,
      outOfStockCount,
      lowStockCount,
      excessStockCount,
      adjustedProductsCount: adjustedProdIds.size,
      stockValueAtCost,
      stockValueAtSale,
      potentialGrossMargin,
      recentMovements: this.stockMovements.slice(0, 15),
    };
  }

  public getCurrentStockSummary(filter: {
    searchTerm?: string;
    categoryId?: number;
    brandId?: number;
    stockStatus?: string;
    sortBy?: string;
    sortDescending?: boolean;
    pageNumber: number;
    pageSize: number;
  }): PagedResult<CurrentStockSummary> {
    const t0 = performance.now();
    const term = filter.searchTerm?.trim().toLowerCase();

    // Compute purchased & free quantities per product from confirmed purchases
    const purchasedMap = new Map<number, number>();
    const freeMap = new Map<number, number>();

    for (const pur of this.purchases) {
      if (pur.status === 'Confirmed') {
        for (const it of pur.items) {
          purchasedMap.set(it.productId, (purchasedMap.get(it.productId) || 0) + it.quantity);
          freeMap.set(it.productId, (freeMap.get(it.productId) || 0) + it.freeQuantity);
        }
      }
    }

    // Compute net adjustments per product from confirmed adjustments
    const adjMap = new Map<number, number>();
    for (const adj of this.stockAdjustments) {
      if (adj.status === 'Confirmed') {
        for (const it of adj.items) {
          adjMap.set(it.productId, (adjMap.get(it.productId) || 0) + it.differenceQuantity);
        }
      }
    }

    let list = this.products.filter((p) => {
      if (!p.isActive) return false;
      if (filter.categoryId && filter.categoryId > 0 && p.categoryId !== filter.categoryId) return false;
      if (filter.brandId && filter.brandId > 0 && p.brandId !== filter.brandId) return false;

      // Status filter
      if (filter.stockStatus && filter.stockStatus !== 'All') {
        if (filter.stockStatus === 'In Stock' && (p.currentStock <= p.reorderLevel || p.currentStock <= 0)) return false;
        if (filter.stockStatus === 'Low Stock' && (p.currentStock <= 0 || p.currentStock > p.reorderLevel)) return false;
        if (filter.stockStatus === 'Out of Stock' && p.currentStock > 0) return false;
        if (filter.stockStatus === 'Excess Stock' && (p.maxStockLevel <= 0 || p.currentStock <= p.maxStockLevel)) return false;
      }

      if (term) {
        const matchEn = p.nameEn.toLowerCase().includes(term);
        const matchTa = p.nameTa ? p.nameTa.toLowerCase().includes(term) : false;
        const matchBar = p.barcode.toLowerCase().includes(term);
        const matchCode = p.productCode.toLowerCase().includes(term);
        if (!matchEn && !matchTa && !matchBar && !matchCode) return false;
      }

      return true;
    });

    // Map to summaries
    const summaries: CurrentStockSummary[] = list.map((p) => {
      let status: 'In Stock' | 'Low Stock' | 'Out of Stock' | 'Excess Stock' = 'In Stock';
      if (p.currentStock <= 0) status = 'Out of Stock';
      else if (p.currentStock <= p.reorderLevel) status = 'Low Stock';
      else if (p.maxStockLevel > 0 && p.currentStock > p.maxStockLevel) status = 'Excess Stock';

      return {
        productId: p.id,
        productCode: p.productCode,
        barcode: p.barcode,
        productName: p.nameEn,
        categoryName: p.categoryName || '—',
        brandName: p.brandName || '—',
        unit: p.unit,
        openingStock: p.openingStock,
        purchasedQuantity: purchasedMap.get(p.id) || 0,
        freeQuantityReceived: freeMap.get(p.id) || 0,
        soldQuantity: 0,
        returnedQuantity: 0,
        adjustmentQuantity: adjMap.get(p.id) || 0,
        currentStock: p.currentStock,
        reorderLevel: p.reorderLevel,
        maxStockLevel: p.maxStockLevel,
        purchaseRate: p.purchaseRate,
        saleRate: p.saleRate,
        stockValuePurchase: p.currentStock * p.purchaseRate,
        stockValueSale: p.currentStock * p.saleRate,
        stockStatus: status,
      };
    });

    // Sort
    const sortBy = filter.sortBy || 'productName';
    const isDesc = !!filter.sortDescending;
    summaries.sort((a, b) => {
      let valA: any = (a as any)[sortBy];
      let valB: any = (b as any)[sortBy];
      if (typeof valA === 'string') valA = valA.toLowerCase();
      if (typeof valB === 'string') valB = valB.toLowerCase();
      if (valA < valB) return isDesc ? 1 : -1;
      if (valA > valB) return isDesc ? -1 : 1;
      return 0;
    });

    const totalCount = summaries.length;
    const pageNumber = Math.max(1, filter.pageNumber);
    const pageSize = Math.max(1, filter.pageSize);
    const totalPages = Math.ceil(totalCount / pageSize);

    const startIndex = (pageNumber - 1) * pageSize;
    const items = summaries.slice(startIndex, startIndex + pageSize);

    const t1 = performance.now();
    const executionTimeMs = parseFloat((t1 - t0).toFixed(2));

    return {
      items,
      totalCount,
      pageNumber,
      pageSize,
      totalPages,
      executionTimeMs,
    };
  }

  public getStockValuation(categoryId?: number) {
    let prods = this.products.filter((p) => p.isActive && p.currentStock > 0);
    if (categoryId && categoryId > 0) {
      prods = prods.filter((p) => p.categoryId === categoryId);
    }

    return prods.map((p) => {
      const stockValueCost = p.currentStock * p.purchaseRate;
      const stockValueSale = p.currentStock * p.saleRate;
      return {
        productId: p.id,
        productCode: p.productCode,
        barcode: p.barcode,
        productName: p.nameEn,
        categoryName: p.categoryName || 'Uncategorized',
        currentStock: p.currentStock,
        purchaseRate: p.purchaseRate,
        saleRate: p.saleRate,
        stockValueCost,
        stockValueSale,
        potentialMargin: stockValueSale - stockValueCost,
        valuationMethod: 'Purchase Cost (FIFO Equivalent)',
      };
    });
  }

  public generateNextAdjustmentNumber(): string {
    const year = new Date().getFullYear();
    const prefix = `ADJ-${year}-`;
    let maxNum = 0;
    for (const a of this.stockAdjustments) {
      if (a.adjustmentNumber && a.adjustmentNumber.startsWith(prefix)) {
        const numPart = parseInt(a.adjustmentNumber.substring(prefix.length), 10);
        if (!isNaN(numPart) && numPart > maxNum) maxNum = numPart;
      }
    }
    return `${prefix}${(maxNum + 1).toString().padStart(4, '0')}`;
  }

  public generateNextVerificationNumber(): string {
    const year = new Date().getFullYear();
    const prefix = `VER-${year}-`;
    let maxNum = 0;
    for (const v of this.physicalVerifications) {
      if (v.verificationNumber && v.verificationNumber.startsWith(prefix)) {
        const numPart = parseInt(v.verificationNumber.substring(prefix.length), 10);
        if (!isNaN(numPart) && numPart > maxNum) maxNum = numPart;
      }
    }
    return `${prefix}${(maxNum + 1).toString().padStart(4, '0')}`;
  }

  public saveStockAdjustmentDraft(adj: Partial<StockAdjustment>): {
    success: boolean;
    errors: string[];
    adjustment?: StockAdjustment;
  } {
    const errors: string[] = [];

    if (!adj.reason || adj.reason.trim().length < 3) {
      errors.push('A valid Reason is required for stock adjustment (min 3 chars).');
    }
    if (!adj.items || adj.items.length === 0) {
      errors.push('At least one product item is required for stock adjustment.');
    }

    if (errors.length > 0) return { success: false, errors };

    // Validate items and ensure no negative stock violation
    for (const it of adj.items!) {
      const prod = this.idIndex.get(it.productId);
      if (!prod) {
        errors.push(`Product ID ${it.productId} not found.`);
        continue;
      }

      if (it.differenceQuantity < 0) {
        const resulting = prod.currentStock + it.differenceQuantity;
        if (resulting < 0) {
          errors.push(
            `Adjustment of ${it.differenceQuantity} for '${prod.nameEn}' would cause negative stock (${resulting}). Operation rejected.`
          );
        }
      }
    }

    if (errors.length > 0) return { success: false, errors };

    const totalDiff = adj.items!.reduce((sum, i) => sum + i.differenceQuantity, 0);
    const totalCost = adj.items!.reduce((sum, i) => sum + i.differenceQuantity * i.unitCost, 0);

    if (adj.id) {
      const idx = this.stockAdjustments.findIndex((a) => a.id === adj.id);
      if (idx === -1) return { success: false, errors: ['Stock adjustment not found.'] };

      if (this.stockAdjustments[idx].status === 'Confirmed') {
        return { success: false, errors: ['Cannot edit an already confirmed stock adjustment.'] };
      }

      const updated: StockAdjustment = {
        ...this.stockAdjustments[idx],
        ...adj,
        totalItems: adj.items!.length,
        totalDifferenceQuantity: totalDiff,
        totalCostImpact: totalCost,
        updatedAt: new Date().toISOString(),
      } as StockAdjustment;

      this.stockAdjustments[idx] = updated;
      this.logAudit('StockAdjustment', updated.adjustmentNumber, 'UPDATE', `Updated draft adjustment #${updated.adjustmentNumber}`);
      return { success: true, errors: [], adjustment: updated };
    } else {
      const num = adj.adjustmentNumber || this.generateNextAdjustmentNumber();
      const newAdj: StockAdjustment = {
        id: Date.now() + Math.floor(Math.random() * 1000),
        adjustmentNumber: num,
        adjustmentDate: adj.adjustmentDate || new Date().toISOString(),
        reason: adj.reason!.trim(),
        notes: adj.notes?.trim() || '',
        status: 'Draft',
        totalItems: adj.items!.length,
        totalDifferenceQuantity: totalDiff,
        totalCostImpact: totalCost,
        createdBy: 'Admin',
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        items: adj.items!.map((it, idx) => ({
          ...it,
          id: it.id || idx + 1,
        })),
      };

      this.stockAdjustments.unshift(newAdj);
      this.logAudit('StockAdjustment', newAdj.adjustmentNumber, 'INSERT', `Created draft stock adjustment #${newAdj.adjustmentNumber}`);
      return { success: true, errors: [], adjustment: newAdj };
    }
  }

  public confirmStockAdjustment(adjId: number): {
    success: boolean;
    error?: string;
    adjustment?: StockAdjustment;
  } {
    const adj = this.stockAdjustments.find((a) => a.id === adjId);
    if (!adj) return { success: false, error: 'Stock adjustment not found.' };

    if (adj.status === 'Confirmed') {
      return { success: false, error: 'Adjustment is already confirmed. Duplicate confirmation prevented.' };
    }
    if (adj.status === 'Cancelled') {
      return { success: false, error: 'Cannot confirm a cancelled stock adjustment.' };
    }

    // Atomically validate that no item produces negative stock
    for (const it of adj.items) {
      const prod = this.idIndex.get(it.productId);
      if (prod && it.differenceQuantity < 0) {
        const resulting = prod.currentStock + it.differenceQuantity;
        if (resulting < 0) {
          return {
            success: false,
            error: `Negative stock prevented: '${prod.nameEn}' would drop to ${resulting}. Operation rolled back.`,
          };
        }
      }
    }

    // Apply adjustments and record movements
    for (const it of adj.items) {
      const prod = this.idIndex.get(it.productId);
      if (prod) {
        const before = prod.currentStock;
        const after = before + it.differenceQuantity;
        prod.currentStock = after;
        prod.updatedAt = new Date().toISOString();

        this.stockMovements.unshift({
          id: Date.now() + Math.floor(Math.random() * 1000),
          productId: prod.id,
          productCode: prod.productCode,
          productName: prod.nameEn,
          referenceType: it.differenceQuantity >= 0 ? 'ADJUSTMENT' : 'ADJUSTMENT',
          referenceId: adj.adjustmentNumber,
          quantity: it.differenceQuantity,
          quantityBefore: before,
          quantityAfter: after,
          notes: `${it.adjustmentType}: ${it.reason || adj.reason} (${it.differenceQuantity >= 0 ? '+' : ''}${it.differenceQuantity})`,
          createdAt: new Date().toISOString(),
        });
      }
    }

    adj.status = 'Confirmed';
    adj.confirmedAt = new Date().toISOString();
    adj.updatedAt = new Date().toISOString();

    this.rebuildIndexes();
    this.logAudit(
      'StockAdjustment',
      adj.adjustmentNumber,
      'UPDATE',
      `Confirmed adjustment #${adj.adjustmentNumber} (${adj.items.length} items adjusted)`
    );

    return { success: true, adjustment: adj };
  }

  public cancelStockAdjustment(adjId: number): { success: boolean; error?: string } {
    const adj = this.stockAdjustments.find((a) => a.id === adjId);
    if (!adj) return { success: false, error: 'Stock adjustment not found.' };

    if (adj.status !== 'Confirmed') {
      return { success: false, error: 'Only confirmed adjustments can be cancelled/reverted.' };
    }

    // Check negative stock for reversal
    for (const it of adj.items) {
      const prod = this.idIndex.get(it.productId);
      if (prod) {
        const reverseDelta = -it.differenceQuantity;
        if (reverseDelta < 0 && prod.currentStock + reverseDelta < 0) {
          return {
            success: false,
            error: `Reversal would cause negative stock for '${prod.nameEn}'. Operation rejected.`,
          };
        }
      }
    }

    // Revert deltas
    for (const it of adj.items) {
      const prod = this.idIndex.get(it.productId);
      if (prod) {
        const reverseDelta = -it.differenceQuantity;
        const before = prod.currentStock;
        const after = before + reverseDelta;
        prod.currentStock = after;
        prod.updatedAt = new Date().toISOString();

        this.stockMovements.unshift({
          id: Date.now() + Math.floor(Math.random() * 1000),
          productId: prod.id,
          productCode: prod.productCode,
          productName: prod.nameEn,
          referenceType: 'ADJUSTMENT',
          referenceId: `REV-${adj.adjustmentNumber}`,
          quantity: reverseDelta,
          quantityBefore: before,
          quantityAfter: after,
          notes: `Reversal of cancelled adjustment #${adj.adjustmentNumber}`,
          createdAt: new Date().toISOString(),
        });
      }
    }

    adj.status = 'Cancelled';
    adj.cancelledAt = new Date().toISOString();
    adj.updatedAt = new Date().toISOString();

    this.rebuildIndexes();
    this.logAudit('StockAdjustment', adj.adjustmentNumber, 'UPDATE', `Cancelled stock adjustment #${adj.adjustmentNumber}`);
    return { success: true };
  }

  public savePhysicalVerificationDraft(ver: Partial<PhysicalStockVerification>): {
    success: boolean;
    errors: string[];
    verification?: PhysicalStockVerification;
  } {
    const errors: string[] = [];
    if (!ver.items || ver.items.length === 0) {
      errors.push('At least one product count is required.');
    }
    if (errors.length > 0) return { success: false, errors };

    const discrepancies = ver.items!.filter((i) => i.differenceQuantity !== 0).length;
    const netDiff = ver.items!.reduce((sum, i) => sum + i.differenceQuantity, 0);

    const num = ver.verificationNumber || this.generateNextVerificationNumber();
    const newVer: PhysicalStockVerification = {
      id: Date.now() + Math.floor(Math.random() * 1000),
      verificationNumber: num,
      verificationDate: ver.verificationDate || new Date().toISOString(),
      categoryId: ver.categoryId,
      categoryName: ver.categoryName || 'All Categories',
      status: 'Draft',
      totalCountedProducts: ver.items!.length,
      totalDiscrepancies: discrepancies,
      netDifferenceQuantity: netDiff,
      notes: ver.notes || '',
      createdBy: 'Admin',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      items: ver.items!.map((it, idx) => ({
        ...it,
        id: it.id || idx + 1,
      })),
    };

    this.physicalVerifications.unshift(newVer);
    this.logAudit('PhysicalVerification', newVer.verificationNumber, 'INSERT', `Created physical audit draft #${newVer.verificationNumber}`);
    return { success: true, errors: [], verification: newVer };
  }

  public confirmPhysicalVerification(verId: number): {
    success: boolean;
    error?: string;
    verification?: PhysicalStockVerification;
  } {
    const ver = this.physicalVerifications.find((v) => v.id === verId);
    if (!ver) return { success: false, error: 'Verification record not found.' };

    if (ver.status === 'Confirmed') {
      return { success: false, error: 'Verification count is already confirmed.' };
    }

    // Apply reconciliation for items with discrepancy
    for (const it of ver.items) {
      const prod = this.idIndex.get(it.productId);
      if (prod) {
        // Reconcile physical count to current stock
        const liveDifference = it.physicalCount - prod.currentStock;
        if (liveDifference !== 0) {
          const before = prod.currentStock;
          const after = it.physicalCount;

          if (after < 0) {
            return {
              success: false,
              error: `Reconciled physical count cannot be negative for '${prod.nameEn}'.`,
            };
          }

          prod.currentStock = after;
          prod.updatedAt = new Date().toISOString();

          this.stockMovements.unshift({
            id: Date.now() + Math.floor(Math.random() * 1000),
            productId: prod.id,
            productCode: prod.productCode,
            productName: prod.nameEn,
            referenceType: 'ADJUSTMENT',
            referenceId: ver.verificationNumber,
            quantity: liveDifference,
            quantityBefore: before,
            quantityAfter: after,
            notes: `Physical Audit Reconciled (Physical: ${it.physicalCount}, System was: ${before})`,
            createdAt: new Date().toISOString(),
          });
        }
      }
    }

    ver.status = 'Confirmed';
    ver.confirmedAt = new Date().toISOString();
    ver.updatedAt = new Date().toISOString();

    this.rebuildIndexes();
    this.logAudit('PhysicalVerification', ver.verificationNumber, 'UPDATE', `Confirmed physical verification #${ver.verificationNumber}`);
    return { success: true, verification: ver };
  }

  public getAllStockAdjustments(): StockAdjustment[] {
    return this.stockAdjustments;
  }

  public getAllPhysicalVerifications(): PhysicalStockVerification[] {
    return this.physicalVerifications;
  }

  private logAudit(entity: string, entityId: string, action: any, details: string) {
    this.auditLogs.unshift({
      id: Date.now() + Math.floor(Math.random() * 1000),
      entity,
      entityId,
      action,
      details,
      timestamp: new Date().toISOString(),
    });
  }
}

export const db = new SQLiteIndexedEngine();
