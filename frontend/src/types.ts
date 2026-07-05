export type ViewState = 'auth' | 'app' | 'settings';
export type ModalState = 'none' | 'command-palette' | 'upload';

export interface Note {
  id: string;
  title: string;
  content: string;
  tags: string[];
  tagIds?: string[];
  audioUrl?: string;
  folderId?: string;
  fileUrl?: string;
  fileType?: string;
  durationSeconds?: number;
  updatedAtUtc?: string;
  vectorSyncStatus?: string;
  vectorSyncError?: string;
  vectorSyncedAtUtc?: string;
}

export interface Folder {
  id: string;
  name: string;
  isOpen: boolean;
  notes: Note[];
  folders?: Folder[];
  parentFolderId?: string | null;
}
