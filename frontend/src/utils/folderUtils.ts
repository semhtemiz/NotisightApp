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

export const moveNoteInFolderTree = (
  folders: Folder[],
  noteId: string,
  targetFolderId: string | null,
  rootFolderId: string,
  rootFolderName: string
): { folders: Folder[]; note?: Note } => {
  let movedNote: Note | undefined;

  const removeNote = (items: Folder[]): Folder[] =>
    items.map(folder => {
      const existingNote = folder.notes.find(note => note.id === noteId);
      if (existingNote) {
        movedNote = existingNote;
        return { ...folder, notes: folder.notes.filter(note => note.id !== noteId) };
      }

      return {
        ...folder,
        folders: folder.folders ? removeNote(folder.folders) : folder.folders
      };
    });

  const withoutNote = removeNote(folders);
  if (!movedNote) {
    return { folders };
  }

  const normalizedTargetId = targetFolderId === rootFolderId ? null : targetFolderId;
  const updatedNote = { ...movedNote, folderId: normalizedTargetId ?? undefined };

  if (normalizedTargetId) {
    return {
      folders: addNoteToFolder(withoutNote, normalizedTargetId, updatedNote),
      note: updatedNote
    };
  }

  const rootIndex = withoutNote.findIndex(folder => folder.id === rootFolderId);
  if (rootIndex >= 0) {
    const nextFolders = [...withoutNote];
    const rootFolder = nextFolders[rootIndex];
    nextFolders[rootIndex] = {
      ...rootFolder,
      isOpen: true,
      notes: [updatedNote, ...rootFolder.notes]
    };
    return { folders: nextFolders, note: updatedNote };
  }

  return {
    folders: [
      {
        id: rootFolderId,
        name: rootFolderName,
        isOpen: true,
        notes: [updatedNote],
        folders: []
      },
      ...withoutNote
    ],
    note: updatedNote
  };
};

export const isFolderDescendant = (
  folders: Folder[],
  folderId: string,
  possibleDescendantId: string
): boolean => {
  const folder = findFolder(folders, folderId);
  if (!folder?.folders) {
    return false;
  }

  const stack = [...folder.folders];
  while (stack.length > 0) {
    const current = stack.pop()!;
    if (current.id === possibleDescendantId) {
      return true;
    }
    if (current.folders) {
      stack.push(...current.folders);
    }
  }

  return false;
};

export const moveFolderInFolderTree = (
  folders: Folder[],
  folderId: string,
  targetParentId: string | null,
  rootFolderId: string
): { folders: Folder[]; folder?: Folder } => {
  let movedFolder: Folder | undefined;

  const removeFolder = (items: Folder[]): Folder[] =>
    items
      .filter(folder => {
        if (folder.id === folderId) {
          movedFolder = folder;
          return false;
        }
        return true;
      })
      .map(folder => ({
        ...folder,
        folders: folder.folders ? removeFolder(folder.folders) : folder.folders
      }));

  const withoutFolder = removeFolder(folders);
  if (!movedFolder) {
    return { folders };
  }

  const normalizedParentId = targetParentId === rootFolderId ? null : targetParentId;
  const updatedFolder = {
    ...movedFolder,
    parentFolderId: normalizedParentId,
    isOpen: true
  };

  if (!normalizedParentId) {
    return { folders: [...withoutFolder, updatedFolder], folder: updatedFolder };
  }

  return {
    folders: addFolderToParent(withoutFolder, normalizedParentId, updatedFolder),
    folder: updatedFolder
  };
};
