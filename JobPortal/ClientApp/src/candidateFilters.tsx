import React from 'react';
import { createRoot } from 'react-dom/client';
import CandidateFilterSidebar from './components/CandidateFilterSidebar';

const el = document.getElementById('candidate-filter-root');

if (el && (window as any).__candidateFilters) {
    createRoot(el).render(
        <React.StrictMode>
            <CandidateFilterSidebar initial={(window as any).__candidateFilters} />
        </React.StrictMode>
    );
}
