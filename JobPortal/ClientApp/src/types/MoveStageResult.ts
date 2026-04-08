/**
 * Mirrors the JSON returned by POST /Admin/MoveApplicationStageJson.
 */
export interface MoveStageResult {
  success: boolean;
  requiresApproval: boolean;
  newStage: string;
}
