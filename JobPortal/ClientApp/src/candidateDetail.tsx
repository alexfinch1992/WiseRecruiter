import React from 'react';
import { createRoot } from 'react-dom/client';
import { CandidateActionPanel } from './components/CandidateActionPanel';

const el = document.getElementById('stage-move-root');
if (el) {
  const { applicationId, jobId, currentStage, stage1Status } = el.dataset;
  console.log('React mounting to:', el);
  createRoot(el).render(
    <React.StrictMode>
      <CandidateActionPanel
        applicationId={Number(applicationId)}
        jobId={Number(jobId)}
        currentStage={currentStage ?? ''}
        stage1RecommendationStatus={stage1Status ?? null}
        onStageChanged={() => window.location.reload()}
        onStage1StatusChanged={() => {}}
      />
    </React.StrictMode>,
  );
}
