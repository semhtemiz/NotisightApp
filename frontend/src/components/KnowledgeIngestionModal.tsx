import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { CloudUpload, X, File, FileText, Loader2 } from 'lucide-react';
import type { Note } from '../types';
import { apiClient } from '../utils/apiClient';
import { buildApiUrl } from '../utils/apiConfig';

interface KnowledgeIngestionModalProps {
  isOpen: boolean;
  onClose: () => void;
  onNoteCreated?: (note: Note) => void;
  targetFolderId?: string | null;
}

export const KnowledgeIngestionModal: React.FC<KnowledgeIngestionModalProps> = ({ isOpen, onClose, onNoteCreated, targetFolderId }) => {
  const [activeTab, setActiveTab] = useState<'document' | 'audio'>('document');
  const [isUploading, setIsUploading] = useState(false);

  if (!isOpen) return null;

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      
      setIsUploading(true);
      try {
        const formData = new FormData();
        formData.append('file', file);
        if (targetFolderId) {
          formData.append('folderId', targetFolderId);
        }
        
        const endpoint = file.name.endsWith('.pdf') ? '/notes/upload-pdf' : '/notes/upload-audio';

        const response = await apiClient.fetchWithAuth(endpoint, {
          method: 'POST',
          body: formData
        });

        if (response.ok) {
          const data = await response.json();
          
          try {
            const noteRes = await apiClient.fetchWithAuth(`/notes/${data.noteId || data.id}`);
            if (noteRes.ok) {
              const realNote = await noteRes.json();
              if (onNoteCreated) {
                onNoteCreated({
                  ...realNote,
                  tags: realNote.tags ? realNote.tags.map((t: any) => t.name) : [],
                  tagIds: realNote.tags ? realNote.tags.map((t: any) => t.id) : [],
                  fileUrl: realNote.fileUrl ? buildApiUrl(`/notes/${realNote.id}/file`) : undefined
                });
              }
              onClose();
              return;
            }
          } catch (e) {
            console.error("Could not fetch full note after upload", e);
          }

          if (onNoteCreated) {
             onNoteCreated({
               id: data.noteId || data.id,
               title: data.title,
               content: file.name.endsWith('.pdf') 
                 ? 'Belge başarıyla yüklendi. Yapay Zeka ile bu belge üzerinde sorular sorabilirsiniz.'
                 : 'Ses dosyası döküme çevrildi.',
               tags: [file.name.endsWith('.pdf') ? 'PDF' : 'Sesli'],
               tagIds: [],
               folderId: targetFolderId || undefined,
                fileUrl: data.fileUrl ? buildApiUrl(`/notes/${data.noteId || data.id}/file`) : undefined,
               fileType: data.fileType,
               vectorSyncStatus: data.vectorSyncStatus ?? 'pending',
               vectorSyncError: data.vectorSyncError,
               vectorSyncedAtUtc: data.vectorSyncedAtUtc
             });
          }
          onClose();
        } else {
          const errorData = await response.json().catch(() => ({}));
          const errorMessage = errorData.detail || errorData.title || errorData.message || response.statusText;
          alert(`Yükleme başarısız oldu. Hata: ${errorMessage}`);
        }
      } catch(err) {
        console.error(err);
        alert('Dosya yüklenirken bir bağlantı hatası oluştu.');
      } finally {
        setIsUploading(false);
      }
    }
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
        <motion.div 
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          onClick={onClose}
          className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        />
        
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.95 }}
          className="relative w-full max-w-md bg-ns-bg-secondary border border-ns-primary/30 shadow-[0_0_40px_-10px_rgba(34,197,94,0.3)] rounded-2xl overflow-hidden p-6"
        >
          <button 
            onClick={onClose}
            className="absolute top-4 right-4 text-ns-text-muted hover:text-ns-text-primary transition-colors disabled:opacity-50"
            disabled={isUploading}
          >
            <X className="w-5 h-5" />
          </button>

          <div className="flex justify-center mb-8 pt-2">
            <div className="flex bg-ns-bg-primary/50 rounded-lg p-1 border border-ns-border/50">
              <button 
                onClick={() => setActiveTab('document')}
                disabled={isUploading}
                className={`px-4 py-1.5 rounded-md text-sm font-medium transition-all ${
                  activeTab === 'document' ? 'bg-ns-bg-tertiary text-ns-text-primary shadow-sm' : 'text-ns-text-secondary hover:text-ns-text-primary'
                } disabled:opacity-50`}
              >
                Belge
              </button>
              <button 
                onClick={() => setActiveTab('audio')}
                disabled={isUploading}
                className={`px-4 py-1.5 rounded-md text-sm font-medium transition-all ${
                  activeTab === 'audio' ? 'bg-ns-bg-tertiary text-ns-text-primary shadow-sm' : 'text-ns-text-secondary hover:text-ns-text-primary'
                } disabled:opacity-50`}
              >
                Ses
              </button>
            </div>
          </div>

          <div 
            onClick={() => !isUploading && document.getElementById('file-upload')?.click()}
            className={`border-2 border-dashed border-ns-border hover:border-ns-primary/50 transition-colors rounded-xl bg-ns-bg-primary/30 flex flex-col items-center justify-center py-12 px-6 text-center mb-6 group ${isUploading ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
          >
            <input 
              id="file-upload" 
              type="file" 
              className="hidden" 
              accept={activeTab === 'document' ? ".pdf" : ".wav,.webm,.mp3,.m4a"}
              onChange={handleFileChange}
              disabled={isUploading}
            />
            {isUploading ? (
              <Loader2 className="w-10 h-10 text-ns-primary mb-4 animate-spin" />
            ) : (
              <CloudUpload className="w-10 h-10 text-ns-primary mb-4 group-hover:scale-110 transition-transform duration-300" />
            )}
            <p className="text-ns-text-primary font-medium mb-1">
              {isUploading 
                ? 'Dosyanız yükleniyor, lütfen bekleyin...' 
                : activeTab === 'document' 
                  ? "PDF belgesini buraya sürükleyip bırakın veya göz atın" 
                  : "WAV, WEBM, MP3 veya M4A ses dosyasını sürükleyip bırakın"}
            </p>
            <p className="text-ns-text-muted text-sm">
              {!isUploading && (activeTab === 'document' ? "Maksimum 20MB'a kadar PDF destekler" : "WAV, WEBM, MP3, M4A destekler")}
            </p>
          </div>

        </motion.div>
      </div>
    </AnimatePresence>
  );
};
