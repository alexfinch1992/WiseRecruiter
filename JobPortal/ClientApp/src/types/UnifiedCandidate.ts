/**
 * Mirrors the C# UnifiedCandidateDto returned by GET /Admin/GetCandidatesJson
 * after the Candidate Unification refactor.
 *
 * One record per unique email address. Multiple applications are collapsed into
 * `applicationIds` and summarised via `activeApplicationCount`.
 */
export interface UnifiedCandidate {
  /** The candidate's email — the unification key. */
  email: string;
  /** Full name from the most recent application. */
  name: string;
  /** Application ID used for navigation links (most recent application). */
  primaryApplicationId: number;
  /** All application IDs associated with this email. */
  applicationIds: number[];
  /** Count of applications that are NOT in a terminal stage (Rejected / Hired). */
  activeApplicationCount: number;
  /** Stage of the most recent active application (for at-a-glance display). */
  currentStage: string;
  /** ISO 8601 date-time of the most recent application. */
  latestAppliedDate: string;
}
