import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CandidateDashboard } from './CandidateDashboard';
import type { CandidateApplication } from '../types/CandidateApplication';

// ---------------------------------------------------------------------------
// Fixture data — three candidates; only Bob is non-compliant
// ---------------------------------------------------------------------------

const mockCandidates: CandidateApplication[] = [
  {
    applicationId: 1,
    jobId: 1,
    fullName: 'Alice Johnson',
    email: 'alice@example.com',
    jobTitle: 'Senior Software Engineer',
    currentStage: 'Interview',
    appliedDate: '2026-03-01T00:00:00Z',
    isNonCompliant: false,
    stage1RecommendationStatus: null,
  },
  {
    applicationId: 2,
    jobId: 1,
    fullName: 'Bob Smith',
    email: 'bob@example.com',
    jobTitle: 'Full Stack Developer',
    currentStage: 'Applied',
    appliedDate: '2026-03-02T00:00:00Z',
    isNonCompliant: true, // ← the one non-compliant candidate
    stage1RecommendationStatus: null,
  },
  {
    applicationId: 3,
    jobId: 1,
    fullName: 'Carol Williams',
    email: 'carol@example.com',
    jobTitle: 'Senior Software Engineer',
    currentStage: 'Offer',
    appliedDate: '2026-03-03T00:00:00Z',
    isNonCompliant: false,
    stage1RecommendationStatus: null,
  },
];

const stageOptions = [
  { id: 'enum:Applied', label: 'Applied', isCustom: false, isGated: false, weight: 0,   requiresRecommendation: false },
  { id: 'enum:Screen',  label: 'Screen',  isCustom: false, isGated: false, weight: 10,  requiresRecommendation: false },
  { id: 'enum:Rejected', label: 'Rejected', isCustom: false, isGated: true, weight: 100, requiresRecommendation: false },
];

const noRec = { status: 'None', notes: null, strengths: null, concerns: null, hireRecommendation: null };

function mockFetch(candidatesData: unknown) {
  global.fetch = vi.fn().mockImplementation((url: string) => {
    if (url.includes('GetAvailableStages')) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(stageOptions) } as unknown as Response);
    }
    if (url.includes('GetStage1RecJson') || url.includes('GetStage2RecJson')) {
      return Promise.resolve({ ok: true, json: () => Promise.resolve(noRec) } as unknown as Response);
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(candidatesData) } as unknown as Response);
  });
}

beforeEach(() => {
  mockFetch(mockCandidates);
});

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('CandidateDashboard', () => {
  // (a) Correct number of rows
  it('renders the correct number of table rows', async () => {
    render(<CandidateDashboard />);

    // Wait until at least the first candidate name appears
    await waitFor(() => screen.getByText('Alice Johnson'));

    // One header row + one body row per candidate
    const rows = screen.getAllByRole('row');
    expect(rows).toHaveLength(mockCandidates.length + 1);
  });

  // (b) Non-compliant warning icon appears only for flagged candidates
  it('shows the non-compliant icon ONLY for isNonCompliant candidates', async () => {
    render(<CandidateDashboard />);
    await waitFor(() => screen.getByText('Bob Smith'));

    // Bob is non-compliant — his table cell should contain the warning icon
    const bobCell = screen.getByText('Bob Smith').closest('td')!;
    expect(bobCell.querySelector('.fa-exclamation-triangle')).toBeTruthy();

    // Alice and Carol are compliant — their cells must NOT contain the icon
    const aliceCell = screen.getByText('Alice Johnson').closest('td')!;
    expect(aliceCell.querySelector('.fa-exclamation-triangle')).toBeNull();

    const carolCell = screen.getByText('Carol Williams').closest('td')!;
    expect(carolCell.querySelector('.fa-exclamation-triangle')).toBeNull();
  });

  // (c) Search input filters the displayed rows
  it('filters visible rows when the user types in the search box', async () => {
    const user = userEvent.setup();
    render(<CandidateDashboard />);
    await waitFor(() => screen.getByText('Alice Johnson'));

    const searchBox = screen.getByPlaceholderText(/filter by candidate name or email/i);
    await user.type(searchBox, 'bob');

    // Only Bob should remain visible
    expect(screen.getByText('Bob Smith')).toBeInTheDocument();
    expect(screen.queryByText('Alice Johnson')).not.toBeInTheDocument();
    expect(screen.queryByText('Carol Williams')).not.toBeInTheDocument();
  });
});
