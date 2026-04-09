using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.HiringRequests
{
    /// <summary>
    /// Governs Stage 2 transitions, reviewed by the Senior Executive.
    /// The entity enters Stage 2 after TalentLeadApproved.
    /// </summary>
    public class Stage2HiringRequestStateMachine : IHiringRequestStateMachine<Stage2HiringRequestTransitionContext>
    {
        public bool CanTransition(HiringRequestStatus from, HiringRequestStatus to) =>
            (from, to) switch
            {
                (HiringRequestStatus.TalentLeadApproved, HiringRequestStatus.ExecutiveApproved) => true,
                (HiringRequestStatus.TalentLeadApproved, HiringRequestStatus.Rejected)         => true,
                _ => false
            };

        public TransitionResult ApplyTransition(
            HiringRequest entity, HiringRequestStatus to, Stage2HiringRequestTransitionContext ctx)
        {
            if (!CanTransition(entity.Status, to))
                return TransitionResult.InvalidState;

            if (ctx.UserId is null)
                throw new InvalidOperationException("UserId is required for all Stage 2 transitions.");

            var now = DateTime.UtcNow;

            switch (to)
            {
                case HiringRequestStatus.ExecutiveApproved:
                    entity.Status = HiringRequestStatus.ExecutiveApproved;
                    entity.ExecutiveApprovedByUserId = ctx.UserId;
                    entity.ExecutiveApprovedUtc = now;
                    entity.ExecutiveNotes = ctx.Notes;
                    entity.UpdatedUtc = now;
                    break;

                case HiringRequestStatus.Rejected:
                    entity.Status = HiringRequestStatus.Rejected;
                    entity.RejectedByUserId = ctx.UserId;
                    entity.RejectedUtc = now;
                    entity.RejectionReason = ctx.RejectionReason;
                    entity.UpdatedUtc = now;
                    break;
            }

            return TransitionResult.Success;
        }
    }
}
