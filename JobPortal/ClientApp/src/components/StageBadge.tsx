import React from 'react';

interface BadgeConfig {
  className: string;
  style?: React.CSSProperties;
}

const STAGE_BADGE: Record<string, BadgeConfig> = {
  Applied:   { className: 'badge text-white', style: { background: '#7B3FF2' } },
  Screen:    { className: 'badge text-white', style: { background: '#7B3FF2' } },
  Interview: { className: 'badge text-white', style: { background: '#7B3FF2' } },
  Offer:     { className: 'badge bg-warning text-dark' },
  Hired:     { className: 'badge bg-success' },
  Rejected:  { className: 'badge bg-danger' },
};

interface StageBadgeProps {
  stage: string;
  isNonCompliant?: boolean;
}

export function StageBadge({ stage, isNonCompliant = false }: StageBadgeProps) {
  const cfg = STAGE_BADGE[stage] ?? { className: 'badge bg-secondary' };
  return (
    <>
      <span className={cfg.className} style={cfg.style ?? {}}>
        {stage}
      </span>
      {isNonCompliant && (
        <i
          className="fas fa-exclamation-triangle ms-1"
          style={{ color: '#f59e0b', fontSize: '0.75rem' }}
          title="Non-compliant application"
        />
      )}
    </>
  );
}
