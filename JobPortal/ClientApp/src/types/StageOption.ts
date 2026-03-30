/**
 * Mirrors the C# StageOptionDto returned by GET /Admin/GetAvailableStages.
 */
export interface StageOption {
  /** Encoded stage id: "enum:Applied" | "enum:Screen" | ... | "stage:{jobStageId}" */
  id: string;
  label: string;
  /** True when the option represents a custom JobStage rather than a system enum value */
  isCustom: boolean;
  /** True when selecting this stage should show a hard-gate confirmation (e.g. Rejected) */
  isGated: boolean;
  /** Sort weight: Applied=0, Screen=10, Custom=20+, Interview=50, Offer=80, Hired=90, Rejected=100 */
  weight: number;
  /** True when a Stage 1 Recommendation approval is required (or bypassed) to move here */
  requiresRecommendation: boolean;
}
