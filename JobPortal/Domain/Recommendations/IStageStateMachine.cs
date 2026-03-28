using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.Recommendations
{
    public interface IStageStateMachine<TContext>
    {
        bool CanTransition(RecommendationStatus from, RecommendationStatus to);
        TransitionResult ApplyTransition(CandidateRecommendation entity, RecommendationStatus to, TContext context);
    }
}
