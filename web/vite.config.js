import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// In development the app proxies /api (including the WebSocket stream) to a local server.
const server = process.env.TASKMGR_SERVER || 'http://localhost:8080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': { target: server, changeOrigin: true, ws: true },
    },
  },
  test: {
    environment: 'jsdom',
    globals: false,
  },
});
