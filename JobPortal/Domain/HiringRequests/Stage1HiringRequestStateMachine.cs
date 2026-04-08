using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.HiringRequests
{
    /// <summary>
    /// Governs Stage 1 transitions, reviewed by the Senior Talent Lead.
    /// When Approved here, the service layer advances the entity to Stage 2.
    /// </summary>
    public class Stage1HiringRequestStateMachine : IHiringRequestStateMachine<Stage1HiringRequestTransitionContext>
    {
        public bool CanTransition(HiringRequestStatus from, HiringRequestStatus to) =>
            (from, to) switch
            {
                (HiringRequestStatus.Draft,         HiringRequestStatus.Draft)         => true,
                (HiringRequestStatus.Draft,         HiringRequestStatus.Submitted)     => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.Draft)         => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.Approved)      => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.Rejected)      => true,
                (HiringRequestStatus.Submitted,     HiringRequestStatus.NeedsRevision) => true,
                (HiringRequestStatus.NeedsRevision, HiringRequestStatus.Draft)         => true,
                (HiringRequestStatus.NeedsRevision, HiringRequestStatus.Submitted)     => true,
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
                    if (ctx.JobTitle is not null)     entity.JobTitle = ctx.JobTitle;
                    if (ctx.Department is not null)   entity.Department = ctx.Department;
                    if (ctx.Headcount.HasValue)       entity.Headcount = ctx.Headcount.Value;
                    if (ctx.Justification is not null) entity.Justification = ctx.Justification;
                    if (ctx.SalaryBand is not null)   entity.SalaryBand = ctx.SalaryBand;
                    if (ctx.TargetStartDate.HasValue) entity.TargetStartDate = ctx.TargetStartDate;
                    if (ctx.EmploymentType.HasValue)  entity.EmploymentType = ctx.EmploymentType.Value;
                    if (ctx.Priority.HasValue)        entity.Priority = ctx.Priority.Value;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.Submitted:
                    if (!ctx.UserId.HasValue)
                        throw new InvalidOperationException("UserId is required for the Submitted transition.");
                    entity.Status = HiringRequestStatus.Submitted;
                    entity.SubmittedByUserId = ctx.UserId.Value;
                    entity.SubmittedUtc = now;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.Approved:
                    if (!ctx.UserId.HasValue)
                        throw new InvalidOperationException("UserId is required for the Approved transition.");
                    entity.Status = HiringRequestStatus.Approved;
                    entity.Stage1ReviewedByUserId = ctx.UserId.Value;
                    entity.Stage1ReviewedUtc = now;
                    entity.Stage1Feedback = ctx.Feedback;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.Rejected:
                    if (!ctx.UserId.HasValue)
                        throw new InvalidOperationException("UserId is required for the Rejected transition.");
                    entity.Status = HiringRequestStatus.Rejected;
                    entity.Stage1ReviewedByUserId = ctx.UserId.Value;
                    entity.Stage1ReviewedUtc = now;
                    entity.RejectionReason = ctx.RejectionReason;
                    entity.LastUpdatedUtc = now;
                    break;

                case HiringRequestStatus.NeedsRevision:
                    if (!ctx.UserId.HasValue)
                        throw new InvalidOperationException("UserId is required for the NeedsRevision transition.");
                    entity.Status = HiringRequestStatus.NeedsRevision;
                    entity.Stage1ReviewedByUserId = ctx.UserId.Value;
                    entity.Stage1ReviewedUtc = now;
                    entity.Stage1Feedback = ctx.Feedback;
                    entity.LastUpdatedUtc = now;
                    break;
            }

            return TransitionResult.Success;
        }
    }
}
