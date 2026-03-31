import React, { useEffect, useState } from 'react';
import type { StageOption } from '../types/StageOption';

interface CandidateActionPanelProps {
  applicationId: number;
  jobId: number;
  currentStage: string;
  stage1RecommendationStatus: string | null;
  onStageChanged: (newStage: string) => void;
  onStage1StatusChanged: (status: string) => void;
}

interface MoveResult {
  success: boolean;
  requiresApproval: boolean;
  message?: string;
  newStage: string;
}

export function CandidateActionPanel({
  applicationId,
  jobId,
  currentStage,
  stage1RecommendationStatus,
  onStageChanged,
  onStage1StatusChanged,
}: CandidateActionPanelProps) {
  const [stages, setStages] = useState<StageOption[]>([]);
  const [selectedId, setSelectedId] = useState('');
  const [moving, setMoving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch(`/Admin/GetAvailableStages?jobId=${jobId}`)
      .then((r) => r.json() as Promise<StageOption[]>)
      .then((data) => {
        setStages(data);
        if (data.length > 0) setSelectedId(data[0].id);
      })
      .catch(() => {});
  }, [jobId]);

  const selectedOption = stages.find((s) => s.id === selectedId);
  const isHardGate   = (selectedOption?.isGated ?? false) && !(selectedOption?.requiresRecommendation ?? false); // Rejected only
  const requiresRec  = selectedOption?.requiresRecommendation ?? false;
  const recCompleted = stage1RecommendationStatus === 'Submitted' || stage1RecommendationStatus === 'Approved';
  const showSoftGate = requiresRec && !recCompleted;
  const [hardConfirmed, setHardConfirmed] = useState(false);
  const [bypassConfirmed, setBypassConfirmed] = useState(false);
  const canMove = !!selectedId && (!isHardGate || hardConfirmed);

  function handleChange(id: string) {
    setSelectedId(id);
    setHardConfirmed(false);
    setBypassConfirmed(false);
    setError(null);
  }

  async function doMove(proceedWithoutApproval: boolean) {
    setMoving(true);
    setError(null);
    try {
      const resp = await fetch('/Admin/MoveApplicationStageJson', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          applicationId: String(applicationId),
          selectedStage: selectedId,
          proceedWithoutApproval: String(proceedWithoutApproval),
        }),
      });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const result = (await resp.json()) as MoveResult;
      onStageChanged(result.newStage);
      setHardConfirmed(false);
      setBypassConfirmed(false);
    } catch {
      setError('Could not update stage.');
    } finally {
      setMoving(false);
    }
  }

  async function handleMove(e: React.MouseEvent) {
    e.stopPropagation();
    if (!canMove) return;
    await doMove(bypassConfirmed);
  }

  if (stages.length === 0) return null; // still loading

  return (
    <div
      className="d-flex flex-column gap-1"
      style={{ minWidth: '220px' }}
      onClick={(e) => e.stopPropagation()}
    >
      {/* Unified flat stage dropdown + Move button */}
      <div className="d-flex gap-1 align-items-center">
        <select
          className="form-select form-select-sm"
          value={selectedId}
          onChange={(e) => handleChange(e.target.value)}
          style={{ fontSize: '0.8rem' }}
          aria-label="Select stage"
        >
        {stages.map((s) => (
            <option key={s.id} value={s.id}>
              {s.label}{s.requiresRecommendation && !recCompleted ? ' •' : ''}
            </option>
          ))}
        </select>
        <button
          className="btn btn-sm btn-primary"
          onClick={handleMove}
          disabled={!canMove || moving}
          style={{ fontSize: '0.78rem', whiteSpace: 'nowrap' }}
        >
          {moving ? '…' : 'Move'}
        </button>
      </div>

      {/* Soft-gate: recommendation not yet submitted */}
      {showSoftGate && (
        <div className="alert alert-warning py-2 px-3 mb-0 mt-1" style={{ fontSize: '0.75rem' }}>
          <i className="fas fa-exclamation-triangle me-1"></i>
          Stage 1 recommendation not yet submitted.
          <div className="form-check mt-1 mb-0">
            <input
              className="form-check-input"
              type="checkbox"
              id={`bypass-${applicationId}`}
              checked={bypassConfirmed}
              onChange={(e) => setBypassConfirmed(e.target.checked)}
            />
            <label className="form-check-label fw-semibold" htmlFor={`bypass-${applicationId}`}>
              Proceed without Stage 1 approval
            </label>
          </div>
        </div>
      )}

      {/* Hard-gate confirmation — Rejected only */}
      {isHardGate && (
        <div className="form-check mb-0" style={{ fontSize: '0.78rem' }}>
          <input
            className="form-check-input"
            type="checkbox"
            id={`gate-confirm-${applicationId}`}
            checked={hardConfirmed}
            onChange={(e) => setHardConfirmed(e.target.checked)}
          />
          <label
            className="form-check-label text-warning fw-semibold"
            htmlFor={`gate-confirm-${applicationId}`}
          >
            Confirm rejection
          </label>
        </div>
      )}

      {/* Error feedback */}
      {error && (
        <div className="text-danger" style={{ fontSize: '0.72rem' }}>
          {error}
        </div>
      )}

      {/* Stage 1 Recommendation — dedicated full-page editor */}
      <div className="border-top mt-2 pt-2" onClick={(e) => e.stopPropagation()}>
        {(!stage1RecommendationStatus || stage1RecommendationStatus === 'None' || stage1RecommendationStatus === 'Draft') ? (
          <a
            href={`/Admin/WriteRecommendation?applicationId=${applicationId}`}
            className="btn btn-sm btn-primary w-100"
            style={{ fontSize: '0.78rem' }}
          >
            <i className="fas fa-edit me-1"></i>Write Stage 1 Recommendation
          </a>
        ) : (
          <div className="d-flex align-items-center gap-2 flex-wrap">
            <span
              className={`badge ${
                stage1RecommendationStatus === 'Approved' ? 'bg-success' : 'bg-warning text-dark'
              }`}
              style={{ fontSize: '0.72rem' }}
            >
              {stage1RecommendationStatus === 'Approved' ? '✓ Approved' : `Status: ${stage1RecommendationStatus}`}
            </span>
            <a
              href={`/Admin/WriteRecommendation?applicationId=${applicationId}`}
              className="btn btn-sm btn-outline-primary"
              style={{ fontSize: '0.72rem' }}
            >
              View/Edit Document
            </a>
          </div>
        )}
      </div>
    </div>
  );
}
