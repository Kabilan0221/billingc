import React, { useRef, useState } from 'react';
import { X, Printer, Barcode as BarcodeIcon, Check } from 'lucide-react';
import { Product } from '../types';

interface BarcodeDialogProps {
  product: Product | null;
  isOpen: boolean;
  onClose: () => void;
}

export const BarcodeDialog: React.FC<BarcodeDialogProps> = ({
  product,
  isOpen,
  onClose,
}) => {
  const [copies, setCopies] = useState<number>(1);
  const [printedAlert, setPrintedAlert] = useState(false);

  if (!isOpen || !product) return null;

  const handlePrint = () => {
    setPrintedAlert(true);
    setTimeout(() => {
      setPrintedAlert(false);
      onClose();
    }, 1500);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-md overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="bg-gradient-to-r from-[#173e65] to-[#255a90] text-white px-4 py-2.5 flex items-center justify-between select-none">
          <div className="flex items-center gap-2">
            <BarcodeIcon className="w-4 h-4 text-white/80" />
            <h3 className="text-xs font-semibold">Barcode Studio — Thermal Label Printing</h3>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Content */}
        <div className="p-4 space-y-4 text-xs">
          {/* Label Preview (50mm x 25mm thermal format) */}
          <div>
            <span className="font-semibold text-slate-700 block mb-1.5">
              50mm × 25mm Thermal Sticker Preview
            </span>
            <div className="border-2 border-dashed border-slate-300 p-3 rounded bg-slate-50 flex justify-center">
              <div className="w-64 h-32 bg-white border border-slate-400 rounded p-2 flex flex-col justify-between shadow-xs select-none">
                {/* Store Name & Price */}
                <div className="flex justify-between items-start border-b border-slate-200 pb-0.5">
                  <span className="font-bold text-[10px] text-slate-900 uppercase tracking-tight">
                    Sri Murugan Super Market
                  </span>
                  <span className="font-mono font-bold text-xs text-slate-900">
                    ₹{product.saleRate.toFixed(2)}
                  </span>
                </div>

                {/* Product Name En & Ta */}
                <div className="my-0.5">
                  <div className="font-bold text-[11px] text-slate-900 leading-tight truncate">
                    {product.nameEn}
                  </div>
                  {product.nameTa && (
                    <div className="text-[10px] text-slate-700 font-sans leading-tight truncate">
                      {product.nameTa}
                    </div>
                  )}
                </div>

                {/* Barcode Graphic */}
                <div className="flex flex-col items-center">
                  {/* Pseudo Barcode Lines */}
                  <div className="h-9 w-full flex items-center justify-center gap-[1.5px] px-2 overflow-hidden bg-white">
                    {product.barcode.split('').map((char, i) => {
                      const code = char.charCodeAt(0);
                      const widths = [1, 2, 3, 1, 2];
                      const w = widths[code % 5];
                      return (
                        <div
                          key={i}
                          className="bg-black h-full"
                          style={{ width: `${w}px` }}
                        />
                      );
                    })}
                  </div>
                  <div className="font-mono text-[10px] text-slate-800 tracking-widest font-semibold mt-0.5">
                    {product.barcode}
                  </div>
                </div>

                {/* MRP and Tax info */}
                <div className="flex justify-between items-center text-[9px] text-slate-500 border-t border-slate-200 pt-0.5 font-mono">
                  <span>MRP: ₹{product.mrp.toFixed(2)}</span>
                  <span>(Incl. {product.taxRate}% GST)</span>
                </div>
              </div>
            </div>
          </div>

          {/* Print Copies */}
          <div className="flex items-center justify-between border-t border-slate-200 pt-3">
            <label className="font-medium text-slate-700">Number of Sticker Copies:</label>
            <input
              type="number"
              min="1"
              max="1000"
              value={copies}
              onChange={(e) => setCopies(Math.max(1, parseInt(e.target.value) || 1))}
              className="w-20 px-2 py-1 bg-white border border-slate-300 rounded text-center font-mono focus:outline-none focus:border-[#106ebe]"
            />
          </div>

          {printedAlert && (
            <div className="bg-emerald-50 text-emerald-800 border border-emerald-300 p-2 rounded text-xs flex items-center gap-1.5 animate-in fade-in">
              <Check className="w-4 h-4 text-emerald-600" />
              <span>Sent {copies} label(s) to Windows Default Thermal Printer!</span>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="bg-slate-100 border-t border-slate-200 px-4 py-2.5 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="px-3.5 py-1.5 bg-slate-200 hover:bg-slate-300 text-slate-700 rounded font-medium text-xs"
          >
            Cancel
          </button>
          <button
            onClick={handlePrint}
            disabled={printedAlert}
            className="px-4 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold text-xs flex items-center gap-1.5 shadow-xs"
          >
            <Printer className="w-3.5 h-3.5" />
            <span>Print {copies} Label{copies > 1 ? 's' : ''}</span>
          </button>
        </div>
      </div>
    </div>
  );
};
