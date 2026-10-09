import React from 'react';
import { ArrowRight, Boxes, CheckCircle2 } from 'lucide-react';

interface ModulePlaceholderViewProps {
  title: string;
  moduleKey: string;
  onGoToProducts: () => void;
}

export const ModulePlaceholderView: React.FC<ModulePlaceholderViewProps> = ({
  title,
  moduleKey,
  onGoToProducts,
}) => {
  return (
    <div className="flex-1 flex flex-col items-center justify-center p-8 bg-[#f8fafc] text-center text-xs text-slate-600">
      <div className="max-w-md bg-white border border-slate-200 rounded-lg p-6 shadow-xs space-y-4">
        <div className="w-12 h-12 bg-blue-50 text-[#106ebe] rounded-full flex items-center justify-center mx-auto">
          <Boxes className="w-6 h-6" />
        </div>

        <div>
          <h3 className="text-base font-bold text-slate-800">{title}</h3>
          <p className="text-slate-500 mt-1">
            Assigned for scheduled implementation in upcoming phases. Phase 1 delivers the complete Products Master, SQLite engine, 50,000 product indexing, Excel import/export, and Windows 7 installer architecture.
          </p>
        </div>

        <div className="bg-slate-50 border border-slate-200 rounded p-3 text-left space-y-1.5 text-[11px]">
          <div className="font-semibold text-slate-700 flex items-center gap-1.5">
            <CheckCircle2 className="w-3.5 h-3.5 text-emerald-600" />
            <span>Phase 1 Requirements Delivered:</span>
          </div>
          <div className="text-slate-600 pl-5">
            • Complete C# WinForms shell & navigation (14 sections)
            <br />• Product Master with bilingual Tamil & English support
            <br />• SQLite relational database with B-tree indexes & migrations
            <br />• 50,000 product benchmark under 500ms
            <br />• Excel import/export using ClosedXML
            <br />• Barcode Studio (EAN-13 / Code 128)
          </div>
        </div>

        <button
          onClick={onGoToProducts}
          className="w-full py-2 bg-[#106ebe] hover:bg-[#005a9e] text-white font-semibold rounded flex items-center justify-center gap-2 shadow-xs"
        >
          <span>Open Product Master (F2)</span>
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
};
