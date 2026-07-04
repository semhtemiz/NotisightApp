import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import path from 'path';
import {defineConfig, loadEnv} from 'vite';

const LOCAL_API_URL_PATTERN = /^https?:\/\/(localhost|127\.0\.0\.1|\[::1\])(?::\d+)?(?:\/|$)/i;
const API_PROXY_TARGET = 'http://localhost:5228';
const API_PROXY_PREFIXES = ['/api', '/auth', '/ai', '/notes', '/folders', '/tags', '/health'];

const validateProductionApiUrl = (mode: string, apiUrl?: string) => {
  if (mode !== 'production') {
    return;
  }

  if (!apiUrl?.trim()) {
    throw new Error('VITE_API_URL must be configured for production builds.');
  }

  if (LOCAL_API_URL_PATTERN.test(apiUrl)) {
    throw new Error('VITE_API_URL cannot point to localhost in production builds.');
  }
};

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  validateProductionApiUrl(mode, env.VITE_API_URL);

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, '.'),
      },
    },
    server: {
      hmr: process.env.DISABLE_HMR !== 'true',
      watch: process.env.DISABLE_HMR === 'true' ? null : {},
      proxy: Object.fromEntries(
        API_PROXY_PREFIXES.map(prefix => [
          prefix,
          {
            target: API_PROXY_TARGET,
            changeOrigin: true,
            secure: false,
          },
        ])
      ),
    },
    build: {
      chunkSizeWarningLimit: 700,
      rollupOptions: {
        output: {
          manualChunks(id) {
            if (!id.includes('node_modules')) {
              return undefined;
            }

            if (id.includes('node_modules/@tiptap') || id.includes('node_modules/prosemirror')) {
              return 'editor-vendor';
            }

            if (id.includes('node_modules/react-pdf') || id.includes('node_modules/pdfjs-dist')) {
              return 'pdf-viewer-vendor';
            }

            return undefined;
          },
        },
      },
    },
  };
});
