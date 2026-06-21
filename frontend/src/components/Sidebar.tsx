import React, { useState, useRef, useEffect } from 'react';
import { ChevronRight, ChevronDown, FileText, Plus, FolderPlus, Search, Settings, Mic, Edit2, Trash2, PanelLeftClose, Upload, Headphones, BookOpen } from 'lucide-react';
import type { Folder, Note } from '../types';
import { apiClient } from '../utils/apiClient';
import { CURRENT_USER_CHANGED_EVENT, getDisplayUserName, readStoredUser, writeStoredUser } from '../utils/currentUser';

interface FolderItemProps {
  folder: Folder;
  level: number;
  activeNoteId: string | null;
  activeFolderId: string | null;
  editingFolderId: string | null;
  onClearEditingFolder: () => void;
  onSelectNote: (id: string) => void;
  onToggleFolder: (id: string) => void;
  onUpdateFolderName: (id: string, name: string) => void;
  onDeleteNote: (id: string, name: string) => void;
  onDeleteFolder: (id: string, name: string) => void;
}

/**
 * Klasör nesnesini ve içindeki notları (ağaç yapısını) görselleştiren Recursive (özyinelemeli) bileşen.
 * Klasör adını düzenlemeyi, klasörü genişletmeyi/daraltmayı ve notları listelemeyi sağlar.
 */
const FolderItem: React.FC<FolderItemProps> = ({
  folder,
  level,
  activeNoteId,
  activeFolderId,
  editingFolderId,
  onClearEditingFolder,
  onSelectNote,
  onToggleFolder,
  onUpdateFolderName,
  onDeleteNote,
  onDeleteFolder
}) => {
  // Klasör adının "düzenleme" modunda olup olmadığını tutar
  const [isEditing, setIsEditing] = useState(false);
  // Düzenleme sırasında klasörün geçici adını tutar
  const [editName, setEditName] = useState(folder.name);
  const inputRef = useRef<HTMLInputElement>(null);

  // Dışarıdan yeni bir klasör oluşturulduğunda otomatik olarak ismini değiştirme moduna geçmesini sağlar
  useEffect(() => {
    if (editingFolderId === folder.id) {
      setIsEditing(true);
      onClearEditingFolder();
    }
  }, [editingFolderId, folder.id, onClearEditingFolder]);

  // Düzenleme moduna girildiğinde input kutusuna odaklanıp metnin tamamını seçer
  useEffect(() => {
    if (isEditing && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [isEditing]);

  // Yeni klasör adını kaydeder (boş bırakıldıysa eski adı geri yükler)
  const handleSave = () => {
    if (editName.trim()) {
      onUpdateFolderName(folder.id, editName.trim());
    } else {
      setEditName(folder.name); // revert if empty
    }
    setIsEditing(false); // düzenleme modundan çık
  };

  // Düzenlerken klavye kullanımını dinler (Enter ile kaydet, Esc ile iptal)
  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') handleSave();
    if (e.key === 'Escape') {
      setEditName(folder.name);
      setIsEditing(false);
    }
  };

  // Bu klasörün o anda "seçili" klasör olup olmadığını kontrol edip ona göre stil belirleyeceğiz
  const isFolderActive = activeFolderId === folder.id;

  return (
    <div className="space-y-1">
      <div
        className={`w-full flex items-center gap-1 font-medium text-left p-1 rounded-lg transition-all group ${isFolderActive ? 'bg-ns-green-surface/55 ring-1 ring-ns-primary/20 shadow-sm shadow-ns-primary/5' : 'hover:bg-ns-surface-hover/70'
          }`}
        style={{ paddingLeft: `${level * 8 + 4}px` }}
      >
        <button
          onClick={() => onToggleFolder(folder.id)}
          className="flex items-center gap-1 flex-1 min-w-0"
        >
          {folder.isOpen ? <ChevronDown className="w-3 h-3 text-ns-text-muted shrink-0" /> : <ChevronRight className="w-3 h-3 text-ns-text-muted shrink-0" />}
          {isEditing ? (
            <input
              ref={inputRef}
              value={editName}
              onChange={e => setEditName(e.target.value)}
              onBlur={handleSave}
              onKeyDown={handleKeyDown}
              onClick={e => e.stopPropagation()}
              className="bg-ns-bg-primary border border-ns-primary rounded-lg px-1 text-ns-text-primary text-[10px] uppercase tracking-widest w-full outline-none"
            />
          ) : (
            <span className="text-ns-text-secondary uppercase text-[10px] tracking-widest truncate">{folder.name}</span>
          )}
        </button>

        {!isEditing && (
          <div className="opacity-0 group-hover:opacity-100 flex items-center shrink-0 transition-opacity">
            <button
              onClick={(e) => {
                e.stopPropagation();
                setEditName(folder.name);
                setIsEditing(true);
              }}
              className="text-ns-text-muted hover:text-ns-text-primary p-0.5 rounded"
              title="Adı Değiştir"
            >
              <Edit2 className="w-3 h-3" />
            </button>
            <button
              onClick={(e) => {
                e.stopPropagation();
                onDeleteFolder(folder.id, folder.name);
              }}
              className="text-ns-text-muted hover:text-ns-error p-0.5 rounded ml-0.5"
              title="Klasörü Sil"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        )}
      </div>

      {folder.isOpen && (
        <div className="space-y-1">
          {folder.folders?.map(subfolder => (
            <FolderItem
              key={subfolder.id}
              folder={subfolder}
              level={level + 1}
              activeNoteId={activeNoteId}
              activeFolderId={activeFolderId}
              editingFolderId={editingFolderId}
              onClearEditingFolder={onClearEditingFolder}
              onSelectNote={onSelectNote}
              onToggleFolder={onToggleFolder}
              onUpdateFolderName={onUpdateFolderName}
              onDeleteNote={onDeleteNote}
              onDeleteFolder={onDeleteFolder}
            />
          ))}
          {folder.notes.map(note => (
            <div key={note.id} className="group flex items-center relative" style={{ paddingLeft: `${(level + 1) * 8 + 16}px` }}>
              <button
                onClick={() => onSelectNote(note.id)}
                className={`w-full flex items-center gap-2 p-1.5 rounded-lg transition-all text-left pr-8 ${activeNoteId === note.id
                    ? 'bg-ns-selection text-ns-primary shadow-sm shadow-ns-primary/10 ring-1 ring-ns-primary/20'
                    : 'hover:bg-ns-surface-hover/70 text-ns-text-secondary'
                  }`}
              >
                {(() => {
                  const Icon = note.fileType === 'audio' ? Headphones : note.fileType === 'pdf' ? BookOpen : FileText;
                  return <Icon className={`w-4 h-4 shrink-0 ${activeNoteId === note.id ? 'text-ns-primary' : 'text-ns-text-muted'}`} />;
                })()}
                <span className="truncate flex-1 text-[13px]">{note.title || 'İsimsiz'}</span>
              </button>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onDeleteNote(note.id, note.title || 'İsimsiz');
                }}
                className="absolute right-2 opacity-0 group-hover:opacity-100 text-ns-text-muted hover:text-ns-error transition-opacity p-1 rounded-lg hover:bg-ns-surface-hover/70"
                title="Sil"
              >
                <Trash2 className="w-3.5 h-3.5" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

interface SidebarProps {
  folders: Folder[];
  activeNoteId: string | null;
  activeFolderId: string | null;
  editingFolderId: string | null;
  onClearEditingFolder: () => void;
  onSelectNote: (id: string) => void;
  onToggleFolder: (id: string) => void;
  onCreateNote: () => void;
  onCreateFolder: () => void;
  onUpdateFolderName: (id: string, name: string) => void;
  onDeleteNote: (id: string, name: string) => void;
  onDeleteFolder: (id: string, name: string) => void;
  onRecordVoiceNote: () => void;
  onUploadPdf?: () => void;
  onOpenSettings: () => void;
  onOpenSearch?: () => void;
  onSearch?: () => void;
  onCollapse: () => void;
  width?: number;
}

export const Sidebar: React.FC<SidebarProps> = ({
  folders, activeNoteId, activeFolderId, editingFolderId, onClearEditingFolder, onSelectNote, onToggleFolder, onCreateNote, onCreateFolder, onUpdateFolderName, onDeleteNote, onDeleteFolder, onRecordVoiceNote, onUploadPdf, onOpenSettings, onOpenSearch, onCollapse, width
}) => {
  const [showAddMenu, setShowAddMenu] = useState(false);
  const [avatarSeed, setAvatarSeed] = useState(() => localStorage.getItem('avatarSeed') || 'Felix');
  const [currentUserName, setCurrentUserName] = useState(() => getDisplayUserName(readStoredUser()));
  const addMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleAvatarChange = () => {
      setAvatarSeed(localStorage.getItem('avatarSeed') || 'Felix');
    };
    window.addEventListener('avatarChanged', handleAvatarChange);
    return () => window.removeEventListener('avatarChanged', handleAvatarChange);
  }, []);

  useEffect(() => {
    const syncUserName = () => {
      setCurrentUserName(getDisplayUserName(readStoredUser()));
    };

    const fetchCurrentUser = async () => {
      try {
        const user = await apiClient.get('/auth/me');
        writeStoredUser(user);
        setCurrentUserName(getDisplayUserName(user));
      } catch {
        syncUserName();
      }
    };

    syncUserName();
    fetchCurrentUser();
    window.addEventListener(CURRENT_USER_CHANGED_EVENT, syncUserName);
    window.addEventListener('storage', syncUserName);

    return () => {
      window.removeEventListener(CURRENT_USER_CHANGED_EVENT, syncUserName);
      window.removeEventListener('storage', syncUserName);
    };
  }, []);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (addMenuRef.current && !addMenuRef.current.contains(event.target as Node)) {
        setShowAddMenu(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);
  return (
    <aside
      className={`absolute md:relative z-20 h-full shrink-0 flex flex-col ns-panel-shell text-ns-text-primary shadow-2xl shadow-black/20 backdrop-blur-xl md:my-2 md:ml-2 md:h-[calc(100%-1rem)] md:rounded-3xl md:overflow-hidden md:shadow-none transition-all ${width ? '' : 'w-[80%] sm:w-[280px] md:w-[240px]'}`}
      style={width ? { width: `${width}px` } : undefined}
    >

      {/* Top Action Bar */}
      <div className="h-12 border-b ns-hairline flex items-center justify-between px-3 shrink-0">
        <span className="text-xs font-bold uppercase tracking-wider text-ns-text-muted">GEZGİN</span>
        <div className="flex items-center gap-1.5 text-ns-text-secondary">
          <div className="relative flex" ref={addMenuRef}>
            <button
              onClick={() => setShowAddMenu(!showAddMenu)}
              className="hover:text-ns-primary-hover hover:bg-ns-surface-hover/70 p-1.5 rounded-lg transition-colors"
              title="Yeni İçerik"
            >
              <Plus className="w-4 h-4" />
            </button>
            {showAddMenu && (
              <div className="absolute top-full left-0 mt-1 w-40 ns-glass rounded-xl shadow-xl z-50 p-1 overflow-hidden">
                <button
                  onClick={() => { onCreateNote(); setShowAddMenu(false); }}
                  className="w-full px-3 py-2 text-left text-sm flex items-center gap-2 hover:bg-ns-surface-hover/70 transition-colors text-ns-text-primary rounded-lg"
                >
                  <FileText className="w-4 h-4 text-ns-primary" />
                  <span>Yeni Not</span>
                </button>
                {onUploadPdf && (
                  <button
                    onClick={() => { onUploadPdf(); setShowAddMenu(false); }}
                    className="w-full px-3 py-2 text-left text-sm flex items-center gap-2 hover:bg-ns-surface-hover/70 transition-colors text-ns-text-primary rounded-lg"
                  >
                    <Upload className="w-4 h-4 text-ns-primary" />
                    <span>Belge / Ses Yükle</span>
                  </button>
                )}
              </div>
            )}
          </div>
          <button
            onClick={onRecordVoiceNote}
            className="hover:text-ns-primary-hover hover:bg-ns-surface-hover/70 p-1.5 rounded-lg transition-colors"
            title="Sesli Not Kaydet"
          >
            <Mic className="w-4 h-4" />
          </button>
          <button
            onClick={onCreateFolder}
            className="hover:text-ns-primary-hover hover:bg-ns-surface-hover/70 p-1.5 rounded-lg transition-colors"
            title="Yeni Klasör"
          >
            <FolderPlus className="w-4 h-4" />
          </button>
          <button
            onClick={onOpenSearch}
            className="hover:text-ns-primary-hover hover:bg-ns-surface-hover/70 p-1.5 rounded-lg transition-colors"
            title="Ara (Cmd+K)"
          >
            <Search className="w-4 h-4" />
          </button>
          <div className="w-px h-4 bg-ns-border/35 ml-1" />
          <button
            onClick={onCollapse}
            className="hover:text-ns-primary-hover hover:bg-ns-surface-hover/70 p-1.5 rounded-lg transition-colors -ml-1"
            title="Gezgini Kapat"
          >
            <PanelLeftClose className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* File Tree */}
      <div className="flex-1 p-2 space-y-1 overflow-y-auto overflow-x-hidden text-sm">
        {folders.map(folder => (
          <FolderItem
            key={folder.id}
            folder={folder}
            level={0}
            activeNoteId={activeNoteId}
            activeFolderId={activeFolderId}
            editingFolderId={editingFolderId}
            onClearEditingFolder={onClearEditingFolder}
            onSelectNote={onSelectNote}
            onToggleFolder={onToggleFolder}
            onUpdateFolderName={onUpdateFolderName}
            onDeleteNote={onDeleteNote}
            onDeleteFolder={onDeleteFolder}
          />
        ))}
      </div>

      {/* Bottom Actions */}
      <div className="p-3 border-t ns-hairline shrink-0">
        <button
          onClick={onOpenSettings}
          className="w-full flex items-center gap-3 p-2 hover:bg-ns-surface-hover/70 rounded-xl transition-colors text-sm text-ns-text-primary hover:text-ns-text-primary"
        >
          <div className="w-6 h-6 rounded-full overflow-hidden bg-ns-bg-tertiary flex items-center justify-center shrink-0 border border-ns-border">
            <img src={`https://api.dicebear.com/9.x/identicon/svg?seed=${avatarSeed}`} alt="Avatar" className="w-full h-full object-cover" />
          </div>
          <span className="flex-1 text-left truncate font-medium" title={currentUserName}>{currentUserName}</span>
          <Settings className="w-4 h-4 shrink-0 text-ns-text-muted hover:text-ns-text-secondary" />
        </button>
      </div>

    </aside>
  );
};
