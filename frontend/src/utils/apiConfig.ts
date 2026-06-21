const DEVELOPMENT_API_URL = 'http://localhost:5228';
const LOCAL_API_URL_PATTERN = /^https?:\/\/(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?(?:\/|$)/i;

const trimTrailingSlash = (value: string) => value.replace(/\/+$/, '');

const resolveApiBaseUrl = () => {
  const configuredUrl = import.meta.env.VITE_API_URL?.trim();

  if (!configuredUrl) {
    if (import.meta.env.PROD) {
      throw new Error('VITE_API_URL must be configured for production builds.');
    }

    return DEVELOPMENT_API_URL;
  }

  if (import.meta.env.PROD && LOCAL_API_URL_PATTERN.test(configuredUrl)) {
    throw new Error('VITE_API_URL cannot point to localhost in production builds.');
  }

  return trimTrailingSlash(configuredUrl);
};

export const API_BASE_URL = resolveApiBaseUrl();

export const buildApiUrl = (endpoint: string) => {
  if (/^https?:\/\//i.test(endpoint)) {
    return endpoint;
  }

  return `${API_BASE_URL}${endpoint.startsWith('/') ? endpoint : `/${endpoint}`}`;
};

export const getAuthHeaders = (includeJsonContentType = false) => {
  const headers: Record<string, string> = {};

  if (includeJsonContentType) {
    headers['Content-Type'] = 'application/json';
  }

  const token = localStorage.getItem('accessToken');
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  return headers;
};
