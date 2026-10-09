import React from 'react';
import {
  LayoutDashboard,
  ShoppingCart,
  Boxes,
  Truck,
  Barcode,
  CreditCard,
  FileClock,
  Users,
  Layers,
  FileSpreadsheet,
  BarChart3,
  HardDriveDownload,
  Database,
  Settings,
  Building,
} from 'lucide-react';

export interface NavItem {
  id: number;
  key: string;
  label: string;
  shortcut?: string;
  icon: React.ComponentType<{ className?: string }>;
  isPhase1Active: boolean;
}

export const SIDEBAR_ITEMS: NavItem[] = [
  { id: 1, key: 'dashboard', label: 'Dashboard', icon: LayoutDashboard, isPhase1Active: false },
  { id: 2, key: 'pos', label: 'POS Billing', shortcut: 'F1', icon: ShoppingCart, isPhase1Active: false },
  { id: 3, key: 'products', label: 'Products', shortcut: 'F2', icon: Boxes, isPhase1Active: true },
  { id: 4, key: 'inbound', label: 'Inbound Goods', shortcut: 'F3', icon: Truck, isPhase1Active: true },
  { id: 5, key: 'barcode', label: 'Barcode Studio', shortcut: 'F4', icon: Barcode, isPhase1Active: true },
  { id: 6, key: 'payments', label: 'Payments', shortcut: 'F5', icon: CreditCard, isPhase1Active: false },
  { id: 7, key: 'history', label: 'Bill History', shortcut: 'F6', icon: FileClock, isPhase1Active: false },
  { id: 8, key: 'suppliers', label: 'Suppliers', shortcut: 'F7', icon: Building, isPhase1Active: true },
  { id: 9, key: 'stock', label: 'Stock', shortcut: 'F8', icon: Layers, isPhase1Active: true },
  { id: 10, key: 'invoices', label: 'Estimates & Invoices', shortcut: 'F9', icon: FileSpreadsheet, isPhase1Active: false },
  { id: 11, key: 'reports', label: 'Reports', shortcut: 'F10', icon: BarChart3, isPhase1Active: false },
  { id: 12, key: 'backup', label: 'Backup & Restore', icon: HardDriveDownload, isPhase1Active: true },
  { id: 13, key: 'data', label: 'Data Management', icon: Database, isPhase1Active: true },
  { id: 14, key: 'settings', label: 'Settings', icon: Settings, isPhase1Active: false },
];

interface Win7SidebarProps {
  activeNav: string;
  onSelectNav: (key: string) => void;
  productCount: number;
  lowStockCount: number;
}

export const Win7Sidebar: React.FC<Win7SidebarProps> = ({
  activeNav,
  onSelectNav,
  productCount,
  lowStockCount,
}) => {
  return (
    <aside className="w-56 shrink-0 bg-[#212b36] border-r border-[#19212a] flex flex-col justify-between text-slate-200 select-none">
      {/* Menu List */}
      <div className="py-2 overflow-y-auto max-h-[calc(100vh-140px)] custom-scrollbar">
        <div className="px-3 pb-2 text-[10px] font-bold uppercase tracking-wider text-slate-400">
          Navigation Modules
        </div>
        <nav className="space-y-0.5 px-1.5">
          {SIDEBAR_ITEMS.map((item) => {
            const Icon = item.icon;
            const isActive = activeNav === item.key;
            return (
              <button
                key={item.key}
                onClick={() => onSelectNav(item.key)}
                className={`w-full flex items-center justify-between px-2.5 py-1.5 rounded text-xs transition-colors text-left group ${
                  isActive
                    ? 'bg-[#106ebe] text-white font-semibold shadow-xs'
                    : 'text-slate-300 hover:bg-[#304050] hover:text-white'
                }`}
              >
                <div className="flex items-center gap-2 truncate">
                  <Icon className={`w-4 h-4 shrink-0 ${isActive ? 'text-white' : 'text-slate-400 group-hover:text-slate-200'}`} />
                  <span className="truncate">{item.label}</span>
                </div>
                {item.shortcut && (
                  <span
                    className={`text-[10px] px-1 py-0.2 rounded font-mono shrink-0 ml-1 ${
                      isActive ? 'bg-white/20 text-white' : 'bg-slate-700/60 text-slate-400'
                    }`}
                  >
                    {item.shortcut}
                  </span>
                )}
              </button>
            );
          })}
        </nav>
      </div>

      {/* Terminal status pill at bottom */}
      <div className="p-2.5 bg-[#171f28] border-t border-[#101720] text-[11px] text-slate-400 space-y-1">
        <div className="flex justify-between items-center">
          <span>Active Catalog:</span>
          <span className="font-semibold text-slate-200 font-mono">{productCount.toLocaleString()}</span>
        </div>
        {lowStockCount > 0 && (
          <div className="flex justify-between items-center text-rose-400">
            <span>Low Stock Items:</span>
            <span className="font-semibold font-mono">{lowStockCount.toLocaleString()}</span>
          </div>
        )}
        <div className="text-[10px] text-slate-400 pt-1 border-t border-slate-700/50">
          SQLite 3.39 · WAL Mode · Win 7 SP1
        </div>
      </div>
    </aside>
  );
};
