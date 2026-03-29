using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.Recommendations
{
    public class Stage2StateMachine : IStageStateMachine<Stage2TransitionContext>
    {
        public bool CanTransition(RecommendationStatus from, RecommendationStatus to) =>
            (from, to) switch
            {
                (RecommendationStatus.Draft,      RecommendationStatus.Draft)      => true,
                (RecommendationStatus.Draft,      RecommendationStatus.Submitted)  => true,
                (RecommendationStatus.Submitted,  RecommendationStatus.Draft)      => true,
                (RecommendationStatus.Submitted,  RecommendationStatus.Approved)   => true,
                _ => false
            };

        public TransitionResult ApplyTransition(CandidateRecommendation rec, RecommendationStatus to, Stage2TransitionContext ctx)
        {
            if (!CanTransition(rec.Status, to))
                return TransitionResult.InvalidState;

            var now = DateTime.UtcNow;

            switch (to)
            {
                case RecommendationStatus.Draft:
                    rec.Status = RecommendationStatus.Draft;
                    rec.Summary = ctx.Notes;
                    rec.ExperienceFit = ctx.Strengths;
                    rec.Concerns = ctx.Concerns;
                    rec.HireRecommendation = ctx.HireRecommendation;
                    rec.LastUpdatedUtc = now;
                    break;

                case RecommendationStatus.Submitted:
                    if (!ctx.UserId.HasValue)
                        throw new InvalidOperationException("UserId is required for the Submitted transition.");
                    rec.Status = RecommendationStatus.Submitted;
                    rec.SubmittedByUserId = ctx.UserId.Value;
                    rec.SubmittedUtc = now;
                    rec.LastUpdatedUtc = now;
                    break;

                case RecommendationStatus.Approved:
                    if (!ctx.UserId.HasValue)
                        throw new InvalidOperationException("UserId is required for the Approved transition.");
                    rec.Status = RecommendationStatus.Approved;
                    rec.ReviewedByUserId = ctx.UserId.Value;
                    rec.ReviewedUtc = now;
                    rec.LastUpdatedUtc = now;
                    rec.BypassedApproval = false;
                    rec.BypassReason = null;
                    break;
            }

            return TransitionResult.Success;
        }
    }
}
