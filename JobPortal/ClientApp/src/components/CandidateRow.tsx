import React from 'react';
import type { UnifiedCandidate } from '../types/UnifiedCandidate';
import { StageBadge } from './StageBadge';

interface CandidateRowProps {
  candidate: UnifiedCandidate;
}


function formatAppliedDate(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('en-AU', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  });
}

export function CandidateRow({ candidate: c }: CandidateRowProps) {
  // Link to the most recent application (first ID in the ordered list)
  const primaryId = c.applicationIds[0];

  return (
    <tr>
      {/* Candidate name + email */}
      <td className="ps-4 py-3">
        <a
          href={`/Admin/CandidateDetails/${primaryId}`}
          className="fw-semibold text-decoration-none"
          style={{ color: '#1E1765' }}
        >
          {c.name}
        </a>
        {c.email && <div className="text-muted small">{c.email}</div>}
      </td>

      {/* Most recent active stage */}
      <td className="py-3">
        <StageBadge stage={c.currentStage} />
      </td>

      {/* Active application count */}
      <td className="py-3">
        {c.activeApplicationCount > 1 ? (
          <span className="badge bg-info text-dark">
            {c.activeApplicationCount} Applications
          </span>
        ) : (
          <span className="text-muted small">1 Application</span>
        )}
      </td>

      {/* Latest applied date */}
      <td className="py-3 text-muted small">{formatAppliedDate(c.latestAppliedDate)}</td>

      {/* Actions */}
      <td className="py-3">
        <a
          href={`/Admin/CandidateDetails/${primaryId}`}
          className="btn btn-sm btn-outline-primary"
        >
          <i className="fas fa-eye me-1" />
          View
        </a>
      </td>
    </tr>
  );
}
