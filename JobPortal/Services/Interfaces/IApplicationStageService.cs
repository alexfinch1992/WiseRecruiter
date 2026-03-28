using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IApplicationStageService
    {
        Task<StageUpdateResult> UpdateStageAsync(
            int applicationId,
            ApplicationStage newStage,
            bool proceedWithoutApproval,
            string userId);
    }
}
