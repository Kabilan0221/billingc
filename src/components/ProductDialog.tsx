import React, { useState, useEffect, useMemo } from 'react';
import { X, Sparkles, Check, AlertCircle, Calculator } from 'lucide-react';
import { Product } from '../types';
import { db } from '../services/dbEngine';

interface ProductDialogProps {
  product: Product | null;
  isOpen: boolean;
  onClose: () => void;
  onSaved: () => void;
}

export const ProductDialog: React.FC<ProductDialogProps> = ({
  product,
  isOpen,
  onClose,
  onSaved,
}) => {
  const [productCode, setProductCode] = useState('');
  const [barcode, setBarcode] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [nameTa, setNameTa] = useState('');
  const [categoryId, setCategoryId] = useState<number | ''>('');
  const [subcategoryId, setSubcategoryId] = useState<number | ''>('');
  const [brandId, setBrandId] = useState<number | ''>('');
  const [unit, setUnit] = useState('PCS');
  const [hsnCode, setHsnCode] = useState('');

  const [purchaseRate, setPurchaseRate] = useState<number>(0);
  const [saleRate, setSaleRate] = useState<number>(0);
  const [wholesaleRate, setWholesaleRate] = useState<number>(0);
  const [mrp, setMrp] = useState<number>(0);
  const [taxRate, setTaxRate] = useState<number>(5);

  const [openingStock, setOpeningStock] = useState<number>(0);
  const [currentStock, setCurrentStock] = useState<number>(0);
  const [reorderLevel, setReorderLevel] = useState<number>(5);
  const [maxStockLevel, setMaxStockLevel] = useState<number>(500);
  const [isActive, setIsActive] = useState<boolean>(true);

  const [validationErrors, setValidationErrors] = useState<string[]>([]);

  const categories = useMemo(() => db.getCategories(), []);
  const subcategories = useMemo(
    () => db.getSubcategories(typeof categoryId === 'number' ? categoryId : undefined),
    [categoryId]
  );
  const brands = useMemo(() => db.getBrands(), []);

  // Initialize data on open
  useEffect(() => {
    if (product) {
      setProductCode(product.productCode);
      setBarcode(product.barcode);
      setNameEn(product.nameEn);
      setNameTa(product.nameTa || '');
      setCategoryId(product.categoryId || '');
      setSubcategoryId(product.subcategoryId || '');
      setBrandId(product.brandId || '');
      setUnit(product.unit || 'PCS');
      setHsnCode(product.hsnCode || '');

      setPurchaseRate(product.purchaseRate);
      setSaleRate(product.saleRate);
      setWholesaleRate(product.wholesaleRate);
      setMrp(product.mrp);
      setTaxRate(product.taxRate);

      setOpeningStock(product.openingStock);
      setCurrentStock(product.currentStock);
      setReorderLevel(product.reorderLevel);
      setMaxStockLevel(product.maxStockLevel);
      setIsActive(product.isActive);
    } else {
      // New Product default
      const stamp = Date.now().toString().slice(-6);
      setProductCode(`PRD${stamp}`);
      generateEan13();
      setNameEn('');
      setNameTa('');
      setCategoryId('');
      setSubcategoryId('');
      setBrandId('');
      setUnit('PCS');
      setHsnCode('');
      setPurchaseRate(0);
      setSaleRate(0);
      setWholesaleRate(0);
      setMrp(0);
      setTaxRate(5);
      setOpeningStock(0);
      setCurrentStock(0);
      setReorderLevel(5);
      setMaxStockLevel(500);
      setIsActive(true);
    }
    setValidationErrors([]);
  }, [product, isOpen]);

  // Margin calculation
  const marginStats = useMemo(() => {
    if (purchaseRate <= 0) return { pct: 0, rupees: 0, isNegative: false };
    const diff = saleRate - purchaseRate;
    const pct = ((diff / purchaseRate) * 100).toFixed(1);
    return { pct: Number(pct), rupees: diff, isNegative: diff < 0 };
  }, [purchaseRate, saleRate]);

  // EAN-13 generator with standard checksum
  const generateEan13 = () => {
    const raw = '890' + Math.floor(100000000 + Math.random() * 900000000).toString();
    const prefix12 = raw.slice(0, 12);
    let sum = 0;
    for (let i = 0; i < 12; i++) {
      const d = parseInt(prefix12[i], 10);
      sum += i % 2 === 0 ? d : d * 3;
    }
    const checkDigit = (10 - (sum % 10)) % 10;
    setBarcode(prefix12 + checkDigit.toString());
  };

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    setValidationErrors([]);

    const payload: Partial<Product> = {
      id: product ? product.id : undefined,
      productCode,
      barcode,
      nameEn,
      nameTa,
      categoryId: typeof categoryId === 'number' ? categoryId : null,
      subcategoryId: typeof subcategoryId === 'number' ? subcategoryId : null,
      brandId: typeof brandId === 'number' ? brandId : null,
      unit,
      hsnCode,
      purchaseRate: Number(purchaseRate) || 0,
      saleRate: Number(saleRate) || 0,
      wholesaleRate: Number(wholesaleRate) || 0,
      mrp: Number(mrp) || 0,
      taxRate: Number(taxRate) || 0,
      openingStock: Number(openingStock) || 0,
      currentStock: Number(currentStock) || 0,
      reorderLevel: Number(reorderLevel) || 5,
      maxStockLevel: Number(maxStockLevel) || 1000,
      isActive,
    };

    const res = db.saveProduct(payload);
    if (!res.success) {
      setValidationErrors(res.errors);
      return;
    }

    onSaved();
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-2xl overflow-hidden flex flex-col max-h-[92vh] animate-in fade-in zoom-in-95 duration-150">
        {/* Win7 Dialog Header */}
        <div className="bg-gradient-to-r from-[#173e65] to-[#255a90] text-white px-4 py-2.5 flex items-center justify-between shadow-xs select-none">
          <div className="flex items-center gap-2">
            <div className="w-4 h-4 rounded bg-white/20 flex items-center justify-center text-[10px] font-bold">
              ✓
            </div>
            <h3 className="text-xs font-semibold tracking-wide">
              {product ? `Edit Product — ${product.productCode}` : 'Product Master — New Product Creation'}
            </h3>
          </div>
          <button
            onClick={onClose}
            className="w-5 h-5 flex items-center justify-center rounded hover:bg-white/20 text-white/80 hover:text-white"
          >
            <X className="w-3.5 h-3.5" />
          </button>
        </div>

        {/* Validation Errors Box */}
        {validationErrors.length > 0 && (
          <div className="bg-rose-50 border-b border-rose-200 px-4 py-2 flex items-start gap-2 text-rose-800 text-xs">
            <AlertCircle className="w-4 h-4 text-rose-600 shrink-0 mt-0.5" />
            <div className="space-y-0.5">
              <span className="font-semibold">Validation checks prevented saving:</span>
              <ul className="list-disc list-inside">
                {validationErrors.map((err, i) => (
                  <li key={i}>{err}</li>
                ))}
              </ul>
            </div>
          </div>
        )}

        {/* Scrollable Form Body */}
        <form onSubmit={handleSave} className="overflow-y-auto p-4 space-y-4 text-xs">
          {/* Section 1: Identification */}
          <div className="border border-slate-200 rounded p-3 bg-slate-50/50 space-y-3">
            <div className="font-semibold text-slate-800 text-[11px] uppercase tracking-wider flex items-center justify-between">
              <span>1. Product Identification & Names</span>
              <span className="text-slate-400 font-normal">Tamil & English UTF-8</span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
              <div>
                <label className="block text-slate-600 font-medium mb-1">Product Code *</label>
                <input
                  type="text"
                  required
                  value={productCode}
                  onChange={(e) => setProductCode(e.target.value)}
                  placeholder="PRD0001"
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div className="md:col-span-2">
                <div className="flex items-center justify-between mb-1">
                  <label className="text-slate-600 font-medium">Barcode (EAN-13 / Code 128) *</label>
                  <button
                    type="button"
                    onClick={generateEan13}
                    className="text-[10px] text-[#106ebe] hover:underline font-semibold"
                  >
                    Generate EAN-13
                  </button>
                </div>
                <input
                  type="text"
                  required
                  value={barcode}
                  onChange={(e) => setBarcode(e.target.value)}
                  placeholder="8901234567890"
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>
            </div>

            {/* Names */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label className="block text-slate-600 font-medium mb-1">Name (English) *</label>
                <input
                  type="text"
                  required
                  value={nameEn}
                  onChange={(e) => setNameEn(e.target.value)}
                  placeholder="e.g. Ponni Boiled Rice 5kg"
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Name in Tamil (தமிழ் பெயர்)</label>
                <input
                  type="text"
                  value={nameTa}
                  onChange={(e) => setNameTa(e.target.value)}
                  placeholder="எ.கா. பொன்னி புழுங்கல் அரிசி 5கிகி"
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-sans focus:border-[#106ebe] focus:outline-none"
                />
              </div>
            </div>

            {/* Categories & Brands */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 pt-1">
              <div>
                <label className="block text-slate-600 font-medium mb-1">Category</label>
                <select
                  value={categoryId}
                  onChange={(e) => {
                    setCategoryId(e.target.value ? Number(e.target.value) : '');
                    setSubcategoryId('');
                  }}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                >
                  <option value="">-- None --</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Subcategory</label>
                <select
                  value={subcategoryId}
                  onChange={(e) => setSubcategoryId(e.target.value ? Number(e.target.value) : '')}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                >
                  <option value="">-- None --</option>
                  {subcategories.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Brand</label>
                <select
                  value={brandId}
                  onChange={(e) => setBrandId(e.target.value ? Number(e.target.value) : '')}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                >
                  <option value="">-- None --</option>
                  {brands.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 pt-1">
              <div>
                <label className="block text-slate-600 font-medium mb-1">Unit</label>
                <select
                  value={unit}
                  onChange={(e) => setUnit(e.target.value)}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                >
                  <option value="PCS">PCS (Pieces)</option>
                  <option value="KG">KG (Kilograms)</option>
                  <option value="G">G (Grams)</option>
                  <option value="LTR">LTR (Litres)</option>
                  <option value="ML">ML (Millilitres)</option>
                  <option value="PACK">PACK</option>
                  <option value="BOX">BOX</option>
                  <option value="BAG">BAG</option>
                </select>
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">HSN / SAC Code</label>
                <input
                  type="text"
                  value={hsnCode}
                  onChange={(e) => setHsnCode(e.target.value)}
                  placeholder="e.g. 1006"
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div className="md:col-span-2 flex items-center gap-2 pt-4">
                <label className="flex items-center gap-2 cursor-pointer text-slate-700 font-medium">
                  <input
                    type="checkbox"
                    checked={isActive}
                    onChange={(e) => setIsActive(e.target.checked)}
                    className="accent-[#106ebe] w-4 h-4 rounded"
                  />
                  <span>Active for Billing & POS</span>
                </label>
              </div>
            </div>
          </div>

          {/* Section 2: Pricing & Tax */}
          <div className="border border-slate-200 rounded p-3 bg-slate-50/50 space-y-3">
            <div className="font-semibold text-slate-800 text-[11px] uppercase tracking-wider flex items-center justify-between">
              <span>2. Pricing, Rates & GST Tax Structure</span>
              <div
                className={`font-mono text-[11px] font-bold px-2 py-0.5 rounded ${
                  marginStats.isNegative
                    ? 'bg-rose-100 text-rose-800'
                    : 'bg-emerald-100 text-emerald-800'
                }`}
              >
                Profit Margin: {marginStats.pct}% (₹{marginStats.rupees.toFixed(2)})
              </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-5 gap-3">
              <div>
                <label className="block text-slate-600 font-medium mb-1">Pur. Rate (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={purchaseRate}
                  onChange={(e) => setPurchaseRate(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-800 font-bold mb-1">Sale Rate (₹) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  required
                  value={saleRate}
                  onChange={(e) => setSaleRate(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border-2 border-[#106ebe] rounded font-mono font-bold text-slate-900 focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Wholesale (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={wholesaleRate}
                  onChange={(e) => setWholesaleRate(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">MRP (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={mrp}
                  onChange={(e) => setMrp(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">GST Tax %</label>
                <select
                  value={taxRate}
                  onChange={(e) => setTaxRate(Number(e.target.value))}
                  className="w-full px-2 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                >
                  <option value={0}>0% (Exempt)</option>
                  <option value={5}>5% (GST)</option>
                  <option value={12}>12% (GST)</option>
                  <option value={18}>18% (GST)</option>
                  <option value={28}>28% (GST)</option>
                </select>
              </div>
            </div>
          </div>

          {/* Section 3: Stock Control */}
          <div className="border border-slate-200 rounded p-3 bg-slate-50/50 space-y-3">
            <div className="font-semibold text-slate-800 text-[11px] uppercase tracking-wider">
              3. Inventory & Thresholds
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              <div>
                <label className="block text-slate-600 font-medium mb-1">Opening Stock</label>
                <input
                  type="number"
                  step="1"
                  value={openingStock}
                  onChange={(e) => setOpeningStock(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Current Stock</label>
                <input
                  type="number"
                  step="1"
                  value={currentStock}
                  onChange={(e) => setCurrentStock(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Reorder Level</label>
                <input
                  type="number"
                  step="1"
                  value={reorderLevel}
                  onChange={(e) => setReorderLevel(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>

              <div>
                <label className="block text-slate-600 font-medium mb-1">Max Capacity</label>
                <input
                  type="number"
                  step="1"
                  value={maxStockLevel}
                  onChange={(e) => setMaxStockLevel(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono focus:border-[#106ebe] focus:outline-none"
                />
              </div>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex items-center justify-end gap-2 pt-2 border-t border-slate-200">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded border border-slate-300 font-medium"
            >
              Cancel (Esc)
            </button>
            <button
              type="submit"
              className="px-5 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold shadow-xs flex items-center gap-1.5"
            >
              <Check className="w-4 h-4" />
              <span>Save Product (F2)</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
