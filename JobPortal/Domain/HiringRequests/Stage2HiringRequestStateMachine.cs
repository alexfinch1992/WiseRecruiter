using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.HiringRequests
{
    /// <summary>
    /// Governs Stage 2 transitions, reviewed by the Senior Executive.
    /// The entity enters Stage 2 at Draft after the Stage 1 Approved transition;
    /// the service layer is responsible for advancing Stage and resetting Status.
    /// </summary>
    public class Stage2HiringRequestStateMachine : IHiringRequestStateMachine<Stage2HiringRequestTransitionContext>
    {
        public bool CanTransition(HiringRequestStatus from, HiringRequestStatus to) =>
            (from, to) switch
            {
                (HiringRequestStatus.Draft,         HiringRequestStatus.Submitted)     => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.Approved)      => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.Rejected)      => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.NeedsRevision) => true,
                (HiringRequestStatus.NeedsRevision, HiringRequestStatus.Submitted)     => true,
                _ => false
            };

        public TransitionResult ApplyTransition(
            HiringRequest entity, HiringRequestStatus to, Stage2HiringRequestTransitionContext ctx)
        {
            if (!CanTransition(entity.Status, to))
                return TransitionResult.InvalidState;

            if (!ctx.UserId.HasValue)
                throw new InvalidOperationException("UserId is required for all Stage 2 transitions.");

            var now = DateTime.UtcNow;

            switch (to)
            {
                case HiringRequestStatus.Submitted:
                    entity.Status = HiringRequestStatus.Submitted;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.Approved:
                    entity.Status = HiringRequestStatus.Approved;
                    entity.Stage2ReviewedByUserId = ctx.UserId.Value;
                    entity.Stage2ReviewedUtc = now;
                    entity.Stage2Feedback = ctx.Feedback;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.Rejected:
                    entity.Status = HiringRequestStatus.Rejected;
                    entity.Stage2ReviewedByUserId = ctx.UserId.Value;
                    entity.Stage2ReviewedUtc = now;
                    entity.RejectionReason = ctx.RejectionReason;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.NeedsRevision:
                    entity.Status = HiringRequestStatus.NeedsRevision;
                    entity.Stage2ReviewedByUserId = ctx.UserId.Value;
                    entity.Stage2ReviewedUtc = now;
                    entity.Stage2Feedback = ctx.Feedback;
                    entity.LastUpdatedUtc = now;
                    break;
            }

            return TransitionResult.Success;
        }
    }
}
