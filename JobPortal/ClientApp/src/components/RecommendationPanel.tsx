import React, { useEffect, useState } from 'react';

interface RecData {
  status: string; // 'None' | 'Draft' | 'Submitted' | 'Approved' | 'Rejected'
  notes: string | null;
  strengths: string | null;
  concerns: string | null;
  hireRecommendation: boolean | null;
}

interface RecommendationPanelProps {
  applicationId: number;
  currentStage: string;
  /** Live Stage 1 status — kept in sync with parent via onStage1StatusChanged */
  stage1Status: string | null;
  onStage1StatusChanged: (status: string) => void;
  /** 1 = Stage 1 editor (default), 2 = Stage 2 editor */
  stage?: 1 | 2;
}

const STATUS_BADGE: Record<string, string> = {
  None:      'bg-secondary',
  Draft:     'bg-secondary',
  Submitted: 'bg-warning text-dark',
  Approved:  'bg-success',
  Rejected:  'bg-danger',
};

function statusBadge(status: string) {
  return STATUS_BADGE[status] ?? 'bg-secondary';
}

// ── Small fieldset for one rec form ──────────────────────────────────────────
interface RecFormProps {
  label: string;
  data: RecData;
  saving: boolean;
  error: string | null;
  onFieldChange: (patch: Partial<RecData>) => void;
  onSaveDraft: () => void;
  onSubmit: () => void;
  fieldLabels?: { notes: string; strengths: string; concerns: string; hireRec: string };
}

function RecForm({ label, data, saving, error, onFieldChange, onSaveDraft, onSubmit, fieldLabels }: RecFormProps) {
  const lbl = fieldLabels ?? { notes: 'Notes / summary', strengths: 'Strengths', concerns: 'Concerns', hireRec: 'Recommend to hire' };
  const isApproved = data.status === 'Approved';
  return (
    <div>
      <div className="d-flex justify-content-between align-items-center mb-1">
        <strong style={{ fontSize: '0.75rem' }}>{label}</strong>
        <span className={`badge ${statusBadge(data.status)}`} style={{ fontSize: '0.68rem' }}>
          {data.status}
        </span>
      </div>

      {!isApproved && (
        <>
          <textarea
            className="form-control form-control-sm mb-1"
            rows={2}
            placeholder={lbl.notes}
            value={data.notes ?? ''}
            onChange={(e) => onFieldChange({ notes: e.target.value })}
            style={{ fontSize: '0.75rem', resize: 'vertical' }}
          />
          <input
            type="text"
            className="form-control form-control-sm mb-1"
            placeholder={lbl.strengths}
            value={data.strengths ?? ''}
            onChange={(e) => onFieldChange({ strengths: e.target.value })}
            style={{ fontSize: '0.75rem' }}
          />
          <input
            type="text"
            className="form-control form-control-sm mb-1"
            placeholder={lbl.concerns}
            value={data.concerns ?? ''}
            onChange={(e) => onFieldChange({ concerns: e.target.value })}
            style={{ fontSize: '0.75rem' }}
          />
          <div className="form-check mb-1" style={{ fontSize: '0.75rem' }}>
            <input
              className="form-check-input"
              type="checkbox"
              id={`hire-rec-${label}`}
              checked={data.hireRecommendation ?? false}
              onChange={(e) => onFieldChange({ hireRecommendation: e.target.checked })}
            />
            <label className="form-check-label" htmlFor={`hire-rec-${label}`}>
              {lbl.hireRec}
            </label>
          </div>

          <div className="d-flex gap-1 flex-wrap">
            <button
              type="button"
              className="btn btn-sm btn-outline-secondary"
              onClick={onSaveDraft}
              disabled={saving}
              style={{ fontSize: '0.72rem' }}
            >
              {saving ? '…' : 'Save Draft'}
            </button>
            {data.status === 'Draft' && (
              <button
                type="button"
                className="btn btn-sm btn-outline-primary"
                onClick={onSubmit}
                disabled={saving}
                style={{ fontSize: '0.72rem' }}
              >
                {saving ? '…' : 'Submit for Approval'}
              </button>
            )}
          </div>

          {error && (
            <div className="text-danger mt-1" style={{ fontSize: '0.7rem' }}>
              {error}
            </div>
          )}
        </>
      )}

      {isApproved && (
        <div className="text-success" style={{ fontSize: '0.72rem' }}>
          ✓ Approved — no further edits allowed.
        </div>
      )}
    </div>
  );
}

// ── Main RecommendationPanel ──────────────────────────────────────────────────
export function RecommendationPanel({
  applicationId,
  currentStage,
  stage1Status,
  onStage1StatusChanged,
  stage = 1,
}: RecommendationPanelProps) {
  // In stage-2 editor mode, skip S1 entirely and show S2 directly
  const showS1 = stage === 1 && (currentStage === 'Screen' || (stage1Status !== null && stage1Status !== 'None'));
  const showS2 = stage === 2 || stage1Status === 'Approved';

  const [s1, setS1] = useState<RecData | null>(null);
  const [s2, setS2] = useState<RecData | null>(null);
  const [s1Saving, setS1Saving] = useState(false);
  const [s2Saving, setS2Saving] = useState(false);
  const [s1Error, setS1Error] = useState<string | null>(null);
  const [s2Error, setS2Error] = useState<string | null>(null);

  // Fetch S1 data when panel becomes visible
  useEffect(() => {
    if (!showS1) return;
    fetch(`/Admin/GetStage1RecJson?applicationId=${applicationId}`)
      .then((r) => (r.ok ? (r.json() as Promise<RecData>) : null))
      .then((data) => data && setS1(data))
      .catch(() => {});
  }, [applicationId, showS1]);

  // Fetch S2 data when S1 becomes approved OR we are in S2 editor mode
  useEffect(() => {
    if (!showS2) return;
    fetch(`/Admin/GetStage2RecJson?applicationId=${applicationId}`)
      .then((r) => (r.ok ? (r.json() as Promise<RecData>) : null))
      .then((data) => {
        if (data) setS2(data);
        else setS2({ status: 'None', notes: null, strengths: null, concerns: null, hireRecommendation: null });
      })
      .catch(() => {});
  }, [applicationId, showS2]);

  // ── Stage 1 handlers ────────────────────────────────────────────────────────
  async function saveS1Draft() {
    if (!s1) return;
    setS1Saving(true);
    setS1Error(null);
    try {
      const resp = await fetch('/Admin/SaveRecDraftJson', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          applicationId: String(applicationId),
          notes: s1.notes ?? '',
          strengths: s1.strengths ?? '',
          concerns: s1.concerns ?? '',
          ...(s1.hireRecommendation !== null ? { hireRecommendation: String(s1.hireRecommendation) } : {}),
        }),
      });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const result = (await resp.json()) as { success: boolean; status: string };
      setS1((prev) => (prev ? { ...prev, status: result.status } : prev));
      onStage1StatusChanged(result.status);
    } catch {
      setS1Error('Save failed. Please try again.');
    } finally {
      setS1Saving(false);
    }
  }

  async function submitS1() {
    console.log('submitS1 fired');
    setS1Saving(true);
    setS1Error(null);
    try {
      const token =
        document.querySelector<HTMLInputElement>('[name="__RequestVerificationToken"]')?.value ?? '';
      const resp = await fetch('/Recommendation/SubmitStage1', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ applicationId: String(applicationId), __RequestVerificationToken: token }),
      });
      const data = await resp.json() as { success: boolean; error?: string };
      console.log('SubmitStage1:', resp.status, data);
      if (resp.ok && data.success) {
        window.location.href = `/Admin/CandidateDetails/${applicationId}`;
      } else {
        setS1Error(data.error ?? 'Submit failed. Please try again.');
      }
    } catch {
      setS1Error('Submit failed. Please try again.');
    } finally {
      setS1Saving(false);
    }
  }

  // ── Stage 2 handlers ────────────────────────────────────────────────────────
  async function saveS2Draft() {
    if (!s2) return;
    setS2Saving(true);
    setS2Error(null);
    try {
      const resp = await fetch('/Admin/SaveStage2RecDraftJson', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({
          applicationId: String(applicationId),
          notes: s2.notes ?? '',
          strengths: s2.strengths ?? '',
          concerns: s2.concerns ?? '',
          ...(s2.hireRecommendation !== null ? { hireRecommendation: String(s2.hireRecommendation) } : {}),
        }),
      });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const result = (await resp.json()) as { success: boolean; status: string };
      setS2((prev) => (prev ? { ...prev, status: result.status } : prev));
    } catch {
      setS2Error('Save failed. Please try again.');
    } finally {
      setS2Saving(false);
    }
  }

  async function submitS2() {
    setS2Saving(true);
    setS2Error(null);
    try {
      const resp = await fetch('/Admin/SubmitStage2RecJson', {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        body: new URLSearchParams({ applicationId: String(applicationId) }),
      });
      if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
      const result = (await resp.json()) as { success: boolean; status: string };
      setS2((prev) => (prev ? { ...prev, status: result.status } : prev));
      if (result.status === 'Submitted' || result.status === 'Approved') {
        window.location.href = `/Admin/CandidateDetails/${applicationId}`;
      }
    } catch {
      setS2Error('Submit failed. Please try again.');
    } finally {
      setS2Saving(false);
    }
  }

  if (stage === 2 && !showS2) return null;
  if (stage === 1 && !showS1) return null;

  const s2FieldLabels = {
    notes:     'Technical Assessment Notes',
    strengths: 'Proposed Salary Range / Compensation Notes',
    concerns:  'Technical Concerns / Gaps',
    hireRec:   'Recommend for Offer',
  };

  return (
    <div
      className="border-top mt-2 pt-2"
      style={{ fontSize: '0.78rem' }}
      onClick={(e) => e.stopPropagation()}
    >
      {/* Stage 1 */}
      {showS1 && (
        s1 ? (
          <RecForm
            label="Stage 1 Recommendation"
            data={s1}
            saving={s1Saving}
            error={s1Error}
            onFieldChange={(patch) => setS1((prev) => (prev ? { ...prev, ...patch } : prev))}
            onSaveDraft={saveS1Draft}
            onSubmit={submitS1}
          />
        ) : (
          <div className="text-muted" style={{ fontSize: '0.72rem' }}>Loading recommendation…</div>
        )
      )}

      {/* Stage 2 */}
      {showS2 && s2 && (
        <div className={showS1 ? 'border-top mt-2 pt-2' : ''}>
          <RecForm
            label="Stage 2 Recommendation"
            data={s2}
            saving={s2Saving}
            error={s2Error}
            fieldLabels={s2FieldLabels}
            onFieldChange={(patch) => setS2((prev) => (prev ? { ...prev, ...patch } : prev))}
            onSaveDraft={saveS2Draft}
            onSubmit={submitS2}
          />
        </div>
      )}
    </div>
  );
}
