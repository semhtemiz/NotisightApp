import React from 'react';
import { motion } from 'motion/react';
import { AlertTriangle } from 'lucide-react';

interface DeleteConfirmModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  itemName: string;
  itemType: 'note' | 'folder' | 'chat';
}

const titleByType = {
  note: 'Notu Sil',
  folder: 'Klasörü Sil',
  chat: 'Sohbeti Sil'
};

const labelByType = {
  note: 'notu',
  folder: 'klasörü',
  chat: 'sohbeti'
};

export const DeleteConfirmModal: React.FC<DeleteConfirmModalProps> = ({ isOpen, onClose, onConfirm, itemName, itemType }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[200] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="bg-ns-bg-secondary border border-ns-border rounded-2xl w-full max-w-sm max-h-[calc(100dvh-2rem)] overflow-y-auto shadow-2xl p-5 sm:p-6"
      >
        <div className="flex flex-col items-center text-center gap-4">
          <div className="w-12 h-12 rounded-full bg-ns-error/20 text-ns-error flex items-center justify-center border border-ns-error/30">
            <AlertTriangle className="w-6 h-6" />
          </div>
          <h2 className="text-lg font-semibold text-ns-text-primary">
            {titleByType[itemType]}
          </h2>
          <p className="text-ns-text-secondary text-sm">
            <strong className="text-ns-text-primary">{itemName}</strong> adlı {labelByType[itemType]} silmek istediğinize emin misiniz? Bu işlem geri alınamaz.
          </p>
          <div className="flex w-full gap-3 mt-4">
            <button
              onClick={onClose}
              className="flex-1 bg-ns-bg-primary hover:bg-ns-surface-hover text-ns-text-primary font-medium py-2.5 rounded-lg transition-colors border border-ns-border"
            >
              İptal
            </button>
            <button
              onClick={() => {
                onConfirm();
                onClose();
              }}
              className="flex-1 bg-ns-error hover:bg-red-500 text-white font-medium py-2.5 rounded-lg transition-colors"
            >
              Sil
            </button>
          </div>
        </div>
      </motion.div>
    </div>
  );
};
