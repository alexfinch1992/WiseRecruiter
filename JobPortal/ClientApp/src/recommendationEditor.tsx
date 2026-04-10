import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { RecommendationPanel } from './components/RecommendationPanel';

const el = document.getElementById('recommendation-editor-root');
if (el) {
  const { applicationId, recStatus, stage, csrfToken } = el.dataset;
  const stageNum = (Number(stage) || 1) as 1 | 2;

  function RecommendationEditorApp() {
    const [status, setStatus] = useState(recStatus ?? 'None');
    return (
      <div style={{ fontSize: '1rem' }}>
        <RecommendationPanel
          applicationId={Number(applicationId)}
          currentStage="Screen"
          stage1Status={stageNum === 1 ? status : null}
          onStage1StatusChanged={setStatus}
          stage={stageNum}
          csrfToken={csrfToken ?? ''}
        />
      </div>
    );
  }

  createRoot(el).render(
    <React.StrictMode>
      <RecommendationEditorApp />
    </React.StrictMode>,
  );
}
