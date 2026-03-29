using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IRecommendationService
    {
        Task<(CandidateRecommendation? Recommendation, bool IsApproved)> GetOrPrepareStage1RecommendationAsync(
            int applicationId,
            bool proceedWithoutApproval,
            string? bypassReason,
            string userId);

        /// <summary>
        /// Returns the Stage 1 recommendation context for an application.
        /// Returns null when the application does not exist.
        /// </summary>
        Task<(CandidateRecommendation? Rec, string? CandidateName, string? JobTitle)?> GetStage1ContextAsync(int applicationId);

        /// <summary>
        /// Creates or updates the Stage 1 recommendation as Draft.
        /// Submitted → Draft is valid (editing flow).
        /// Approved → Draft is blocked (InvalidState).
        /// Returns NotFound when the application does not exist.
        /// </summary>
        Task<TransitionResult> SaveStage1DraftAsync(int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation);

        /// <summary>
        /// Transitions a Draft Stage 1 recommendation to Submitted.
        /// Returns NotFound when no recommendation exists.
        /// Returns InvalidState for any invalid transition (e.g. Approved → Submitted).
        /// </summary>
        Task<TransitionResult> SubmitStage1RecommendationAsync(int applicationId, int userId);

        /// <summary>
        /// Returns all recommendations (any stage) that are awaiting approval (Status == Submitted).
        /// </summary>
        Task<List<PendingRecommendationDto>> GetPendingRecommendationsAsync();

        /// <summary>
        /// Returns the full review context for a Stage 1 recommendation, or null when the application does not exist.
        /// </summary>
        Task<Stage1ReviewViewModel?> GetStage1ReviewAsync(int applicationId);

        /// <summary>
        /// Returns the full review context for a Stage 2 recommendation, or null when the application does not exist.
        /// </summary>
        Task<Stage2ReviewViewModel?> GetStage2ReviewAsync(int applicationId);

        /// <summary>
        /// Marks the Stage 1 recommendation as Approved and records the approver.
        /// Returns NotFound when no Submitted recommendation exists.
        /// Returns AlreadyApproved when already approved.
        /// Returns Approved on success.
        /// </summary>
        Task<ApprovalResult> ApproveStage1RecommendationAsync(int applicationId, int userId, string? approvalFeedback = null);

        /// <summary>
        /// Marks the Stage 2 recommendation as Approved. Manager-only.
        /// Returns Forbidden when the caller is not authorized.
        /// Returns NotFound when no Stage 2 recommendation exists.
        /// Returns AlreadyApproved when already approved.
        /// Returns Approved on success.
        /// </summary>
        Task<ApprovalResult> ApproveStage2RecommendationAsync(int applicationId, int userId, string? approvalFeedback = null);

        Task<(CandidateRecommendation? Rec, string? CandidateName, string? JobTitle)?> GetStage2ContextAsync(int applicationId);
        Task<TransitionResult> SaveStage2DraftAsync(int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation);
        Task<TransitionResult> SubmitStage2RecommendationAsync(int applicationId, int userId);
    }
}
