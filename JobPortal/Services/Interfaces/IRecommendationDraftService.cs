using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IRecommendationDraftService
    {
        Task<TransitionResult> SaveStage1DraftAsync(
            int    applicationId,
            string? notes,
            string? strengths,
            string? concerns,
            bool?   hireRecommendation);

        Task<TransitionResult> SaveStage2DraftAsync(
            int    applicationId,
            string? notes,
            string? strengths,
            string? concerns,
            bool?   hireRecommendation);
    }
}
