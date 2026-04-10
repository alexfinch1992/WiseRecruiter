import React from 'react';
import { createRoot } from 'react-dom/client';
import CandidateFilterSidebar from './components/CandidateFilterSidebar';

const el = document.getElementById('candidate-filter-root');

if (el && el.dataset.initial) {
    const initial = JSON.parse(el.dataset.initial);
    createRoot(el).render(
        <React.StrictMode>
            <CandidateFilterSidebar initial={initial} />
        </React.StrictMode>
    );
}
