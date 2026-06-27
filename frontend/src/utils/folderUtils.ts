import { Folder, Note } from '../types';

export const findNote = (folders: Folder[], noteId: string): Note | undefined => {
  for (const folder of folders) {
    const note = folder.notes.find(n => n.id === noteId);
    if (note) return note;
    if (folder.folders) {
      const foundInSub = findNote(folder.folders, noteId);
      if (foundInSub) return foundInSub;
    }
  }
  return undefined;
};

export const findFolder = (folders: Folder[], folderId: string): Folder | undefined => {
  for (const folder of folders) {
    if (folder.id === folderId) return folder;
    if (folder.folders) {
      const foundInSub = findFolder(folder.folders, folderId);
      if (foundInSub) return foundInSub;
    }
  }
  return undefined;
};

export const findNotePath = (folders: Folder[], noteId: string): string[] | undefined => {
  for (const folder of folders) {
    if (folder.notes.some(n => n.id === noteId)) {
      return [folder.name];
    }
    if (folder.folders) {
      const nestedPath = findNotePath(folder.folders, noteId);
      if (nestedPath) {
        return [folder.name, ...nestedPath];
      }
    }
  }
  return undefined;
};

export const getAllNotes = (folders: Folder[]): Note[] => {
  let notes: Note[] = [];
  for (const folder of folders) {
    notes = [...notes, ...folder.notes];
    if (folder.folders) {
      notes = [...notes, ...getAllNotes(folder.folders)];
    }
  }
  return notes;
};

export const toggleFolderRecursively = (folders: Folder[], folderId: string): Folder[] => {
  return folders.map(f => {
    if (f.id === folderId) {
      return { ...f, isOpen: !f.isOpen };
    }
    if (f.folders) {
      return { ...f, folders: toggleFolderRecursively(f.folders, folderId) };
    }
    return f;
  });
};

export const addNoteToFolder = (folders: Folder[], folderId: string, note: Note): Folder[] => {
  return folders.map(f => {
    if (f.id === folderId) {
      return { ...f, notes: [note, ...f.notes], isOpen: true };
    }
    if (f.folders) {
      return { ...f, folders: addNoteToFolder(f.folders, folderId, note) };
    }
    return f;
  });
};

export const addFolderToParent = (folders: Folder[], parentId: string, newFolder: Folder): Folder[] => {
  return folders.map(f => {
    if (f.id === parentId) {
      return { ...f, folders: [...(f.folders || []), newFolder], isOpen: true };
    }
    if (f.folders) {
      return { ...f, folders: addFolderToParent(f.folders, parentId, newFolder) };
    }
    return f;
  });
};

export const updateNoteInFolder = (folders: Folder[], noteId: string, updates: Partial<Note>): Folder[] => {
  return folders.map(f => {
    const hasNote = f.notes.some(n => n.id === noteId);
    if (hasNote) {
      return {
        ...f,
        notes: f.notes.map(n => n.id === noteId ? { ...n, ...updates } : n)
      };
    }
    if (f.folders) {
      return { ...f, folders: updateNoteInFolder(f.folders, noteId, updates) };
    }
    return f;
  });
};

export const updateFolderName = (folders: Folder[], folderId: string, name: string): Folder[] => {
  return folders.map(f => {
    if (f.id === folderId) {
      return { ...f, name };
    }
    if (f.folders) {
      return { ...f, folders: updateFolderName(f.folders, folderId, name) };
    }
    return f;
  });
};

export const deleteNoteFromFolder = (folders: Folder[], noteId: string): Folder[] => {
  return folders.map(f => {
    if (f.notes.some(n => n.id === noteId)) {
      return {
        ...f,
        notes: f.notes.filter(n => n.id !== noteId)
      };
    }
    if (f.folders) {
      return { ...f, folders: deleteNoteFromFolder(f.folders, noteId) };
    }
    return f;
  });
};

export const deleteFolder = (folders: Folder[], folderId: string): Folder[] => {
  return folders.filter(f => f.id !== folderId).map(f => {
    if (f.folders) {
      return { ...f, folders: deleteFolder(f.folders, folderId) };
    }
    return f;
  });
};
