import React, { useState, useEffect } from 'react';
import type { Note } from '../types';
import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';
import { ZoomIn, ZoomOut, Loader2, BookOpen } from 'lucide-react';
import { apiClient } from '../utils/apiClient';

// Setup worker
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();

interface PdfViewerProps {
  note: Note;
  folderPathStr?: string;
}

export const PdfViewer: React.FC<PdfViewerProps> = ({ note, folderPathStr }) => {
  const [numPages, setNumPages] = useState<number>();
  const [scale, setScale] = useState<number>(1.0);
  const [pdfData, setPdfData] = useState<Blob | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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

  return (
    <div className="flex-1 flex flex-col bg-ns-bg-primary h-full relative">
      <header className="h-12 border-b border-ns-border flex items-center px-4 md:px-8 gap-4 justify-between shrink-0 bg-ns-bg-primary/95 backdrop-blur-sm z-10">
        <div className="flex items-center gap-2 text-ns-text-muted text-xs truncate">
          <span className="truncate">{folderPathStr || 'çalışma alanı'}</span>
          <span>/</span>
          <BookOpen className="w-3.5 h-3.5" />
          <span className="text-ns-text-primary truncate font-medium">{note.title || 'İsimsiz PDF'}</span>
        </div>
        
        {numPages && (
          <div className="flex items-center gap-2 bg-ns-bg-secondary rounded-lg p-1 border border-ns-border">
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
      
      <div className="flex-1 w-full overflow-auto bg-ns-bg-secondary flex justify-center p-4 md:p-8 no-scrollbar">
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
          <div className="w-full flex justify-center pb-8">
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
              <div className="flex flex-col items-center gap-6">
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
