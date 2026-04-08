using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.HiringRequests
{
    /// <summary>
    /// Governs Stage 1 transitions, reviewed by the Senior Talent Lead.
    /// When TalentLeadApproved, the service layer advances the entity to Stage 2.
    /// </summary>
    public class Stage1HiringRequestStateMachine : IHiringRequestStateMachine<Stage1HiringRequestTransitionContext>
    {
        public bool CanTransition(HiringRequestStatus from, HiringRequestStatus to) =>
            (from, to) switch
            {
                (HiringRequestStatus.Draft,     HiringRequestStatus.Draft)              => true,
                (HiringRequestStatus.Draft,     HiringRequestStatus.Submitted)          => true,
                (HiringRequestStatus.Submitted, HiringRequestStatus.TalentLeadApproved) => true,
                (HiringRequestStatus.Submitted, HiringRequestStatus.Rejected)           => true,
                _ => false
            };

        public TransitionResult ApplyTransition(
            HiringRequest entity, HiringRequestStatus to, Stage1HiringRequestTransitionContext ctx)
        {
            if (!CanTransition(entity.Status, to))
                return TransitionResult.InvalidState;

            var now = DateTime.UtcNow;

            switch (to)
            {
                case HiringRequestStatus.Draft:
                    entity.Status = HiringRequestStatus.Draft;
                    if (ctx.RoleTitle is not null)         entity.RoleTitle = ctx.RoleTitle;
                    if (ctx.Department is not null)        entity.Department = ctx.Department;
                    if (ctx.LevelBand is not null)         entity.LevelBand = ctx.LevelBand;
                    if (ctx.Location is not null)          entity.Location = ctx.Location;
                    if (ctx.IsReplacement.HasValue)        entity.IsReplacement = ctx.IsReplacement.Value;
                    if (ctx.ReplacementReason is not null) entity.ReplacementReason = ctx.ReplacementReason;
                    if (ctx.Headcount.HasValue)            entity.Headcount = ctx.Headcount.Value;
                    if (ctx.Justification is not null)     entity.Justification = ctx.Justification;
                    entity.UpdatedUtc = now;
                    break;

                case HiringRequestStatus.Submitted:
                    if (ctx.UserId is null)
                        throw new InvalidOperationException("UserId is required for the Submitted transition.");
                    entity.Status = HiringRequestStatus.Submitted;
                    entity.UpdatedUtc = now;
                    break;

                case HiringRequestStatus.TalentLeadApproved:
                    if (ctx.UserId is null)
                        throw new InvalidOperationException("UserId is required for the TalentLeadApproved transition.");
                    entity.Status = HiringRequestStatus.TalentLeadApproved;
                    entity.TalentLeadReviewedByUserId = ctx.UserId;
                    entity.TalentLeadReviewedUtc = now;
                    entity.TalentLeadNotes = ctx.Notes;
                    entity.UpdatedUtc = now;
                    break;

                case HiringRequestStatus.Rejected:
                    if (ctx.UserId is null)
                        throw new InvalidOperationException("UserId is required for the Rejected transition.");
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
