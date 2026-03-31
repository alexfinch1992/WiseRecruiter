using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public record RecommendationSummaryResult(
        List<CandidateRecommendation> Recommendations,
        bool RequiresStage1ApprovalWarning);

    public interface IRecommendationSummaryService
    {
        /// <summary>
        /// Loads this application's recommendations ordered Stage1 first, then Stage2, and
        /// determines whether the Stage1 approval warning should be shown.
        /// </summary>
        Task<RecommendationSummaryResult> GetRecommendationSummaryAsync(int applicationId);
    }
}
