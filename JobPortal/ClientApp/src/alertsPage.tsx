import React from 'react';
import { createRoot } from 'react-dom/client';
import { AlertsPage } from './components/AlertsPage';

const container = document.getElementById('alerts-page-root');
if (container) {
  createRoot(container).render(
    <React.StrictMode>
      <AlertsPage />
    </React.StrictMode>,
  );
}
