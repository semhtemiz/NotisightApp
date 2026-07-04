import React, { useState, useRef, useEffect } from 'react';
import { Play, Pause, Volume2, VolumeX } from 'lucide-react';
import type { Note } from '../types';
import { apiClient } from '../utils/apiClient';

interface AudioViewerProps {
  note: Note;
  folderPathStr?: string;
}

export const AudioViewer: React.FC<AudioViewerProps> = ({ note, folderPathStr }) => {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [playbackRate, setPlaybackRate] = useState(1);
  const [isMuted, setIsMuted] = useState(false);
  const [audioSrc, setAudioSrc] = useState<string | undefined>(undefined);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let isMounted = true;
    let objectUrl: string | null = null;

    const fetchAudio = async () => {
      if (!note.fileUrl) {
        setIsLoading(false);
        return;
      }
      
      try {
        setIsLoading(true);
        const res = await apiClient.fetchWithAuth(note.fileUrl);
        
        if (!res.ok) throw new Error('Ses yüklenemedi');
        
        const blob = await res.blob();
        if (isMounted) {
          objectUrl = URL.createObjectURL(blob);
          setAudioSrc(objectUrl);
        }
      } catch (err) {
        console.error('Audio fetch error:', err);
      } finally {
        if (isMounted) setIsLoading(false);
      }
    };

    fetchAudio();

    return () => {
      isMounted = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [note.fileUrl]);

  const togglePlayPause = () => {
    if (audioRef.current) {
      if (isPlaying) {
        audioRef.current.pause();
      } else {
        audioRef.current.play();
      }
      setIsPlaying(!isPlaying);
    }
  };

  const handleTimeUpdate = () => {
    if (audioRef.current) {
      setCurrentTime(audioRef.current.currentTime);
    }
  };

  const handleLoadedMetadata = () => {
    if (audioRef.current) {
      setDuration(audioRef.current.duration);
    }
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newTime = Number(e.target.value);
    if (audioRef.current) {
      audioRef.current.currentTime = newTime;
      setCurrentTime(newTime);
    }
  };

  const toggleMute = () => {
    if (audioRef.current) {
      audioRef.current.muted = !isMuted;
      setIsMuted(!isMuted);
    }
  };

  const cyclePlaybackRate = () => {
    if (audioRef.current) {
      const newRate = playbackRate === 1 ? 1.5 : playbackRate === 1.5 ? 2 : 1;
      audioRef.current.playbackRate = newRate;
      setPlaybackRate(newRate);
    }
  };

  const formatTime = (time: number) => {
    if (isNaN(time)) return '0:00';
    const mins = Math.floor(time / 60);
    const secs = Math.floor(time % 60);
    return `${mins}:${secs < 10 ? '0' : ''}${secs}`;
  };

  return (
    <div className="flex-1 flex flex-col bg-ns-bg-primary h-full relative">
      <header className="h-12 border-b border-ns-border flex items-center px-4 md:px-8 gap-4 justify-between shrink-0">
        <div className="flex min-w-0 flex-1 items-center gap-2 overflow-hidden text-ns-text-muted text-xs">
          <span className="truncate">{folderPathStr || 'çalışma alanı'}</span>
          <span className="shrink-0">/</span>
          <span className="truncate text-ns-text-primary">{note.title || 'İsimsiz Ses'}</span>
        </div>
      </header>
      <div className="flex-1 flex flex-col items-center justify-start w-full h-full p-4 sm:p-8 overflow-y-auto gap-8">
         
         <div className="w-full max-w-2xl bg-ns-bg-secondary p-5 sm:p-12 rounded-3xl border border-ns-border shadow-2xl flex flex-col items-center shrink-0">
             
             <div className="w-20 h-20 sm:w-24 sm:h-24 bg-ns-primary/10 rounded-full flex items-center justify-center mb-6 relative">
                 {isPlaying && (
                   <div className="absolute inset-0 bg-ns-primary/20 rounded-full animate-ping opacity-75"></div>
                 )}
                 <svg xmlns="http://www.w3.org/2000/svg" width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-ns-primary relative z-10"><path d="M12 2v20M17 5v14M22 10v4M7 5v14M2 10v4"/></svg>
             </div>
             
             <h2 className="text-xl sm:text-2xl font-bold mb-8 text-center text-ns-text-primary">{note.title}</h2>
             
             {isLoading ? (
               <div className="flex flex-col items-center justify-center gap-3 text-ns-text-muted my-6">
                 <div className="w-6 h-6 animate-spin rounded-full border-2 border-ns-primary border-t-transparent"></div>
                 <span className="text-sm font-medium">Ses yükleniyor...</span>
               </div>
             ) : audioSrc ? (
               <div className="w-full flex flex-col gap-6">
                 <audio 
                    ref={audioRef} 
                    src={audioSrc} 
                    onTimeUpdate={handleTimeUpdate}
                    onLoadedMetadata={handleLoadedMetadata}
                    onEnded={() => setIsPlaying(false)}
                    className="hidden" 
                 />
                 
                 <div className="flex items-center gap-3 sm:gap-4 w-full px-2 sm:px-4">
                   <span className="text-xs text-ns-text-muted font-medium w-10 text-right shrink-0">{formatTime(currentTime)}</span>
                   <input 
                     type="range" 
                     min="0" 
                     max={duration || 100} 
                     value={currentTime} 
                     onChange={handleSeek}
                     className="flex-1 h-2 bg-ns-bg-tertiary rounded-lg appearance-none cursor-pointer accent-ns-primary outline-none focus:ring-2 focus:ring-ns-primary/50"
                   />
                   <span className="text-xs text-ns-text-muted font-medium w-10 text-left shrink-0">{formatTime(duration)}</span>
                 </div>
                 
                 <div className="flex items-center justify-between w-full px-2 sm:px-8 mt-2">
                    <button onClick={cyclePlaybackRate} className="w-10 h-10 sm:w-12 sm:h-12 flex items-center justify-center rounded-full hover:bg-ns-surface-hover text-ns-text-secondary hover:text-ns-text-primary transition-colors text-xs font-bold">
                       {playbackRate}x
                    </button>
                    
                    <button 
                      onClick={togglePlayPause} 
                      className="w-14 h-14 sm:w-16 sm:h-16 bg-ns-primary hover:bg-ns-primary-hover flex items-center justify-center rounded-full text-white shadow-lg shadow-ns-primary/30 transition-transform hover:scale-105 active:scale-95"
                    >
                      {isPlaying ? <Pause className="w-6 h-6 sm:w-7 sm:h-7 fill-current" /> : <Play className="w-6 h-6 sm:w-7 sm:h-7 fill-current translate-x-0.5" />}
                    </button>
                    
                    <button onClick={toggleMute} className="w-10 h-10 sm:w-12 sm:h-12 flex items-center justify-center rounded-full hover:bg-ns-surface-hover text-ns-text-secondary hover:text-ns-text-primary transition-colors">
                       {isMuted ? <VolumeX className="w-5 h-5 sm:w-6 sm:h-6" /> : <Volume2 className="w-5 h-5 sm:w-6 sm:h-6" />}
                    </button>
                 </div>
               </div>
             ) : (
               <div className="text-ns-text-muted text-sm">Ses dosyası bulunamadı.</div>
             )}
         </div>

         {note.content && (
           <div className="w-full max-w-2xl bg-ns-bg-secondary p-6 sm:p-8 rounded-2xl border border-ns-border shrink-0">
               <h3 className="text-xs sm:text-sm font-bold text-ns-text-muted mb-4 uppercase tracking-wider flex items-center gap-2">
                 <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-ns-primary"><path d="m12 3-1.912 5.813a2 2 0 0 1-1.275 1.275L3 12l5.813 1.912a2 2 0 0 1 1.275 1.275L12 21l1.912-5.813a2 2 0 0 1 1.275-1.275L21 12l-5.813-1.912a2 2 0 0 1-1.275-1.275L12 3Z"/></svg>
                 Yapay Zeka Dökümü
               </h3>
               <div className="prose prose-sm sm:prose-base prose-invert text-ns-text-primary leading-relaxed whitespace-pre-wrap break-words" dangerouslySetInnerHTML={{ __html: note.content }} />
           </div>
         )}
      </div>
    </div>
  );
};
