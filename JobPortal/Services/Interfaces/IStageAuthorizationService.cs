namespace JobPortal.Services.Interfaces
{
    public interface IStageAuthorizationService
    {
        bool CanApproveStage1(int userId);
        bool CanApproveStage2(int userId);
    }
}
