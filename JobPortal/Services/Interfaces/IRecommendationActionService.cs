using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IRecommendationActionService
    {
        Task<TransitionResult> SubmitStage1Async(int applicationId, int userId);
        Task<TransitionResult> SubmitStage2Async(int applicationId, int userId);
    }
}
