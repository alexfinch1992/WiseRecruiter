/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: resolve(__dirname, '../wwwroot/js/dist'),
    emptyOutDir: true,
    rollupOptions: {
      input: {
        'candidate-dashboard':    resolve(__dirname, 'src/main.tsx'),
        'candidate-detail':       resolve(__dirname, 'src/candidateDetail.tsx'),
        'recommendation-editor':  resolve(__dirname, 'src/recommendationEditor.tsx'),
        'alert-bell':             resolve(__dirname, 'src/alertBell.tsx'),
        'alerts-page':            resolve(__dirname, 'src/alertsPage.tsx'),
        'job-alert-toggle':       resolve(__dirname, 'src/jobAlertToggle.tsx'),
      },
      output: {
        // Predictable filenames — no content hash — so Razor views can reference them directly.
        entryFileNames: '[name].js',
        assetFileNames: '[name].[ext]',
      },
    },
  },
  // Proxy /Admin/* to the running .NET server when running `vite dev`
  server: {
    proxy: {
      '/Admin': 'http://localhost:5236',
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test-setup.ts'],
    globals: true,
  },
});
