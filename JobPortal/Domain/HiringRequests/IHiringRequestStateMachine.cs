using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Domain.HiringRequests
{
    public interface IHiringRequestStateMachine<TContext>
    {
        bool CanTransition(HiringRequestStatus from, HiringRequestStatus to);
        TransitionResult ApplyTransition(HiringRequest entity, HiringRequestStatus to, TContext context);
    }
}
