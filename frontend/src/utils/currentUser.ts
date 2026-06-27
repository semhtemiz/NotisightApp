export interface StoredAuthUser {
  id?: string;
  username?: string;
  displayName?: string;
  email?: string;
}

export const CURRENT_USER_STORAGE_KEY = 'notisight_current_user';
export const CURRENT_USER_CHANGED_EVENT = 'auth:user-changed';

export const getDisplayUserName = (user: StoredAuthUser | null | undefined) => {
  const username = user?.username?.trim();
  if (username) return username;

  const displayName = user?.displayName?.trim();
  if (displayName) return displayName;

  const emailName = user?.email?.split('@')[0]?.trim();
  return emailName || 'Kullanıcı';
};

export const readStoredUser = (): StoredAuthUser | null => {
  try {
    const rawUser = localStorage.getItem(CURRENT_USER_STORAGE_KEY);
    return rawUser ? JSON.parse(rawUser) : null;
  } catch {
    return null;
  }
};

export const writeStoredUser = (user: StoredAuthUser | null | undefined) => {
  if (user) {
    localStorage.setItem(CURRENT_USER_STORAGE_KEY, JSON.stringify(user));
  } else {
    localStorage.removeItem(CURRENT_USER_STORAGE_KEY);
  }

  window.dispatchEvent(new Event(CURRENT_USER_CHANGED_EVENT));
};
