import React, { useEffect, useState } from 'react';
import type { UnifiedCandidate } from '../types/UnifiedCandidate';
import { CandidateRow } from './CandidateRow';

// ---------------------------------------------------------------------------
// Loading / error states
// ---------------------------------------------------------------------------

function LoadingSpinner() {
  return (
    <div className="text-center py-5">
      <div className="spinner-border" style={{ color: '#7B3FF2' }} role="status">
        <span className="visually-hidden">Loading...</span>
      </div>
      <p className="text-muted mt-3 small">Loading candidates...</p>
    </div>
  );
}

function ErrorAlert({ message }: { message: string }) {
  return (
    <div className="alert alert-danger d-flex align-items-center gap-2" role="alert">
      <i className="fas fa-exclamation-circle" />
      <span>Failed to load candidates: {message}</span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Container -- owns fetch, filtering state, and table chrome
// ---------------------------------------------------------------------------

export function CandidateDashboard() {
  const [candidates, setCandidates] = useState<UnifiedCandidate[]>([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/Admin/GetCandidatesJson')
      .then((res) => {
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return res.json() as Promise<CandidateApplication[]>;
      })
      .then((data) => {
        setCandidates(data as UnifiedCandidate[]);
        setLoading(false);
      })
      .catch((err: Error) => {
        setError(err.message);
        setLoading(false);
      });
  }, []);

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorAlert message={error} />;

  const q = search.toLowerCase();
  const filtered = q
    ? candidates.filter(
        (c) =>
          c.name.toLowerCase().includes(q) ||
          c.email.toLowerCase().includes(q),
      )
    : candidates;

  return (
    <div>
      {/* Page header */}
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="fw-bold mb-0">
          <i className="fas fa-users me-2" style={{ color: '#7B3FF2' }} />
          Candidates
        </h2>
        <span className="text-muted small">
          {filtered.length} result{filtered.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Search bar */}
      <div className="card shadow-sm border-0 mb-4">
        <div className="card-body py-3">
          <div className="d-flex gap-2">
            <input
              type="text"
              className="form-control form-control-sm"
              placeholder="Filter by candidate name or email..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              autoComplete="off"
            />
            {search && (
              <button
                className="btn btn-sm btn-outline-secondary"
                onClick={() => setSearch('')}
              >
                Clear
              </button>
            )}
          </div>
        </div>
      </div>

      {/* Empty state */}
      {filtered.length === 0 ? (
        <div className="text-center text-muted py-5">
          <i className="fas fa-user-slash fa-2x mb-3 d-block" style={{ color: '#adb5bd' }} />
          <p>No candidates found{search ? ` matching "${search}"` : ''}.</p>
        </div>
      ) : (
        <div className="card shadow-sm border-0">
          <div className="table-responsive">
            <table className="table table-hover mb-0 align-middle">
              <thead style={{ background: 'linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%)' }}>
                <tr>
                  <th className="text-white fw-semibold py-3 ps-4">Candidate</th>
                  <th className="text-white fw-semibold py-3">Latest Stage</th>
                  <th className="text-white fw-semibold py-3">Applications</th>
                  <th className="text-white fw-semibold py-3">Last Applied</th>
                  <th className="text-white fw-semibold py-3">Actions</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((c, rowIndex) => (
                  <CandidateRow
                    key={c.primaryApplicationId}
                    candidate={c}
                    ids={filtered.map((f) => f.primaryApplicationId)}
                    idx={rowIndex}
                  />
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}