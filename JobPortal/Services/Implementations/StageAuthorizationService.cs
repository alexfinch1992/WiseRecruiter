using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    // Temporary open implementation — always grants approval.
    // Replace with real permission checks when roles are defined.
    public class StageAuthorizationService : IStageAuthorizationService
    {
        public bool CanApproveStage1(int userId) => true;
        public bool CanApproveStage2(int userId) => true;
    }
}
