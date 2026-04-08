/**
 * Mirrors the JSON returned by GET /Admin/GetStage1RecJson and /Admin/GetStage2RecJson.
 */
export interface RecData {
  status: string;
  notes: string | null;
  strengths: string | null;
  concerns: string | null;
  hireRecommendation: boolean | null;
}

/**
 * Mirrors the JSON returned by POST /Admin/SaveRecDraftJson,
 * /Admin/SaveStage2RecDraftJson, and /Admin/SubmitStage2RecJson.
 */
export interface RecActionResult {
  success: boolean;
  status: string;
}

/**
 * Mirrors the JSON returned by POST /Recommendation/SubmitStage1.
 */
export interface SubmitResult {
  success: boolean;
  error?: string;
}
