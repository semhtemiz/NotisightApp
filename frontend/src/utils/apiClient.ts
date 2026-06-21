import { buildApiUrl, getAuthHeaders } from './apiConfig';
import { writeStoredUser } from './currentUser';

class ApiClient {
  private setTokens(accessToken: string, refreshToken: string) {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
  }

  private clearTokens() {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    writeStoredUser(null);
  }

  private getHeaders(includeJsonContentType: boolean, extraHeaders?: HeadersInit): Headers {
    const headers = new Headers(getAuthHeaders(includeJsonContentType));

    if (extraHeaders) {
      new Headers(extraHeaders).forEach((value, key) => headers.set(key, value));
    }

    return headers;
  }

  private buildRequestHeaders(options: RequestInit): Headers {
    const hasBody = options.body !== undefined && options.body !== null;
    const isFormData = typeof FormData !== 'undefined' && options.body instanceof FormData;

    return this.getHeaders(hasBody && !isFormData, options.headers);
  }

  private async buildError(response: Response, fallback: string): Promise<Error> {
    try {
      const data = await response.json();
      const message = data.detail || data.message || data.title || fallback;
      return new Error(message);
    } catch {
      try {
        const text = await response.text();
        return new Error(text || fallback);
      } catch {
        return new Error(fallback);
      }
    }
  }

  private async refreshAccessToken(): Promise<boolean> {
    const refreshToken = localStorage.getItem('refreshToken');
    if (!refreshToken) return false;

    try {
      const response = await fetch(buildApiUrl('/auth/refresh'), {
        method: 'POST',
        headers: this.getHeaders(true),
        body: JSON.stringify({ refreshToken }),
      });

      if (response.ok) {
        const data = await response.json();
        this.setTokens(data.accessToken, data.refreshToken);
        return true;
      }
      return false;
    } catch {
      return false;
    }
  }

  async fetchWithAuth(endpoint: string, options: RequestInit = {}): Promise<Response> {
    const url = buildApiUrl(endpoint);
    let response = await fetch(url, {
      ...options,
      headers: this.buildRequestHeaders(options),
    });

    if (response.status === 401) {
      const refreshed = await this.refreshAccessToken();
      if (refreshed) {
        response = await fetch(url, {
          ...options,
          headers: this.buildRequestHeaders(options),
        });
      } else {
        this.clearTokens();
        window.dispatchEvent(new Event('auth:unauthorized'));
      }
    }

    return response;
  }

  async get(endpoint: string) {
    const response = await this.fetchWithAuth(endpoint, { method: 'GET' });
    if (!response.ok) throw await this.buildError(response, `GET ${endpoint} failed`);
    return response.json();
  }

  async post(endpoint: string, body: any) {
    const response = await this.fetchWithAuth(endpoint, {
      method: 'POST',
      body: JSON.stringify(body),
    });
    if (!response.ok) throw await this.buildError(response, `POST ${endpoint} failed`);
    return response.json();
  }

  async put(endpoint: string, body: any) {
    const response = await this.fetchWithAuth(endpoint, {
      method: 'PUT',
      body: JSON.stringify(body),
    });
    if (!response.ok) throw await this.buildError(response, `PUT ${endpoint} failed`);
    if (response.status === 204) return null;
    return response.json();
  }

  async delete(endpoint: string) {
    const response = await this.fetchWithAuth(endpoint, { method: 'DELETE' });
    if (!response.ok) throw await this.buildError(response, `DELETE ${endpoint} failed`);
    return response.status === 204 ? null : response.json();
  }
}

export const apiClient = new ApiClient();
