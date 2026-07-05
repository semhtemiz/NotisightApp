import React, { Suspense, lazy, useEffect, useState } from 'react';
import { Sidebar } from './Sidebar';
import { EmptyState } from './EmptyState';
import { DeleteConfirmModal } from './DeleteConfirmModal';
import { Bot, FolderOpen, Loader2, FileText, X, PanelLeft, PanelRight, Headphones, BookOpen } from 'lucide-react';
import type { Folder, Note } from '../types';
import {
  findNote,
  findFolder,
  findNotePath,
  toggleFolderRecursively,
  addNoteToFolder,
  addFolderToParent,
  updateNoteInFolder,
  updateFolderName,
  deleteNoteFromFolder,
  deleteFolder,
  getAllNotes,
  isFolderDescendant,
  moveFolderInFolderTree,
  moveNoteInFolderTree
} from '../utils/folderUtils';
import { apiClient } from '../utils/apiClient';
import { buildApiUrl } from '../utils/apiConfig';

const Editor = lazy(() => import('./Editor').then(module => ({ default: module.Editor })));
const PdfViewer = lazy(() => import('./PdfViewer').then(module => ({ default: module.PdfViewer })));
const AudioViewer = lazy(() => import('./AudioViewer').then(module => ({ default: module.AudioViewer })));
const AIAssistant = lazy(() => import('./AIAssistant').then(module => ({ default: module.AIAssistant })));
const CommandPalette = lazy(() => import('./CommandPalette').then(module => ({ default: module.CommandPalette })));
const KnowledgeIngestionModal = lazy(() => import('./KnowledgeIngestionModal').then(module => ({ default: module.KnowledgeIngestionModal })));
const VoiceRecorderModal = lazy(() => import('./VoiceRecorderModal').then(module => ({ default: module.VoiceRecorderModal })));

const ROOT_NOTES_ID = 'root-notes';
const ROOT_NOTES_NAME_KEY = 'notisight_root_notes_name';
const LAST_USED_FOLDER_KEY = 'notisight_last_used_folder_id';
const DEFAULT_ROOT_NOTES_NAME = 'Genel Notlar';
const DEFAULT_FOLDER_NAME = 'Yeni Klasör';
type MobileSurface = 'explorer' | 'note' | 'ai';

const isMobileViewport = () =>
  typeof window !== 'undefined' && window.matchMedia('(max-width: 767px)').matches;

const WorkspaceFallback = () => (
  <div className="flex flex-1 items-center justify-center bg-ns-bg-primary text-ns-text-muted">
    <Loader2 className="h-5 w-5 animate-spin" />
  </div>
);

const AiPanelFallback = ({ width }: { width: number }) => (
  <div
    className="hidden h-full shrink-0 items-center justify-center border-l border-ns-border bg-ns-bg-secondary/70 text-ns-text-muted md:flex"
    style={{ width }}
  >
    <Loader2 className="h-5 w-5 animate-spin" />
  </div>
);

interface DashboardProps {
  onOpenSettings: () => void;
}

export const Dashboard: React.FC<DashboardProps> = ({ onOpenSettings }) => {
  const [folders, setFolders] = useState<Folder[]>([]);
  const [activeNoteId, setActiveNoteId] = useState<string | null>(null);
  const [openNoteIds, setOpenNoteIds] = useState<string[]>([]);
  const [activeFolderId, setActiveFolderId] = useState<string | null>(null);
  const [editingFolderId, setEditingFolderId] = useState<string | null>(null);
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => !isMobileViewport());
  const [sidebarWidth, setSidebarWidth] = useState(256);
  const [isAIAssistantOpen, setIsAIAssistantOpen] = useState(() => !isMobileViewport());
  const [aiWidth, setAiWidth] = useState(480);
  const [activeMobileSurface, setActiveMobileSurface] = useState<MobileSurface>('explorer');
  const [isCommandPaletteOpen, setIsCommandPaletteOpen] = useState(false);
  const [isUploadModalOpen, setIsUploadModalOpen] = useState(false);
  const [isVoiceRecorderOpen, setIsVoiceRecorderOpen] = useState(false);
  const [uploadTargetFolderId, setUploadTargetFolderId] = useState<string | null>(null);
  const [voiceTargetFolderId, setVoiceTargetFolderId] = useState<string | null>(null);
  const [deleteModalState, setDeleteModalState] = useState<{ isOpen: boolean; itemId: string; itemName: string; itemType: 'note' | 'folder' }>({
    isOpen: false,
    itemId: '',
    itemName: '',
    itemType: 'note'
  });

  useEffect(() => {
    const fetchData = async () => {
      try {
        const foldersData = await apiClient.get('/folders');
        const notesData = await apiClient.get('/notes');
        
        const folderMap = new Map();
        foldersData.forEach((f: any) => {
          folderMap.set(f.id, {
            id: f.id,
            name: f.name,
            isOpen: false,
            notes: [],
            folders: [],
            parentFolderId: f.parentFolderId
          });
        });
        
        const rootFolders: Folder[] = [];
        const rootNotes: Note[] = [];
        
        notesData.forEach((n: any) => {
          const note: Note = {
            id: n.id,
            title: n.title || '',
            content: n.content || '',
            tags: n.tags ? n.tags.map((t: any) => t.name) : [],
            tagIds: n.tags ? n.tags.map((t: any) => t.id) : [],
            folderId: n.folderId,
            fileUrl: n.fileUrl ? buildApiUrl(`/notes/${n.id}/file`) : undefined,
            fileType: n.fileType,
            durationSeconds: n.durationSeconds,
            vectorSyncStatus: n.vectorSyncStatus,
            vectorSyncError: n.vectorSyncError,
            vectorSyncedAtUtc: n.vectorSyncedAtUtc
          };
          if (n.folderId && folderMap.has(n.folderId)) {
            folderMap.get(n.folderId).notes.push(note);
          } else {
            rootNotes.push(note);
          }
        });
        
        foldersData.forEach((f: any) => {
          const folderNode = folderMap.get(f.id);
          if (folderNode.parentFolderId && folderMap.has(folderNode.parentFolderId)) {
            folderMap.get(folderNode.parentFolderId).folders.push(folderNode);
          } else {
            rootFolders.push(folderNode);
          }
        });
        
        if (rootNotes.length > 0) {
          rootFolders.unshift({
            id: ROOT_NOTES_ID,
            name: localStorage.getItem(ROOT_NOTES_NAME_KEY) || DEFAULT_ROOT_NOTES_NAME,
            isOpen: true,
            notes: rootNotes,
            folders: []
          });
        }
        
        setFolders(rootFolders);
        if (isMobileViewport() && !activeNoteId) {
          setActiveMobileSurface(rootFolders.length > 0 ? 'explorer' : 'note');
        }
      } catch (err) {
        console.error('Veri çekme hatası:', err);
      }
    };
    
    fetchData();
  }, []);

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setIsCommandPaletteOpen(prev => !prev);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const handleToggleFolder = (folderId: string) => {
    setFolders(toggleFolderRecursively(folders, folderId));
    setActiveFolderId(folderId);
    if (folderId !== ROOT_NOTES_ID) {
      localStorage.setItem(LAST_USED_FOLDER_KEY, folderId);
    }
  };

  const handleSelectRoot = () => {
    setActiveFolderId(null);
    localStorage.removeItem(LAST_USED_FOLDER_KEY);
  };

  const handleSelectNote = (noteId: string) => {
    setActiveNoteId(noteId);
    const note = findNote(folders, noteId);
    if (note?.folderId) {
      setActiveFolderId(note.folderId);
      rememberFolder(note.folderId);
    } else {
      setActiveFolderId(ROOT_NOTES_ID);
    }
    if (!openNoteIds.includes(noteId)) {
      setOpenNoteIds(prev => [...prev, noteId]);
    }
    setActiveMobileSurface('note');
  };

  const findFirstRealFolderId = (items: Folder[]): string | null => {
    for (const folder of items) {
      if (folder.id !== ROOT_NOTES_ID) {
        return folder.id;
      }

      const nestedFolderId = findFirstRealFolderId(folder.folders || []);
      if (nestedFolderId) return nestedFolderId;
    }

    return null;
  };

  const rememberFolder = (folderId: string | null) => {
    if (folderId && folderId !== ROOT_NOTES_ID) {
      localStorage.setItem(LAST_USED_FOLDER_KEY, folderId);
    }
  };

  const getRootNotesFolder = () => folders.find(folder => folder.id === ROOT_NOTES_ID);

  const moveNoteToFolder = async (note: Note, folderId: string) => {
    await apiClient.put(`/notes/${note.id}`, {
      title: note.title || 'İsimsiz',
      content: note.content || '',
      folderId,
      tagIds: note.tagIds ?? []
    });
  };

  const materializeRootNotesFolder = async () => {
    const rootNotesFolder = getRootNotesFolder();
    if (!rootNotesFolder) {
      return null;
    }

    const folderName = rootNotesFolder.name.trim() || DEFAULT_ROOT_NOTES_NAME;
    const response = await apiClient.post('/folders', { name: folderName, parentFolderId: null });
    const realFolderId = response.id;
    const movedNotes = rootNotesFolder.notes.map(note => ({ ...note, folderId: realFolderId }));
    const realFolder: Folder = {
      id: realFolderId,
      name: response.name,
      isOpen: true,
      notes: movedNotes,
      folders: [],
      parentFolderId: response.parentFolderId
    };

    await Promise.all(rootNotesFolder.notes.map(note => moveNoteToFolder(note, realFolderId)));

    setFolders(prev => [realFolder, ...prev.filter(folder => folder.id !== ROOT_NOTES_ID)]);
    setActiveFolderId(realFolderId);
    rememberFolder(realFolderId);
    localStorage.removeItem(ROOT_NOTES_NAME_KEY);

    return realFolderId;
  };

  const resolveExistingTargetFolderId = () => {
    if (activeFolderId && activeFolderId !== ROOT_NOTES_ID && findFolder(folders, activeFolderId)) {
      return activeFolderId;
    }

    const storedFolderId = localStorage.getItem(LAST_USED_FOLDER_KEY);
    if (storedFolderId && findFolder(folders, storedFolderId)) {
      return storedFolderId;
    }

    return findFirstRealFolderId(folders);
  };

  const createDefaultFolder = async () => {
    const response = await apiClient.post('/folders', { name: DEFAULT_FOLDER_NAME, parentFolderId: null });
    const newFolder: Folder = {
      id: response.id,
      name: response.name,
      isOpen: true,
      notes: [],
      folders: [],
      parentFolderId: response.parentFolderId
    };

    setFolders(prev => [...prev, newFolder]);
    setActiveFolderId(newFolder.id);
    rememberFolder(newFolder.id);

    return newFolder.id;
  };

  const ensureTargetFolderId = async () => {
    if (activeFolderId === ROOT_NOTES_ID) {
      const materializedFolderId = await materializeRootNotesFolder();
      if (materializedFolderId) return materializedFolderId;
    }

    const existingFolderId = resolveExistingTargetFolderId();
    if (existingFolderId) {
      rememberFolder(existingFolderId);
      return existingFolderId;
    }

    const rootNotesFolder = getRootNotesFolder();
    if (rootNotesFolder && rootNotesFolder.notes.length > 0) {
      const materializedFolderId = await materializeRootNotesFolder();
      if (materializedFolderId) return materializedFolderId;
    }

    return createDefaultFolder();
  };

  const handleCloseTab = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const newOpenNoteIds = openNoteIds.filter(noteId => noteId !== id);
    setOpenNoteIds(newOpenNoteIds);
    if (activeNoteId === id) {
      setActiveNoteId(newOpenNoteIds.length > 0 ? newOpenNoteIds[newOpenNoteIds.length - 1] : null);
    }
  };

  const handleCreateNote = async () => {
    try {
      const targetFolderId = await ensureTargetFolderId();
      const response = await apiClient.post('/notes', { title: 'Yeni Not', content: '', folderId: targetFolderId, tagIds: [] });
      
      const newNote: Note = {
        id: response.id,
        title: response.title,
        tags: [],
        tagIds: [],
        content: response.content,
        folderId: response.folderId,
        vectorSyncStatus: response.vectorSyncStatus,
        vectorSyncError: response.vectorSyncError,
        vectorSyncedAtUtc: response.vectorSyncedAtUtc
      };
      
      rememberFolder(targetFolderId);
      setFolders(prev => addNoteToFolder(prev, targetFolderId, newNote));
      setActiveNoteId(newNote.id);
      setOpenNoteIds(prev => [...prev, newNote.id]);
      setActiveMobileSurface('note');
    } catch(err) { console.error('Not oluşturma hatası:', err); }
  };

  const handleCreateFolder = async () => {
    try {
      const targetParentId = activeFolderId && activeFolderId !== ROOT_NOTES_ID ? activeFolderId : null;
      const response = await apiClient.post('/folders', { name: DEFAULT_FOLDER_NAME, parentFolderId: targetParentId });
      
      const newFolder: Folder = {
        id: response.id,
        name: response.name,
        isOpen: true,
        notes: [],
        folders: [],
        parentFolderId: response.parentFolderId
      };
      
      if (targetParentId) {
        setFolders(prev => addFolderToParent(prev, targetParentId, newFolder));
      } else {
        setFolders(prev => [...prev, newFolder]);
      }
      setActiveFolderId(newFolder.id);
      rememberFolder(newFolder.id);
      setEditingFolderId(newFolder.id);
    } catch(err) { console.error('Klasör oluşturma hatası:', err); }
  };

  const handleOpenUploadModal = async () => {
    try {
      const targetFolderId = await ensureTargetFolderId();
      setUploadTargetFolderId(targetFolderId);
      setIsUploadModalOpen(true);
    } catch (err) {
      console.error('Yükleme hedef klasörü hazırlanırken hata oluştu:', err);
    }
  };

  const handleOpenVoiceRecorder = async () => {
    try {
      const targetFolderId = await ensureTargetFolderId();
      setVoiceTargetFolderId(targetFolderId);
      setIsVoiceRecorderOpen(true);
    } catch (err) {
      console.error('Ses kaydı hedef klasörü hazırlanırken hata oluştu:', err);
    }
  };

  const handleSaveVoiceNote = (title: string, folderId: string, audioBlob: Blob) => {
    // Ses kaydı kaydetme modalı içinde zaten upload işlemi var (bunu KnowledgeIngestionModal/VoiceRecorderModal içine alacağız)
    // Bu sadece fallback olarak duruyor. Asıl API çağrısı Modal içinde yapılacak ve buraya not dönülecek.
    const audioUrl = URL.createObjectURL(audioBlob);
    const newNote: Note = {
        id: `n${Date.now()}`,
        title: title,
        tags: ['Sesli'],
        tagIds: [],
        content: '<p>Sesli notunuz işleniyor...</p>',
        audioUrl
      };
    if (folderId && folderId !== ROOT_NOTES_ID) {
      rememberFolder(folderId);
      setFolders(addNoteToFolder(folders, folderId, newNote));
    } else {
       setFolders(prev => {
          const rootFolderIndex = prev.findIndex(f => f.id === ROOT_NOTES_ID);
          if (rootFolderIndex >= 0) {
            const newFolders = [...prev];
            newFolders[rootFolderIndex] = { ...newFolders[rootFolderIndex], notes: [newNote, ...newFolders[rootFolderIndex].notes] };
            return newFolders;
          } else {
            return [{ id: ROOT_NOTES_ID, name: localStorage.getItem(ROOT_NOTES_NAME_KEY) || DEFAULT_ROOT_NOTES_NAME, isOpen: true, notes: [newNote], folders: [] }, ...prev];
          }
        });
    }
    setActiveNoteId(newNote.id);
    setOpenNoteIds(prev => [...prev, newNote.id]);
    setActiveMobileSurface('note');
  };

  // Yeni note objesini doğrudan Dashboard'a ekleyen metod (Upload modalları için)
  const handleAddNewNoteDirectly = (newNote: Note) => {
    if (newNote.folderId && newNote.folderId !== ROOT_NOTES_ID) {
      rememberFolder(newNote.folderId);
      setFolders(prev => addNoteToFolder(prev, newNote.folderId!, newNote));
    } else {
      setFolders(prev => {
        const rootFolderIndex = prev.findIndex(f => f.id === ROOT_NOTES_ID);
        if (rootFolderIndex >= 0) {
          const newFolders = [...prev];
          newFolders[rootFolderIndex] = { ...newFolders[rootFolderIndex], notes: [newNote, ...newFolders[rootFolderIndex].notes] };
          return newFolders;
        } else {
          return [{ id: ROOT_NOTES_ID, name: localStorage.getItem(ROOT_NOTES_NAME_KEY) || DEFAULT_ROOT_NOTES_NAME, isOpen: true, notes: [newNote], folders: [] }, ...prev];
        }
      });
    }
    setActiveNoteId(newNote.id);
    setOpenNoteIds(prev => {
        if(!prev.includes(newNote.id)) return [...prev, newNote.id];
        return prev;
    });
    setActiveMobileSurface('note');
  }

  const handleUpdateNote = async (id: string, updates: Partial<Note>) => {
    // Optimistic update
    setFolders(prev => updateNoteInFolder(prev, id, updates));
    
    try {
      const currentNote = findNote(folders, id);
      if (currentNote) {
        const response = await apiClient.put(`/notes/${id}`, {
          title: updates.title !== undefined ? updates.title : currentNote.title,
          content: updates.content !== undefined ? updates.content : currentNote.content,
          folderId: updates.folderId !== undefined ? updates.folderId : currentNote.folderId,
          tagIds: currentNote.tagIds ?? []
        });
        setFolders(prev => updateNoteInFolder(prev, id, {
          vectorSyncStatus: response.vectorSyncStatus,
          vectorSyncError: response.vectorSyncError,
          vectorSyncedAtUtc: response.vectorSyncedAtUtc
        }));
      }
    } catch(err) {
      console.error('Not güncellenirken hata oluştu:', err);
    }
  };
  
  const handleUpdateFolderName = async (id: string, name: string) => {
    if (id === ROOT_NOTES_ID) {
      localStorage.setItem(ROOT_NOTES_NAME_KEY, name);
      setFolders(prev => updateFolderName(prev, id, name));
      return;
    }

    const currentFolder = findFolder(folders, id);
    setFolders(prev => updateFolderName(prev, id, name));
    try {
      await apiClient.put(`/folders/${id}`, { name, parentFolderId: currentFolder?.parentFolderId ?? null });
    } catch(err) { console.error('Klasör güncellenirken hata oluştu:', err); }
  };

  const handleMoveNote = async (noteId: string, targetFolderId: string | null) => {
    const currentNote = findNote(folders, noteId);
    if (!currentNote) return;

    const normalizedTargetId = targetFolderId === ROOT_NOTES_ID ? null : targetFolderId;
    if ((currentNote.folderId ?? null) === normalizedTargetId) {
      return;
    }

    const previousFolders = folders;
    const rootFolderName = localStorage.getItem(ROOT_NOTES_NAME_KEY) || DEFAULT_ROOT_NOTES_NAME;

    setFolders(prev => moveNoteInFolderTree(prev, noteId, normalizedTargetId, ROOT_NOTES_ID, rootFolderName).folders);
    if (activeNoteId === noteId) {
      setActiveFolderId(normalizedTargetId ?? ROOT_NOTES_ID);
    }
    if (normalizedTargetId) {
      rememberFolder(normalizedTargetId);
    }

    try {
      const response = await apiClient.put(`/notes/${noteId}`, {
        title: currentNote.title || 'İsimsiz',
        content: currentNote.content || '',
        folderId: normalizedTargetId,
        tagIds: currentNote.tagIds ?? []
      });

      setFolders(prev => updateNoteInFolder(prev, noteId, {
        folderId: response.folderId ?? undefined,
        durationSeconds: response.durationSeconds,
        vectorSyncStatus: response.vectorSyncStatus,
        vectorSyncError: response.vectorSyncError,
        vectorSyncedAtUtc: response.vectorSyncedAtUtc
      }));
    } catch (err) {
      console.error('Not taşınırken hata oluştu:', err);
      setFolders(previousFolders);
    }
  };

  const handleMoveFolder = async (folderId: string, targetParentId: string | null) => {
    if (folderId === ROOT_NOTES_ID) return;

    const currentFolder = findFolder(folders, folderId);
    if (!currentFolder) return;

    const normalizedParentId = targetParentId === ROOT_NOTES_ID ? null : targetParentId;
    if ((currentFolder.parentFolderId ?? null) === normalizedParentId) {
      return;
    }

    if (normalizedParentId && (normalizedParentId === folderId || isFolderDescendant(folders, folderId, normalizedParentId))) {
      return;
    }

    const previousFolders = folders;
    setFolders(prev => moveFolderInFolderTree(prev, folderId, normalizedParentId, ROOT_NOTES_ID).folders);
    setActiveFolderId(folderId);
    rememberFolder(folderId);

    try {
      await apiClient.put(`/folders/${folderId}`, {
        name: currentFolder.name,
        parentFolderId: normalizedParentId
      });
    } catch (err) {
      console.error('Klasör taşınırken hata oluştu:', err);
      setFolders(previousFolders);
    }
  };
  
  const handleDeleteNotePrompt = (id: string, name: string) => {
    setDeleteModalState({ isOpen: true, itemId: id, itemName: name, itemType: 'note' });
  };

  const handleDeleteFolderPrompt = (id: string, name: string) => {
    setDeleteModalState({ isOpen: true, itemId: id, itemName: name, itemType: 'folder' });
  };
  
  const handleDeleteConfirm = async () => {
    const idToDelete = deleteModalState.itemId;
    try {
      if (deleteModalState.itemType === 'note') {
        await apiClient.delete(`/notes/${idToDelete}`);
        setFolders(prev => deleteNoteFromFolder(prev, idToDelete));
        setOpenNoteIds(prev => {
          const newIds = prev.filter(id => id !== idToDelete);
          if (activeNoteId === idToDelete) {
            setActiveNoteId(newIds.length > 0 ? newIds[newIds.length - 1] : null);
          }
          return newIds;
        });
      } else {
        await apiClient.delete(`/folders/${idToDelete}`);
        setFolders(prev => deleteFolder(prev, idToDelete));
        if (activeFolderId === idToDelete) {
          setActiveFolderId(null);
        }
      }
    } catch (err) {
      console.error('Silme hatası:', err);
    } finally {
      setDeleteModalState({ ...deleteModalState, isOpen: false });
    }
  };

  useEffect(() => {
    const currentNoteIds = new Set(getAllNotes(folders).map(n => n.id));
    setOpenNoteIds(prev => {
      const newIds = prev.filter(id => currentNoteIds.has(id));
      if (prev.length !== newIds.length) {
        if (activeNoteId && !currentNoteIds.has(activeNoteId)) {
           setActiveNoteId(newIds.length > 0 ? newIds[newIds.length - 1] : null);
        }
        return newIds;
      }
      return prev;
    });
  }, [folders]);

  const activeNote = activeNoteId ? findNote(folders, activeNoteId) : null;
  const activeNotePath = activeNoteId ? findNotePath(folders, activeNoteId) : undefined;
  const folderPathStr = activeNotePath ? activeNotePath.join(' / ') : 'çalışma alanı';
  const mobileNavItems = [
    { id: 'explorer' as const, label: 'Gezgin', icon: FolderOpen },
    { id: 'note' as const, label: 'Not', icon: FileText },
    { id: 'ai' as const, label: 'AI', icon: Bot },
  ];

  const renderOpenTabs = () => openNoteIds.length > 0 && (
    <div className="flex items-center h-10 border-b ns-hairline shrink-0 px-2 overflow-x-auto no-scrollbar gap-1 pt-1">
      {openNoteIds.map(id => {
        const note = findNote(folders, id);
        if (!note) return null;
        const isActive = activeNoteId === id;
        return (
          <div
            key={id}
            onClick={() => setActiveNoteId(id)}
            className={`group relative flex h-8 min-w-[7.5rem] max-w-[70vw] cursor-pointer select-none items-center gap-2 rounded-t-lg border-b-2 px-3 text-[11px] font-medium transition-colors md:min-w-0 md:max-w-[200px] md:text-xs ${
              isActive
                ? 'bg-ns-bg-secondary/70 text-ns-text-primary border-ns-primary'
                : 'text-ns-text-secondary hover:bg-ns-surface-hover/50 hover:text-ns-text-primary border-transparent'
            }`}
          >
            {(() => {
              const Icon = note.fileType === 'audio' ? Headphones : note.fileType === 'pdf' ? BookOpen : FileText;
              return <Icon className="w-3.5 h-3.5 shrink-0 opacity-70" />;
            })()}
            <span className="truncate pr-4">{note.title || 'İsimsiz'}</span>
            <button
              onClick={(e) => handleCloseTab(id, e)}
              className={`absolute right-1.5 p-0.5 rounded-sm hover:bg-ns-divider transition-all shrink-0 ${
                isActive ? 'opacity-100' : 'opacity-0 group-hover:opacity-100'
              }`}
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        );
      })}
    </div>
  );

  const renderWorkspaceContent = () => activeNote ? (
    activeNote.fileType === 'pdf' ? (
      <PdfViewer key={activeNote.id} note={activeNote} folderPathStr={folderPathStr} onUpdate={handleUpdateNote} />
    ) : activeNote.fileType === 'audio' ? (
      <AudioViewer key={activeNote.id} note={activeNote} folderPathStr={folderPathStr} onUpdate={handleUpdateNote} />
    ) : (
      <Editor key={activeNote.id} note={activeNote} onUpdate={handleUpdateNote} folderPathStr={folderPathStr} />
    )
  ) : (
    <EmptyState
      onCreateNote={handleCreateNote}
      onUpload={handleOpenUploadModal}
      onRecordVoiceNote={handleOpenVoiceRecorder}
    />
  );

  return (
    <div className="flex h-[100dvh] w-full overflow-hidden bg-ns-bg-primary relative md:h-screen">
      <div className="hidden h-full w-full md:flex">
        {isSidebarOpen ? (
          <>
            <Sidebar
              width={sidebarWidth}
              folders={folders}
              activeNoteId={activeNoteId}
              activeFolderId={activeFolderId}
              editingFolderId={editingFolderId}
              onClearEditingFolder={() => setEditingFolderId(null)}
              onSelectNote={handleSelectNote}
              onToggleFolder={handleToggleFolder}
              onCreateNote={handleCreateNote}
              onCreateFolder={handleCreateFolder}
              onUpdateFolderName={handleUpdateFolderName}
              onDeleteNote={handleDeleteNotePrompt}
              onDeleteFolder={handleDeleteFolderPrompt}
              onSelectRoot={handleSelectRoot}
              onMoveNote={handleMoveNote}
              onMoveFolder={handleMoveFolder}
              onRecordVoiceNote={handleOpenVoiceRecorder}
              onUploadPdf={handleOpenUploadModal}
              onOpenSettings={onOpenSettings}
              onSearch={() => setIsCommandPaletteOpen(true)}
              onCollapse={() => setIsSidebarOpen(false)}
            />
            <div
              className="ns-panel-divider hidden w-2 cursor-col-resize transition-colors z-30 shrink-0 md:block"
              onMouseDown={(e) => {
                e.preventDefault();
                const startX = e.clientX;
                const startWidth = sidebarWidth;
                const onMouseMove = (e: MouseEvent) => {
                  const newWidth = Math.max(200, Math.min(startWidth + (e.clientX - startX), 600));
                  setSidebarWidth(newWidth);
                };
                const onMouseUp = () => {
                  document.removeEventListener('mousemove', onMouseMove);
                  document.removeEventListener('mouseup', onMouseUp);
                  document.body.style.cursor = 'default';
                  document.body.style.userSelect = '';
                };
                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';
              }}
            />
          </>
        ) : (
          <div className="hidden md:flex relative z-10 w-12 h-full shrink-0 flex-col bg-ns-bg-secondary/70 backdrop-blur-xl items-center py-3">
            <button
              onClick={() => setIsSidebarOpen(true)}
              className="p-2 text-ns-text-secondary hover:text-ns-text-primary hover:bg-ns-surface-hover/70 rounded-lg transition-colors"
              title="Gezgini Aç"
            >
              <PanelLeft className="w-5 h-5" />
            </button>
          </div>
        )}

        <div className="flex-1 flex flex-col min-w-0 bg-ns-bg-primary w-full h-full relative">
          {renderOpenTabs()}
          <Suspense fallback={<WorkspaceFallback />}>
            {renderWorkspaceContent()}
          </Suspense>
        </div>

        {isAIAssistantOpen ? (
          <>
            <div
              className="ns-panel-divider hidden w-2 cursor-col-resize transition-colors z-30 shrink-0 md:block"
              onMouseDown={(e) => {
                e.preventDefault();
                const startX = e.clientX;
                const startWidth = aiWidth;
                const onMouseMove = (e: MouseEvent) => {
                  const newWidth = Math.max(300, Math.min(startWidth - (e.clientX - startX), 800));
                  setAiWidth(newWidth);
                };
                const onMouseUp = () => {
                  document.removeEventListener('mousemove', onMouseMove);
                  document.removeEventListener('mouseup', onMouseUp);
                  document.body.style.cursor = 'default';
                  document.body.style.userSelect = '';
                };
                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';
              }}
            />
            <Suspense fallback={<AiPanelFallback width={aiWidth} />}>
              <AIAssistant
                width={aiWidth}
                onCollapse={() => setIsAIAssistantOpen(false)}
                onSelectNote={handleSelectNote}
              />
            </Suspense>
          </>
        ) : (
          <div className="hidden md:flex relative z-10 w-12 h-full shrink-0 flex-col bg-ns-bg-secondary/70 backdrop-blur-xl items-center py-3 gap-4">
            <button
              onClick={() => setIsAIAssistantOpen(true)}
              className="p-2 text-ns-text-secondary hover:text-ns-text-primary hover:bg-ns-surface-hover/70 rounded-lg transition-colors"
              title="Yapay Zeka'yı Aç"
            >
              <PanelRight className="w-5 h-5" />
            </button>
          </div>
        )}
      </div>

      <div className="flex h-full w-full flex-col md:hidden">
        <div className="relative min-h-0 flex-1 overflow-hidden pb-[calc(4.5rem+env(safe-area-inset-bottom))]">
          {activeMobileSurface === 'explorer' && (
            <Sidebar
              mobileFullScreen
              folders={folders}
              activeNoteId={activeNoteId}
              activeFolderId={activeFolderId}
              editingFolderId={editingFolderId}
              onClearEditingFolder={() => setEditingFolderId(null)}
              onSelectNote={handleSelectNote}
              onToggleFolder={handleToggleFolder}
              onCreateNote={handleCreateNote}
              onCreateFolder={handleCreateFolder}
              onUpdateFolderName={handleUpdateFolderName}
              onDeleteNote={handleDeleteNotePrompt}
              onDeleteFolder={handleDeleteFolderPrompt}
              onSelectRoot={handleSelectRoot}
              onMoveNote={handleMoveNote}
              onMoveFolder={handleMoveFolder}
              onRecordVoiceNote={handleOpenVoiceRecorder}
              onUploadPdf={handleOpenUploadModal}
              onOpenSettings={onOpenSettings}
              onSearch={() => setIsCommandPaletteOpen(true)}
              onCollapse={() => setActiveMobileSurface('note')}
            />
          )}

          {activeMobileSurface === 'note' && (
            <div className="flex h-full min-w-0 flex-col bg-ns-bg-primary">
              {renderOpenTabs()}
              <Suspense fallback={<WorkspaceFallback />}>
                {renderWorkspaceContent()}
              </Suspense>
            </div>
          )}

          {activeMobileSurface === 'ai' && (
            <Suspense fallback={<WorkspaceFallback />}>
              <AIAssistant
                mobileFullScreen
                onCollapse={() => setActiveMobileSurface('note')}
                onSelectNote={handleSelectNote}
              />
            </Suspense>
          )}
        </div>

        <nav className="fixed inset-x-0 bottom-0 z-40 border-t border-ns-border/60 bg-ns-bg-secondary/95 px-3 pb-[calc(env(safe-area-inset-bottom)+0.55rem)] pt-2 backdrop-blur-xl">
          <div className="mx-auto grid max-w-md grid-cols-3 gap-2">
            {mobileNavItems.map(item => {
              const Icon = item.icon;
              const isActive = activeMobileSurface === item.id;
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => setActiveMobileSurface(item.id)}
                  className={`flex min-h-12 flex-col items-center justify-center gap-1 rounded-2xl border px-2 text-[11px] font-semibold transition-colors ${
                    isActive
                      ? 'border-ns-primary/30 bg-ns-primary/10 text-ns-primary'
                      : 'border-transparent text-ns-text-muted hover:bg-ns-surface-hover/70 hover:text-ns-text-primary'
                  }`}
                >
                  <Icon className="h-[18px] w-[18px]" strokeWidth={isActive ? 2.4 : 2} />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </div>
        </nav>
      </div>

      <Suspense fallback={null}>
        <CommandPalette
          isOpen={isCommandPaletteOpen}
          onClose={() => setIsCommandPaletteOpen(false)}
          onSelectAction={(action) => {
            if (action === 'upload') handleOpenUploadModal();
            if (action === 'create') handleCreateNote();
          }}
        />

        <KnowledgeIngestionModal
          isOpen={isUploadModalOpen}
          onClose={() => setIsUploadModalOpen(false)}
          onNoteCreated={handleAddNewNoteDirectly}
          targetFolderId={uploadTargetFolderId}
        />

        <VoiceRecorderModal
          isOpen={isVoiceRecorderOpen}
          onClose={() => setIsVoiceRecorderOpen(false)}
          onSave={handleSaveVoiceNote}
          onNoteCreated={handleAddNewNoteDirectly}
          folders={folders}
          preferredFolderId={voiceTargetFolderId}
        />
      </Suspense>

      <DeleteConfirmModal 
        isOpen={deleteModalState.isOpen}
        onClose={() => setDeleteModalState({ ...deleteModalState, isOpen: false })}
        onConfirm={handleDeleteConfirm}
        itemName={deleteModalState.itemName}
        itemType={deleteModalState.itemType}
      />
    </div>
  );
};
