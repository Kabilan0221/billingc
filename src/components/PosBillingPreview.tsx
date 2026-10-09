import React, { useState } from 'react';
import { ShoppingCart, Search, Plus, Trash2, Printer, CreditCard, Check, ArrowRight } from 'lucide-react';
import { db } from '../services/dbEngine';
import { Product } from '../types';

interface CartItem {
  product: Product;
  quantity: number;
}

export const PosBillingPreview: React.FC<{ onNavigateToProducts: () => void }> = ({
  onNavigateToProducts,
}) => {
  const [scanInput, setScanInput] = useState('');
  const [cart, setCart] = useState<CartItem[]>([
    { product: db.getAllProducts()[0] || ({} as any), quantity: 2 },
    { product: db.getAllProducts()[2] || ({} as any), quantity: 1 },
  ]);
  const [completedMessage, setCompletedMessage] = useState<string | null>(null);

  const handleScan = (e: React.FormEvent) => {
    e.preventDefault();
    if (!scanInput.trim()) return;

    const found = db.getProductByBarcode(scanInput.trim()) || db.getAllProducts().find(
      (p) => p.productCode.toLowerCase() === scanInput.trim().toLowerCase()
    );

    if (found) {
      setCart((prev) => {
        const existing = prev.find((item) => item.product.id === found.id);
        if (existing) {
          return prev.map((item) =>
            item.product.id === found.id ? { ...item, quantity: item.quantity + 1 } : item
          );
        }
        return [...prev, { product: found, quantity: 1 }];
      });
      setScanInput('');
    } else {
      alert(`Product with barcode or code "${scanInput}" not found in catalog.`);
    }
  };

  const subtotal = cart.reduce((sum, item) => sum + (item.product.saleRate || 0) * item.quantity, 0);
  const tax = cart.reduce(
    (sum, item) => sum + ((item.product.saleRate || 0) * item.quantity * (item.product.taxRate || 0)) / 100,
    0
  );
  const total = subtotal + tax;

  const handleCheckout = () => {
    setCompletedMessage(`Invoice #${Math.floor(100000 + Math.random() * 900000)} generated! ₹${total.toFixed(2)} paid.`);
    setTimeout(() => {
      setCart([]);
      setCompletedMessage(null);
    }, 2500);
  };

  return (
    <div className="flex-1 flex flex-col md:flex-row h-full bg-[#f8fafc] overflow-hidden text-xs text-slate-800">
      {/* Left: POS Bill Cart */}
      <div className="flex-1 flex flex-col h-full border-r border-slate-200 bg-white">
        {/* Scan Barcode Bar */}
        <div className="p-3 bg-slate-100 border-b border-slate-200">
          <form onSubmit={handleScan} className="flex gap-2">
            <div className="relative flex-1">
              <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-slate-400" />
              <input
                type="text"
                value={scanInput}
                onChange={(e) => setScanInput(e.target.value)}
                placeholder="Scan Barcode (USB Scanner) or enter Product Code..."
                className="w-full pl-8 pr-3 py-1.5 bg-white border border-slate-300 rounded font-mono text-xs focus:border-[#106ebe] focus:outline-none"
                autoFocus
              />
            </div>
            <button
              type="submit"
              className="px-4 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold shrink-0"
            >
              Add Item
            </button>
          </form>
        </div>

        {/* Cart Grid */}
        <div className="flex-1 overflow-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-[#1f4e79] text-white sticky top-0 font-semibold">
              <tr>
                <th className="p-2 w-10 text-center">#</th>
                <th className="p-2">Item Description</th>
                <th className="p-2 text-right w-20">Rate ₹</th>
                <th className="p-2 text-center w-20">Qty</th>
                <th className="p-2 text-right w-20">Amount ₹</th>
                <th className="p-2 w-10 text-center"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {cart.map((item, idx) => (
                <tr key={idx} className="hover:bg-slate-50">
                  <td className="p-2 text-center text-slate-400">{idx + 1}</td>
                  <td className="p-2">
                    <div className="font-semibold text-slate-900">{item.product.nameEn}</div>
                    {item.product.nameTa && <div className="text-[10px] text-slate-500 font-sans">{item.product.nameTa}</div>}
                    <div className="text-[10px] text-slate-400 font-mono">Barcode: {item.product.barcode}</div>
                  </td>
                  <td className="p-2 text-right font-mono tabular-nums">₹{item.product.saleRate?.toFixed(2)}</td>
                  <td className="p-2 text-center">
                    <input
                      type="number"
                      min="1"
                      value={item.quantity}
                      onChange={(e) => {
                        const val = parseInt(e.target.value) || 1;
                        setCart((prev) =>
                          prev.map((c, i) => (i === idx ? { ...c, quantity: val } : c))
                        );
                      }}
                      className="w-12 py-0.5 px-1 border border-slate-300 rounded text-center font-mono"
                    />
                  </td>
                  <td className="p-2 text-right font-mono font-bold tabular-nums">
                    ₹{((item.product.saleRate || 0) * item.quantity).toFixed(2)}
                  </td>
                  <td className="p-2 text-center">
                    <button
                      onClick={() => setCart((prev) => prev.filter((_, i) => i !== idx))}
                      className="text-slate-400 hover:text-rose-600"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Right: Payment & Summary Panel */}
      <div className="w-full md:w-72 bg-slate-50 p-4 flex flex-col justify-between border-t md:border-t-0 md:border-l border-slate-200">
        <div className="space-y-4">
          <div className="font-bold text-slate-800 text-sm border-b border-slate-200 pb-2">
            Billing Summary (POS F1)
          </div>

          <div className="space-y-2 text-xs">
            <div className="flex justify-between text-slate-600">
              <span>Items Count:</span>
              <span className="font-mono">{cart.length} lines</span>
            </div>
            <div className="flex justify-between text-slate-600">
              <span>Subtotal:</span>
              <span className="font-mono">₹{subtotal.toFixed(2)}</span>
            </div>
            <div className="flex justify-between text-slate-600">
              <span>GST Tax:</span>
              <span className="font-mono">₹{tax.toFixed(2)}</span>
            </div>
            <div className="flex justify-between font-bold text-sm text-slate-900 pt-2 border-t border-slate-200">
              <span>Net Payable:</span>
              <span className="font-mono text-base text-[#106ebe]">₹{total.toFixed(2)}</span>
            </div>
          </div>

          {completedMessage && (
            <div className="bg-emerald-50 text-emerald-800 border border-emerald-300 p-2.5 rounded font-medium flex items-center gap-2">
              <Check className="w-4 h-4 text-emerald-600 shrink-0" />
              <span>{completedMessage}</span>
            </div>
          )}
        </div>

        <div className="space-y-2 pt-4">
          <button
            onClick={handleCheckout}
            disabled={cart.length === 0}
            className="w-full py-2.5 bg-[#106ebe] hover:bg-[#005a9e] disabled:opacity-50 text-white rounded font-bold shadow-xs flex items-center justify-center gap-2"
          >
            <CreditCard className="w-4 h-4" />
            <span>Complete Bill & Print (F1)</span>
          </button>

          <button
            onClick={onNavigateToProducts}
            className="w-full py-1.5 text-slate-600 hover:text-slate-900 flex items-center justify-center gap-1 font-medium"
          >
            <span>Manage Products Master (F2)</span>
            <ArrowRight className="w-3 h-3" />
          </button>
        </div>
      </div>
    </div>
  );
};
