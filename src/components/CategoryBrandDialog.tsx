import React, { useState } from 'react';
import { X, Plus, Trash2, FolderPlus } from 'lucide-react';
import { Category, Subcategory, Brand } from '../types';
import { db } from '../services/dbEngine';

interface CategoryBrandDialogProps {
  initialTab?: 'categories' | 'brands';
  isOpen: boolean;
  onClose: () => void;
  onUpdated: () => void;
}

export const CategoryBrandDialog: React.FC<CategoryBrandDialogProps> = ({
  initialTab = 'categories',
  isOpen,
  onClose,
  onUpdated,
}) => {
  const [activeTab, setActiveTab] = useState<'categories' | 'brands'>(initialTab);

  // Category state
  const [selectedCatId, setSelectedCatId] = useState<number | null>(null);
  const [newCatName, setNewCatName] = useState('');
  const [newCatCode, setNewCatCode] = useState('');

  // Subcategory state
  const [newSubName, setNewSubName] = useState('');
  const [newSubCode, setNewSubCode] = useState('');

  // Brand state
  const [newBrandName, setNewBrandName] = useState('');
  const [newBrandCode, setNewBrandCode] = useState('');

  const categories = db.getCategories();
  const currentSubcategories = selectedCatId ? db.getSubcategories(selectedCatId) : [];
  const brands = db.getBrands();

  const handleAddCategory = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newCatName.trim()) return;
    const cat = db.addCategory(newCatName.trim(), newCatCode.trim());
    setNewCatName('');
    setNewCatCode('');
    setSelectedCatId(cat.id);
    onUpdated();
  };

  const handleAddSubcategory = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCatId || !newSubName.trim()) return;
    db.addSubcategory(selectedCatId, newSubName.trim(), newSubCode.trim());
    setNewSubName('');
    setNewSubCode('');
    onUpdated();
  };

  const handleAddBrand = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newBrandName.trim()) return;
    db.addBrand(newBrandName.trim(), newBrandCode.trim());
    setNewBrandName('');
    setNewBrandCode('');
    onUpdated();
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-xs p-4">
      <div className="bg-white rounded-lg shadow-2xl border border-slate-300 w-full max-w-2xl overflow-hidden flex flex-col max-h-[85vh]">
        {/* Header */}
        <div className="bg-gradient-to-r from-[#173e65] to-[#255a90] text-white px-4 py-2.5 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FolderPlus className="w-4 h-4 text-white/80" />
            <h3 className="text-xs font-semibold">Hierarchy Masters — Categories & Brands</h3>
          </div>
          <button onClick={onClose} className="text-white/80 hover:text-white">
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Tab Strip */}
        <div className="flex border-b border-slate-200 bg-slate-100 text-xs font-medium px-3 pt-2 gap-1">
          <button
            onClick={() => setActiveTab('categories')}
            className={`px-3 py-1.5 rounded-t transition-colors ${
              activeTab === 'categories'
                ? 'bg-white text-[#106ebe] font-semibold border-t-2 border-[#106ebe]'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Categories & Subcategories
          </button>
          <button
            onClick={() => setActiveTab('brands')}
            className={`px-3 py-1.5 rounded-t transition-colors ${
              activeTab === 'brands'
                ? 'bg-white text-[#106ebe] font-semibold border-t-2 border-[#106ebe]'
                : 'text-slate-600 hover:text-slate-900'
            }`}
          >
            Brands Master
          </button>
        </div>

        {/* Body */}
        <div className="p-4 overflow-y-auto flex-1 text-xs">
          {activeTab === 'categories' ? (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Category Column */}
              <div className="border border-slate-200 rounded p-3 bg-slate-50/50 flex flex-col h-[380px]">
                <h4 className="font-semibold text-slate-700 mb-2">Categories</h4>
                <div className="flex-1 overflow-y-auto bg-white border border-slate-200 rounded divide-y divide-slate-100">
                  {categories.map((c) => (
                    <div
                      key={c.id}
                      onClick={() => setSelectedCatId(c.id)}
                      className={`px-2.5 py-1.5 cursor-pointer flex items-center justify-between transition-colors ${
                        selectedCatId === c.id ? 'bg-[#cce8ff] text-[#004a80] font-semibold' : 'hover:bg-slate-50'
                      }`}
                    >
                      <span>{c.name}</span>
                      <span className="font-mono text-[10px] text-slate-400">{c.code}</span>
                    </div>
                  ))}
                </div>

                <form onSubmit={handleAddCategory} className="mt-3 pt-2 border-t border-slate-200 space-y-2">
                  <input
                    type="text"
                    required
                    value={newCatName}
                    onChange={(e) => setNewCatName(e.target.value)}
                    placeholder="New category name..."
                    className="w-full px-2 py-1 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                  />
                  <div className="flex gap-2">
                    <input
                      type="text"
                      value={newCatCode}
                      onChange={(e) => setNewCatCode(e.target.value)}
                      placeholder="Code (optional)"
                      className="w-1/2 px-2 py-1 bg-white border border-slate-300 rounded font-mono"
                    />
                    <button
                      type="submit"
                      className="w-1/2 px-2 py-1 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold flex items-center justify-center gap-1"
                    >
                      <Plus className="w-3.5 h-3.5" />
                      <span>Add Category</span>
                    </button>
                  </div>
                </form>
              </div>

              {/* Subcategories Column */}
              <div className="border border-slate-200 rounded p-3 bg-slate-50/50 flex flex-col h-[380px]">
                <h4 className="font-semibold text-slate-700 mb-2">
                  Subcategories {selectedCatId ? `(for selected)` : `(select category)`}
                </h4>
                <div className="flex-1 overflow-y-auto bg-white border border-slate-200 rounded divide-y divide-slate-100">
                  {currentSubcategories.length === 0 ? (
                    <div className="p-4 text-center text-slate-400">
                      {selectedCatId ? 'No subcategories yet.' : 'Select a category on the left.'}
                    </div>
                  ) : (
                    currentSubcategories.map((s) => (
                      <div key={s.id} className="px-2.5 py-1.5 flex items-center justify-between">
                        <span>{s.name}</span>
                        <span className="font-mono text-[10px] text-slate-400">{s.code}</span>
                      </div>
                    ))
                  )}
                </div>

                <form onSubmit={handleAddSubcategory} className="mt-3 pt-2 border-t border-slate-200 space-y-2">
                  <input
                    type="text"
                    required
                    disabled={!selectedCatId}
                    value={newSubName}
                    onChange={(e) => setNewSubName(e.target.value)}
                    placeholder="New subcategory name..."
                    className="w-full px-2 py-1 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none disabled:opacity-50"
                  />
                  <div className="flex gap-2">
                    <input
                      type="text"
                      disabled={!selectedCatId}
                      value={newSubCode}
                      onChange={(e) => setNewSubCode(e.target.value)}
                      placeholder="Code (optional)"
                      className="w-1/2 px-2 py-1 bg-white border border-slate-300 rounded font-mono disabled:opacity-50"
                    />
                    <button
                      type="submit"
                      disabled={!selectedCatId}
                      className="w-1/2 px-2 py-1 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold disabled:opacity-50 flex items-center justify-center gap-1"
                    >
                      <Plus className="w-3.5 h-3.5" />
                      <span>Add Subcat</span>
                    </button>
                  </div>
                </form>
              </div>
            </div>
          ) : (
            /* Brands Tab */
            <div className="border border-slate-200 rounded p-4 bg-slate-50/50 flex flex-col h-[380px]">
              <h4 className="font-semibold text-slate-700 mb-2">Registered Brands</h4>
              <div className="flex-1 overflow-y-auto bg-white border border-slate-200 rounded divide-y divide-slate-100">
                {brands.map((b) => (
                  <div key={b.id} className="px-3 py-2 flex items-center justify-between hover:bg-slate-50">
                    <span className="font-medium text-slate-800">{b.name}</span>
                    <span className="font-mono text-[11px] text-slate-400">{b.code}</span>
                  </div>
                ))}
              </div>

              <form onSubmit={handleAddBrand} className="mt-4 pt-3 border-t border-slate-200 flex gap-2">
                <input
                  type="text"
                  required
                  value={newBrandName}
                  onChange={(e) => setNewBrandName(e.target.value)}
                  placeholder="Brand name..."
                  className="flex-1 px-2.5 py-1.5 bg-white border border-slate-300 rounded focus:border-[#106ebe] focus:outline-none"
                />
                <input
                  type="text"
                  value={newBrandCode}
                  onChange={(e) => setNewBrandCode(e.target.value)}
                  placeholder="Code (e.g. BR-NESTLE)"
                  className="w-40 px-2.5 py-1.5 bg-white border border-slate-300 rounded font-mono"
                />
                <button
                  type="submit"
                  className="px-4 py-1.5 bg-[#106ebe] hover:bg-[#005a9e] text-white rounded font-semibold flex items-center gap-1"
                >
                  <Plus className="w-3.5 h-3.5" />
                  <span>Add Brand</span>
                </button>
              </form>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="bg-slate-100 border-t border-slate-200 px-4 py-2 flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-1.5 bg-slate-200 hover:bg-slate-300 text-slate-700 rounded font-medium text-xs"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
};
