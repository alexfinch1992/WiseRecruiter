import React from 'react';
import { createRoot } from 'react-dom/client';
import { CandidateDashboard } from './components/CandidateDashboard';

const container = document.getElementById('candidate-dashboard-root');
if (container) {
  createRoot(container).render(
    <React.StrictMode>
      <CandidateDashboard />
    </React.StrictMode>,
  );
}
