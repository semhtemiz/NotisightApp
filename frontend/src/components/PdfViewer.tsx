import React, { useState, useEffect } from 'react';
import type { Note } from '../types';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { ZoomIn, ZoomOut, Loader2, BookOpen, Edit2, Check, X } from 'lucide-react';
import { apiClient } from '../utils/apiClient';

// Setup worker
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

interface PdfViewerProps {
  note: Note;
  folderPathStr?: string;
  onUpdate?: (id: string, updates: Partial<Note>) => void;
}

export const PdfViewer: React.FC<PdfViewerProps> = ({ note, folderPathStr, onUpdate }) => {
  const [numPages, setNumPages] = useState<number>();
  const [scale, setScale] = useState<number>(1.0);
  const [pdfData, setPdfData] = useState<Blob | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditingTitle, setIsEditingTitle] = useState(false);
  const [draftTitle, setDraftTitle] = useState(note.title || '');

  useEffect(() => {
    setDraftTitle(note.title || '');
  }, [note.title]);

  useEffect(() => {
    let isMounted = true;
    const fetchPdf = async () => {
      if (!note.fileUrl) {
        setLoading(false);
        return;
      }
      
      try {
        setLoading(true);
        const res = await apiClient.fetchWithAuth(note.fileUrl);
        
        if (!res.ok) throw new Error('PDF yüklenirken hata oluştu.');
        
        const blob = await res.blob();
        if (isMounted) {
          setPdfData(blob);
          setError(null);
        }
      } catch (err: any) {
        if (isMounted) setError(err.message);
      } finally {
        if (isMounted) setLoading(false);
      }
    };
    fetchPdf();
    
    return () => { isMounted = false; };
  }, [note.fileUrl]);

  function onDocumentLoadSuccess({ numPages }: { numPages: number }): void {
    setNumPages(numPages);
  }

  const saveTitle = () => {
    const nextTitle = draftTitle.trim();
    if (!nextTitle) {
      setDraftTitle(note.title || '');
      setIsEditingTitle(false);
      return;
    }

    if (nextTitle !== note.title) {
      onUpdate?.(note.id, { title: nextTitle });
    }
    setIsEditingTitle(false);
  };

  const cancelTitleEdit = () => {
    setDraftTitle(note.title || '');
    setIsEditingTitle(false);
  };

  const handleTitleKeyDown = (event: React.KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      saveTitle();
    }
    if (event.key === 'Escape') {
      cancelTitleEdit();
    }
  };

  return (
    <div className="flex-1 flex min-h-0 flex-col overflow-hidden bg-ns-bg-primary h-full relative">
      <header className="h-12 border-b border-ns-border flex items-center px-4 md:px-8 gap-4 justify-between shrink-0 bg-ns-bg-primary/95 backdrop-blur-sm z-10">
        <div className="flex min-w-0 flex-1 items-center gap-2 overflow-hidden text-ns-text-muted text-xs">
          <span className="truncate">{folderPathStr || 'çalışma alanı'}</span>
          <span className="shrink-0">/</span>
          <BookOpen className="w-3.5 h-3.5 shrink-0" />
          {isEditingTitle ? (
            <div className="flex min-w-0 flex-1 items-center gap-1">
              <input
                value={draftTitle}
                onChange={(event) => setDraftTitle(event.target.value)}
                onBlur={saveTitle}
                onKeyDown={handleTitleKeyDown}
                className="min-w-0 flex-1 rounded-md border border-ns-primary/60 bg-ns-bg-secondary px-2 py-1 text-xs font-medium text-ns-text-primary outline-none"
                autoFocus
              />
              <button
                type="button"
                onMouseDown={(event) => event.preventDefault()}
                onClick={saveTitle}
                className="rounded-md p-1 text-ns-primary hover:bg-ns-surface-hover"
                title="Kaydet"
              >
                <Check className="h-3.5 w-3.5" />
              </button>
              <button
                type="button"
                onMouseDown={(event) => event.preventDefault()}
                onClick={cancelTitleEdit}
                className="rounded-md p-1 text-ns-text-muted hover:bg-ns-surface-hover hover:text-ns-text-primary"
                title="İptal"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          ) : (
            <div className="group flex min-w-0 items-center gap-1.5">
              <span className="text-ns-text-primary truncate font-medium">{note.title || 'İsimsiz PDF'}</span>
              <button
                type="button"
                onClick={() => {
                  setDraftTitle(note.title || '');
                  setIsEditingTitle(true);
                }}
                className="rounded-md p-1 text-ns-text-muted opacity-0 transition-opacity hover:bg-ns-surface-hover hover:text-ns-text-primary group-hover:opacity-100 focus:opacity-100"
                title="Adı Değiştir"
              >
                <Edit2 className="h-3.5 w-3.5" />
              </button>
            </div>
          )}
        </div>
        
        {numPages && (
          <div className="flex shrink-0 items-center gap-2 bg-ns-bg-secondary rounded-lg p-1 border border-ns-border">
            <button 
              onClick={() => setScale(s => Math.max(0.5, s - 0.1))}
              className="p-1 hover:bg-ns-surface-hover rounded text-ns-text-secondary hover:text-ns-text-primary transition-colors"
              title="Uzaklaş"
            ><ZoomOut className="w-4 h-4" /></button>
            <span className="text-xs font-medium w-12 text-center text-ns-text-secondary">{Math.round(scale * 100)}%</span>
            <button 
              onClick={() => setScale(s => Math.min(3, s + 0.1))}
              className="p-1 hover:bg-ns-surface-hover rounded text-ns-text-secondary hover:text-ns-text-primary transition-colors"
              title="Yakınlaş"
            ><ZoomIn className="w-4 h-4" /></button>
          </div>
        )}
      </header>
      
      <div className="mobile-scroll-area flex-1 min-h-0 w-full overflow-auto bg-ns-bg-secondary flex justify-start md:justify-center p-3 md:p-8 no-scrollbar">
        {loading ? (
          <div className="flex flex-col items-center justify-center h-full gap-3 text-ns-text-muted">
            <Loader2 className="w-8 h-8 animate-spin text-ns-primary" />
            <span className="text-sm font-medium">PDF yükleniyor...</span>
          </div>
        ) : error ? (
          <div className="flex flex-col items-center justify-center h-full text-red-400 gap-2">
            <span className="text-sm font-medium">{error}</span>
          </div>
        ) : pdfData ? (
          <div className="w-full min-w-max flex justify-center pb-8">
            <Document
              file={pdfData}
              onLoadSuccess={onDocumentLoadSuccess}
              loading={
                <div className="flex items-center justify-center p-12 text-ns-text-muted gap-2">
                  <Loader2 className="w-4 h-4 animate-spin" /> 
                  <span className="text-sm">Belge hazırlanıyor...</span>
                </div>
              }
            >
              <div className="flex min-w-max flex-col items-center gap-6">
                {numPages && Array.from(new Array(numPages), (el, index) => (
                  <div key={`page_${index + 1}`} className="shadow-2xl rounded-lg overflow-hidden border border-ns-border bg-white ring-1 ring-black/5">
                    <Page 
                      pageNumber={index + 1} 
                      scale={scale} 
                      renderTextLayer={true}
                      renderAnnotationLayer={true}
                    />
                  </div>
                ))}
              </div>
            </Document>
          </div>
        ) : (
          <div className="flex items-center justify-center h-full text-ns-text-muted text-sm font-medium">
            PDF dosyası bulunamadı.
          </div>
        )}
      </div>
    </div>
  );
};
