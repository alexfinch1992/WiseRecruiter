/**
 * Mirrors the C# CandidateApplicationDto returned by GET /Admin/GetCandidatesJson.
 * Property names are camelCase — ASP.NET Core's default JSON serializer policy.
 */
export interface CandidateApplication {
  applicationId: number;
  jobId: number;
  fullName: string;
  email: string | null;
  jobTitle: string | null;
  /** Stringified ApplicationStage enum: "Applied" | "Screen" | "Interview" | "Offer" | "Hired" | "Rejected" */
  currentStage: string;
  /** ISO 8601 date-time string */
  appliedDate: string;
  /** Placeholder — reserved for compliance checks; currently always false */
  isNonCompliant: boolean;
  /** Status of the Stage 1 recommendation for this application, or null if none exists */
  stage1RecommendationStatus: string | null;
}
