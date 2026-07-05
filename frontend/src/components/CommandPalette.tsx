import React, { useEffect, useMemo, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { BookOpen, FileText, Headphones, Search } from 'lucide-react';
import type { Folder, Note } from '../types';
import { findNotePath, getAllNotes } from '../utils/folderUtils';

interface CommandPaletteProps {
  isOpen: boolean;
  folders: Folder[];
  onClose: () => void;
  onSelectNote: (id: string) => void;
}

type SearchableNote = {
  note: Note;
  path: string;
  searchableText: string;
};

const MAX_RESULTS = 8;

const toPlainText = (html: string) => {
  if (!html) return '';

  const container = document.createElement('div');
  container.innerHTML = html;
  return container.textContent || container.innerText || '';
};

const getNoteIcon = (note: Note) => {
  if (note.fileType === 'audio') return Headphones;
  if (note.fileType === 'pdf') return BookOpen;
  return FileText;
};

const getUpdatedTime = (note: Note) => {
  const value = note.updatedAtUtc ? Date.parse(note.updatedAtUtc) : 0;
  return Number.isFinite(value) ? value : 0;
};

export const CommandPalette: React.FC<CommandPaletteProps> = ({
  isOpen,
  folders,
  onClose,
  onSelectNote
}) => {
  const inputRef = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);

  const searchableNotes = useMemo<SearchableNote[]>(() => {
    return getAllNotes(folders)
      .map(note => {
        const path = findNotePath(folders, note.id)?.join(' / ') || 'Ana dizin';
        const text = [
          note.title,
          path,
          note.tags?.join(' '),
          toPlainText(note.content)
        ].filter(Boolean).join(' ');

        return {
          note,
          path,
          searchableText: text.toLocaleLowerCase('tr-TR')
        };
      })
      .sort((a, b) => getUpdatedTime(b.note) - getUpdatedTime(a.note));
  }, [folders]);

  const normalizedQuery = query.trim().toLocaleLowerCase('tr-TR');
  const results = useMemo(() => {
    if (!normalizedQuery) {
      return searchableNotes.slice(0, MAX_RESULTS);
    }

    const terms = normalizedQuery
      .split(/\s+/)
      .filter(Boolean);

    return searchableNotes
      .map(item => {
        const title = item.note.title.toLocaleLowerCase('tr-TR');
        const path = item.path.toLocaleLowerCase('tr-TR');
        let score = 0;

        for (const term of terms) {
          if (title === term) score += 20;
          if (title.startsWith(term)) score += 12;
          if (title.includes(term)) score += 8;
          if (path.includes(term)) score += 4;
          if (item.searchableText.includes(term)) score += 1;
        }

        return { ...item, score };
      })
      .filter(item => item.score > 0)
      .sort((a, b) => b.score - a.score || getUpdatedTime(b.note) - getUpdatedTime(a.note))
      .slice(0, MAX_RESULTS);
  }, [normalizedQuery, searchableNotes]);

  useEffect(() => {
    if (isOpen) {
      setQuery('');
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  useEffect(() => {
    setSelectedIndex(0);
  }, [query]);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
        return;
      }

      if (event.key === 'ArrowDown') {
        event.preventDefault();
        setSelectedIndex(prev => results.length > 0 ? (prev + 1) % results.length : 0);
        return;
      }

      if (event.key === 'ArrowUp') {
        event.preventDefault();
        setSelectedIndex(prev => results.length > 0 ? (prev - 1 + results.length) % results.length : 0);
        return;
      }

      if (event.key === 'Enter' && results[selectedIndex]) {
        event.preventDefault();
        onSelectNote(results[selectedIndex].note.id);
        onClose();
      }
    };

    if (isOpen) window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose, onSelectNote, results, selectedIndex]);

  if (!isOpen) return null;

  const heading = normalizedQuery ? 'Arama Sonuçları' : 'Son Dosyalar';

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-start justify-center px-3 pt-4 sm:px-0 sm:pt-[15vh]">
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
          className="relative mx-0 max-h-[calc(100dvh-2rem)] w-full max-w-xl overflow-hidden border border-ns-border/50 bg-ns-bg-secondary/90 shadow-2xl backdrop-blur-xl sm:mx-4"
          style={{ borderRadius: '16px' }}
        >
          <div className="flex items-center border-b border-ns-border px-4 py-3">
            <Search className="mr-3 h-5 w-5 text-ns-text-muted" />
            <input
              ref={inputRef}
              type="text"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Notlarda ara..."
              className="flex-1 border-none bg-transparent text-base text-ns-text-primary placeholder:text-ns-text-disabled focus:outline-none sm:text-lg"
            />
          </div>

          <div className="max-h-[60vh] overflow-y-auto p-2">
            <div className="px-2 pb-2 pt-3 text-xs font-semibold uppercase tracking-wider text-ns-text-muted">
              {heading}
            </div>

            {results.length > 0 ? (
              <div className="space-y-1 pb-2">
                {results.map((item, index) => {
                  const Icon = getNoteIcon(item.note);
                  const isSelected = index === selectedIndex;

                  return (
                    <button
                      key={item.note.id}
                      type="button"
                      onMouseEnter={() => setSelectedIndex(index)}
                      onClick={() => {
                        onSelectNote(item.note.id);
                        onClose();
                      }}
                      className={`flex w-full items-center rounded-lg px-3 py-2.5 text-left transition-colors ${
                        isSelected
                          ? 'bg-ns-primary/10 text-ns-primary'
                          : 'text-ns-text-primary hover:bg-ns-surface-hover/50'
                      }`}
                    >
                      <Icon className={`mr-3 h-4 w-4 shrink-0 ${isSelected ? 'text-ns-primary' : 'text-ns-text-muted'}`} />
                      <div className="min-w-0 flex-1">
                        <div className="truncate text-sm font-semibold">{item.note.title || 'İsimsiz'}</div>
                        <div className="mt-0.5 truncate text-[11px] text-ns-text-muted">{item.path}</div>
                      </div>
                    </button>
                  );
                })}
              </div>
            ) : (
              <div className="px-3 py-8 text-center text-sm text-ns-text-muted">
                Eşleşen not bulunamadı.
              </div>
            )}
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
};
