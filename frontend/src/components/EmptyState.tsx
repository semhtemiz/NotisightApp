import React from 'react';
import { motion } from 'motion/react';
import { Plus, FileText, Mic } from 'lucide-react';
import emptyStateLogoUrl from '../assets/emptystate_logo.svg';

interface EmptyStateProps {
  onCreateNote: () => void;
  onUpload: () => void;
  onRecordVoiceNote: () => void;
}

/**
 * Boş Durum (EmptyState) Bileşeni
 * Herhangi bir not seçili olmadığında editörün (orta alanın) yerini alır. 
 * Kullanıcıya "Yeni Not", "Belge Yükle" veya "Ses Kaydet" gibi hızlı eylem başlangıçları sunar.
 */
export const EmptyState: React.FC<EmptyStateProps> = ({ onCreateNote, onUpload, onRecordVoiceNote }) => {
  return (
    <main className="relative flex-1 flex flex-col bg-ns-bg-primary h-full items-center justify-center overflow-hidden p-6 text-center text-ns-text-primary">
      <div className="pointer-events-none absolute inset-x-[18%] top-[18%] h-56 rounded-full bg-ns-primary/8 blur-3xl" />
      <div className="pointer-events-none absolute bottom-[18%] left-[30%] h-40 w-40 rounded-full bg-ns-green-glow/8 blur-3xl" />

      <motion.div
        initial={{ opacity: 0, scale: 0.9 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ duration: 0.5 }}
        className="relative z-10 flex w-full max-w-2xl flex-col items-center"
      >
        <div className="relative mb-7">
          <div className="absolute inset-[-18px] rounded-full bg-ns-primary/12 blur-2xl" />
          <div className="relative flex h-32 w-32 items-center justify-center">
            <img src={emptyStateLogoUrl} alt="" aria-hidden="true" className="h-28 w-28 object-contain" />
          </div>
        </div>

        <div className="mb-10 max-w-xl">
          <p className="mb-3 text-xs font-semibold uppercase tracking-[0.24em] text-ns-primary/90">Notisight Workspace</p>
          <h2 className="text-3xl font-semibold tracking-normal text-ns-text-primary sm:text-4xl">Senin verin, senin zekan.</h2>
          <p className="mt-3 text-sm leading-6 text-ns-text-secondary sm:text-base">
            Başlamak için bir not seçin veya yeni bir düşünce, belge veya ses kaydı ekleyin.
          </p>
        </div>

        <div className="grid w-full grid-cols-1 gap-4 sm:grid-cols-3">
          {[
            { icon: Plus, label: 'Boş Not Oluştur', onClick: onCreateNote },
            { icon: FileText, label: 'Belge / Ses Yükle', onClick: onUpload },
            { icon: Mic, label: 'Sesli Not Kaydet', onClick: onRecordVoiceNote },
          ].map((action, i) => (
            <motion.button
              key={action.label}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 + i * 0.1 }}
              onClick={action.onClick}
              className="group grid min-h-40 grid-rows-[3.25rem_2.75rem] place-items-center rounded-3xl border border-ns-border/55 bg-ns-bg-secondary/38 p-6 text-ns-text-primary shadow-[0_18px_48px_-38px_rgba(0,0,0,0.85)] backdrop-blur-xl transition-all hover:-translate-y-0.5 hover:border-ns-primary/35 hover:bg-ns-surface-hover/42 hover:shadow-[0_22px_56px_-36px_rgba(34,197,94,0.28)]"
            >
              <div className="flex h-12 w-12 items-center justify-center rounded-full border border-ns-primary/22 bg-ns-primary/8 transition-colors group-hover:bg-ns-primary/14">
                <action.icon className="h-5 w-5 text-ns-primary" />
              </div>
              <span className="flex min-h-10 items-center text-center text-sm font-semibold leading-5">{action.label}</span>
            </motion.button>
          ))}
        </div>
      </motion.div>
    </main>
  );
};
