import React from 'react';
import { Minus, Square, X, Monitor, Code2 } from 'lucide-react';

interface Win7TitleBarProps {
  title: string;
  activeView: 'desktop' | 'solution';
  onToggleView: (view: 'desktop' | 'solution') => void;
  is50kLoaded: boolean;
}

export const Win7TitleBar: React.FC<Win7TitleBarProps> = ({
  title,
  activeView,
  onToggleView,
  is50kLoaded,
}) => {
  return (
    <div className="select-none flex items-center justify-between px-3 py-1.5 bg-gradient-to-b from-[#b8d6f5] via-[#7baee2] to-[#5a98d6] border-b border-[#356ca6] shadow-sm">
      {/* Icon + Title */}
      <div className="flex items-center gap-2">
        <div className="w-4 h-4 rounded bg-[#1e4a7a] flex items-center justify-center text-white text-[10px] font-bold shadow-inner">
          SB
        </div>
        <span className="text-xs font-semibold text-[#0f2d4e] tracking-tight drop-shadow-[0_1px_1px_rgba(255,255,255,0.7)] truncate">
          {title}
        </span>
        {is50kLoaded && (
          <span className="text-[10px] px-1.5 py-0.2 bg-amber-100 text-amber-800 border border-amber-300 rounded font-mono">
            50,000 Catalog Active
          </span>
        )}
      </div>

      {/* Center Switcher: Run App / Inspect C# Solution */}
      <div className="flex items-center gap-1 bg-white/40 p-0.5 rounded border border-white/60 shadow-xs">
        <button
          onClick={() => onToggleView('desktop')}
          className={`flex items-center gap-1 px-2.5 py-0.5 text-xs font-medium rounded transition-all ${
            activeView === 'desktop'
              ? 'bg-[#1b4e82] text-white shadow-xs font-semibold'
              : 'text-[#12365c] hover:bg-white/50'
          }`}
          title="Run Windows 7 WinForms Billing Application"
        >
          <Monitor className="w-3.5 h-3.5" />
          <span>WinForms Shell</span>
        </button>
        <button
          onClick={() => onToggleView('solution')}
          className={`flex items-center gap-1 px-2.5 py-0.5 text-xs font-medium rounded transition-all ${
            activeView === 'solution'
              ? 'bg-[#1b4e82] text-white shadow-xs font-semibold'
              : 'text-[#12365c] hover:bg-white/50'
          }`}
          title="Inspect C# .NET 4.8 Solution, SQLite Scripts & Inno Setup Installer"
        >
          <Code2 className="w-3.5 h-3.5" />
          <span>Visual Studio Solution (C#)</span>
        </button>
      </div>

      {/* Win7 Aero Glass Window Controls */}
      <div className="flex items-center gap-1">
        <button
          className="w-7 h-5 flex items-center justify-center rounded-sm bg-gradient-to-b from-[#e3edf7] to-[#b7d2eb] hover:from-[#f0f6fc] hover:to-[#cbe0f5] border border-[#729bbb] shadow-xs active:shadow-inner text-[#194069]"
          title="Minimize"
        >
          <Minus className="w-3 h-3" />
        </button>
        <button
          className="w-7 h-5 flex items-center justify-center rounded-sm bg-gradient-to-b from-[#e3edf7] to-[#b7d2eb] hover:from-[#f0f6fc] hover:to-[#cbe0f5] border border-[#729bbb] shadow-xs active:shadow-inner text-[#194069]"
          title="Maximize"
        >
          <Square className="w-2.5 h-2.5" />
        </button>
        <button
          className="w-9 h-5 flex items-center justify-center rounded-sm bg-gradient-to-b from-[#e87060] to-[#c73220] hover:from-[#f38677] hover:to-[#db3c28] border border-[#a22718] shadow-xs active:shadow-inner text-white"
          title="Close (Exit)"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>
    </div>
  );
};
