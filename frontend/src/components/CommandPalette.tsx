import React, { useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Search, FileText, Plus, Sparkles, UploadCloud } from 'lucide-react';

interface CommandPaletteProps {
  isOpen: boolean;
  onClose: () => void;
  onSelectAction?: (action: string) => void;
}

export const CommandPalette: React.FC<CommandPaletteProps> = ({ isOpen, onClose, onSelectAction }) => {
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isOpen) {
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    if (isOpen) window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-start justify-center pt-[15vh]">
        <motion.div 
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          onClick={onClose}
          className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        />
        
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: -20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.95, y: -20 }}
          className="relative w-full max-w-xl mx-4 bg-ns-bg-secondary/90 backdrop-blur-xl border border-ns-border/50 shadow-2xl overflow-hidden"
          style={{ borderRadius: '16px' }}
        >
          <div className="flex items-center px-4 py-3 border-b border-ns-border">
            <Search className="w-5 h-5 text-ns-text-muted mr-3" />
            <input 
              ref={inputRef}
              type="text"
              placeholder="Notları, etiketleri ara veya bir komut yaz..."
              className="flex-1 bg-transparent border-none text-ns-text-primary placeholder:text-ns-text-disabled focus:outline-none text-lg"
            />
          </div>

          <div className="max-h-[60vh] overflow-y-auto p-2">
            <div className="px-2 pt-3 pb-2 text-xs font-semibold text-ns-text-muted uppercase tracking-wider">
              Son Notlar
            </div>
            <div className="space-y-1">
              <button className="w-full flex items-center px-3 py-2.5 rounded-lg bg-ns-primary/10 text-ns-primary">
                <FileText className="w-4 h-4 mr-3 opacity-70" />
                <span>Alfa Projesi Yol Haritası</span>
              </button>
              <button className="w-full flex items-center px-3 py-2.5 rounded-lg text-ns-text-primary hover:bg-ns-surface-hover/50 transition-colors">
                <FileText className="w-4 h-4 mr-3 text-ns-text-muted" />
                <span>Toplantı Notları - Ürün</span>
              </button>
              <button className="w-full flex items-center px-3 py-2.5 rounded-lg text-ns-text-primary hover:bg-ns-surface-hover/50 transition-colors">
                <FileText className="w-4 h-4 mr-3 text-ns-text-muted" />
                <span>Mimari Taslağı 2</span>
              </button>
            </div>

            <div className="px-2 pt-5 pb-2 text-xs font-semibold text-ns-text-muted uppercase tracking-wider">
              Eylemler
            </div>
            <div className="space-y-1 pb-2">
              <button 
                onClick={() => { onSelectAction?.('create'); onClose(); }}
                className="w-full flex items-center px-3 py-2.5 rounded-lg text-ns-text-primary hover:bg-ns-surface-hover/50 transition-colors"
              >
                <Plus className="w-4 h-4 mr-3 text-ns-text-muted" />
                <span>Yeni not oluştur</span>
              </button>
              <button className="w-full flex items-center px-3 py-2.5 rounded-lg text-ns-text-primary hover:bg-ns-surface-hover/50 transition-colors">
                <Sparkles className="w-4 h-4 mr-3 text-ns-primary" />
                <span>Tüm notlar hakkında yapay zekaya sor</span>
              </button>
              <button 
                onClick={() => { onSelectAction?.('upload'); onClose(); }}
                className="w-full flex items-center px-3 py-2.5 rounded-lg text-ns-text-primary hover:bg-ns-surface-hover/50 transition-colors"
              >
                <UploadCloud className="w-4 h-4 mr-3 text-ns-text-muted" />
                <span>PDF Yükle</span>
              </button>
            </div>
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
};
