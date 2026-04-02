import React from 'react';
import { createRoot } from 'react-dom/client';
import { AlertBell } from './components/AlertBell';

const container = document.getElementById('alerts-bell-root');
if (container) {
  createRoot(container).render(
    <React.StrictMode>
      <AlertBell />
    </React.StrictMode>,
  );
}
