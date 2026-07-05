import React, { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Mic, Square, Pause, Play, X, Folder, HelpCircle, StopCircle, Check, Loader2 } from 'lucide-react';
import type { Folder as FolderType, Note } from '../types';
import { apiClient } from '../utils/apiClient';
import { buildApiUrl } from '../utils/apiConfig';

interface VoiceRecorderModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (title: string, folderId: string, audioBlob: Blob) => void;
  onNoteCreated?: (note: Note) => void;
  folders: FolderType[];
  preferredFolderId?: string | null;
}

const readAudioBlobDurationSeconds = (blob: Blob): Promise<number | undefined> =>
  new Promise(resolve => {
    const audio = document.createElement('audio');
    const objectUrl = URL.createObjectURL(blob);

    const cleanup = () => {
      URL.revokeObjectURL(objectUrl);
      audio.removeAttribute('src');
    };

    audio.preload = 'metadata';
    audio.onloadedmetadata = () => {
      const duration = audio.duration;
      cleanup();
      resolve(Number.isFinite(duration) && duration > 0 ? duration : undefined);
    };
    audio.onerror = () => {
      cleanup();
      resolve(undefined);
    };
    audio.src = objectUrl;
  });

export const VoiceRecorderModal: React.FC<VoiceRecorderModalProps> = ({ isOpen, onClose, onSave, onNoteCreated, folders, preferredFolderId }) => {
  const [status, setStatus] = useState<'idle' | 'recording' | 'paused' | 'done'>('idle');
  const [selectedFolderId, setSelectedFolderId] = useState<string>(folders[0]?.id || '');
  const [title, setTitle] = useState('Sesli Not');
  const [volume, setVolume] = useState<number>(0);
  const [isUploading, setIsUploading] = useState(false);
  
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<BlobPart[]>([]);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const animationFrameRef = useRef<number | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  
  const finalBlobRef = useRef<Blob | null>(null);

  const flatFolders = React.useMemo(() => {
    const flatten = (items: FolderType[], prefix = ''): {id: string, name: string}[] => {
      let result: {id: string, name: string}[] = [];
      items.forEach(item => {
        if (item.id === 'root-notes') return;
        result.push({ id: item.id, name: `${prefix}${item.name}` });
        if (item.folders) {
          result = result.concat(flatten(item.folders, `${prefix}${item.name} / `));
        }
      });
      return result;
    };
    return flatten(folders);
  }, [folders]);

  useEffect(() => {
    if (!isOpen) {
      resetState();
    } else {
      const preferredExists = preferredFolderId && flatFolders.some(folder => folder.id === preferredFolderId);
      setSelectedFolderId(preferredExists ? preferredFolderId : flatFolders[0]?.id || '');
    }
  }, [isOpen, flatFolders, preferredFolderId]);

  const resetState = () => {
    setStatus('idle');
    setVolume(0);
    audioChunksRef.current = [];
    finalBlobRef.current = null;
    setTitle('Sesli Not');
    setIsUploading(false);
    stopMediaTracks();
  };

  const stopMediaTracks = () => {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach(track => track.stop());
      streamRef.current = null;
    }
    if (animationFrameRef.current) {
      cancelAnimationFrame(animationFrameRef.current);
    }
    if (audioContextRef.current) {
      audioContextRef.current.close().catch(console.error);
      audioContextRef.current = null;
    }
  };

  const updateVolume = () => {
    if (!analyserRef.current) return;
    const dataArray = new Uint8Array(analyserRef.current.frequencyBinCount);
    analyserRef.current.getByteFrequencyData(dataArray);
    
    let sum = 0;
    for (let i = 0; i < dataArray.length; i++) {
        sum += dataArray[i];
    }
    const avg = sum / dataArray.length;
    setVolume(avg);

    animationFrameRef.current = requestAnimationFrame(updateVolume);
  };

  const startRecording = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
      
      const audioContext = new AudioContext();
      audioContextRef.current = audioContext;
      const analyser = audioContext.createAnalyser();
      analyserRef.current = analyser;
      analyser.fftSize = 256;
      
      const source = audioContext.createMediaStreamSource(stream);
      source.connect(analyser);
      
      const mediaRecorder = new MediaRecorder(stream);
      mediaRecorderRef.current = mediaRecorder;
      audioChunksRef.current = [];
      
      mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };
      
      mediaRecorder.onstop = () => {
        const audioBlob = new Blob(audioChunksRef.current, { type: 'audio/webm' });
        finalBlobRef.current = audioBlob;
      };
      
      mediaRecorder.start();
      setStatus('recording');
      updateVolume();
      
    } catch (err) {
      console.error("Microphone access denied:", err);
      alert("Lütfen mikrofon erişimine izin verin.");
    }
  };

  const pauseRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
      mediaRecorderRef.current.pause();
      setStatus('paused');
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
      }
      setVolume(0);
    }
  };

  const resumeRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'paused') {
      mediaRecorderRef.current.resume();
      setStatus('recording');
      updateVolume();
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current && (mediaRecorderRef.current.state === 'recording' || mediaRecorderRef.current.state === 'paused')) {
      mediaRecorderRef.current.stop();
      setStatus('done');
      setTimeout(() => {
        stopMediaTracks();
        setVolume(0);
      }, 100);
    }
  };

  const handleSave = async () => {
    if (!finalBlobRef.current) return;
    
    setIsUploading(true);
    try {
      const formData = new FormData();
      const file = new File([finalBlobRef.current], `${title || 'kayit'}.webm`, { type: 'audio/webm' });
      formData.append('file', file);
      if (selectedFolderId && selectedFolderId !== 'root-notes') {
        formData.append('folderId', selectedFolderId);
      }
      const durationSeconds = await readAudioBlobDurationSeconds(finalBlobRef.current);
      if (durationSeconds) {
        formData.append('durationSeconds', String(durationSeconds));
      }
      
      const response = await apiClient.fetchWithAuth('/notes/upload-audio', {
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
                fileUrl: realNote.fileUrl ? buildApiUrl(`/notes/${realNote.id}/file`) : undefined,
                durationSeconds: realNote.durationSeconds
              });
            }
            onClose();
            return;
          }
        } catch (e) {
          console.error("Could not fetch full note after voice upload", e);
        }

        if (onNoteCreated) {
           onNoteCreated({
              id: data.noteId || data.id,
              title: title || data.title,
              content: 'Ses kaydı döküme çevrildi.',
              tags: ['Sesli'],
              tagIds: [],
              folderId: selectedFolderId && selectedFolderId !== 'root-notes' ? selectedFolderId : undefined,
              fileUrl: data.fileUrl ? buildApiUrl(`/notes/${data.noteId || data.id}/file`) : undefined,
              fileType: data.fileType || 'audio',
              durationSeconds: data.durationSeconds,
              updatedAtUtc: data.updatedAtUtc ?? new Date().toISOString(),
              vectorSyncStatus: data.vectorSyncStatus ?? 'pending',
              vectorSyncError: data.vectorSyncError,
              vectorSyncedAtUtc: data.vectorSyncedAtUtc
           });
        }
        onClose();
      } else {
        const errData = await response.json().catch(() => ({}));
        const errorMessage = errData.detail || errData.message || errData.title || response.statusText;
        alert(`Ses kaydı yüklenemedi: ${errorMessage}`);
      }
    } catch(err) {
      console.error(err);
      alert('Yükleme hatası.');
    } finally {
      setIsUploading(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4">
      <motion.div 
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        exit={{ opacity: 0, scale: 0.95 }}
        className="bg-ns-bg-secondary border border-ns-border rounded-2xl w-full max-w-md max-h-[calc(100dvh-2rem)] shadow-2xl overflow-y-auto relative"
      >
        <div className="flex items-center justify-between p-4 border-b border-ns-border">
          <h2 className="text-sm font-semibold text-ns-text-primary flex items-center gap-2">
            <Mic className="w-4 h-4 text-ns-primary" />
            Sesli Not Kaydet
          </h2>
          <button onClick={onClose} disabled={isUploading} className="p-1 hover:bg-ns-surface-hover rounded-md text-ns-text-muted transition-colors disabled:opacity-50">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="p-5 sm:p-6 flex flex-col items-center">
          
          {(status === 'idle' || status === 'recording' || status === 'paused') && (
            <div className="w-full flex flex-col items-center gap-8 py-4">
              <div className="relative flex items-center justify-center w-32 h-32 rounded-full border-2 border-ns-border bg-ns-bg-primary/50">
                {status === 'recording' && (
                  <motion.div 
                    className="absolute inset-0 bg-ns-primary/20 rounded-full"
                    animate={{ scale: 1 + (volume / 100) * 0.5 }}
                    transition={{ type: 'spring', bounce: 0, duration: 0.1 }}
                  />
                )}
                <Mic className={`w-12 h-12 relative z-10 ${status === 'recording' ? 'text-ns-primary' : 'text-ns-text-disabled'}`} />
              </div>

              <div className="flex items-center gap-4">
                {status === 'idle' && (
                  <button 
                    onClick={startRecording}
                    className="flex items-center justify-center gap-2 bg-ns-primary hover:bg-ns-primary-hover text-white px-6 py-3 rounded-full font-medium transition-colors"
                  >
                    <Play className="w-5 h-5 fill-current" />
                    Kayıt Başlat
                  </button>
                )}

                {status === 'recording' && (
                  <>
                    <button 
                      onClick={pauseRecording}
                      className="flex items-center justify-center p-4 bg-ns-surface-hover hover:bg-ns-divider text-white rounded-full transition-colors"
                      title="Duraklat"
                    >
                      <Pause className="w-6 h-6 fill-current text-ns-text-primary" />
                    </button>
                    <button 
                      onClick={stopRecording}
                      className="flex items-center justify-center p-4 bg-red-500/20 hover:bg-red-500/30 text-red-500 rounded-full transition-colors border border-red-500/50"
                      title="Bitir"
                    >
                      <Square className="w-6 h-6 fill-current" />
                    </button>
                  </>
                )}

                {status === 'paused' && (
                  <>
                    <button 
                      onClick={resumeRecording}
                      className="flex items-center justify-center p-4 bg-ns-primary hover:bg-ns-primary-hover text-white rounded-full transition-colors"
                      title="Devam Et"
                    >
                      <Play className="w-6 h-6 fill-current" />
                    </button>
                    <button 
                      onClick={stopRecording}
                      className="flex items-center justify-center p-4 bg-red-500/20 hover:bg-red-500/30 text-red-500 rounded-full transition-colors border border-red-500/50"
                      title="Bitir"
                    >
                      <Square className="w-6 h-6 fill-current" />
                    </button>
                  </>
                )}
              </div>
            </div>
          )}

          {status === 'done' && (
            <div className="w-full space-y-4">
              <div className="p-4 bg-ns-primary/10 border border-ns-primary/20 rounded-xl flex items-center justify-center gap-2 text-ns-primary mb-6">
                {isUploading ? (
                  <Loader2 className="w-5 h-5 animate-spin" />
                ) : (
                  <Check className="w-5 h-5" />
                )}
                <span className="font-medium">
                  {isUploading ? 'Kaydediliyor ve dönüştürülüyor...' : 'Ses kaydı tamamlandı'}
                </span>
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-medium text-ns-text-muted">Not Başlığı</label>
                <input 
                  type="text"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="Başlık girin..."
                  disabled={isUploading}
                  className="w-full bg-ns-bg-primary border border-ns-border rounded-lg px-4 py-2.5 text-ns-text-primary focus:outline-none focus:ring-2 focus:ring-ns-primary/50 focus:border-ns-primary disabled:opacity-50"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-medium text-ns-text-muted">Klasör Seçin</label>
                <div className="relative">
                  <Folder className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-ns-text-secondary" />
                  <select
                    value={selectedFolderId}
                    onChange={(e) => setSelectedFolderId(e.target.value)}
                    disabled={isUploading}
                    className="w-full bg-ns-bg-primary border border-ns-border rounded-lg pl-10 pr-4 py-2.5 text-ns-text-primary focus:outline-none focus:ring-2 focus:ring-ns-primary/50 focus:border-ns-primary appearance-none disabled:opacity-50"
                  >
                    {flatFolders.length === 0 && <option value="">Klasör yok</option>}
                    {flatFolders.map(f => (
                      <option key={f.id} value={f.id}>{f.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              <button 
                onClick={handleSave}
                disabled={isUploading}
                className="w-full mt-4 bg-ns-primary hover:bg-ns-primary-hover text-white font-medium rounded-lg px-6 py-2.5 transition-colors flex items-center justify-center gap-2 disabled:opacity-50"
              >
                {isUploading ? 'Yükleniyor...' : 'Kaydet'}
              </button>
            </div>
          )}
        </div>
      </motion.div>
    </div>
  );
};
