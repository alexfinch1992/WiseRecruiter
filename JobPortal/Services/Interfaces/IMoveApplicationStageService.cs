using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IMoveApplicationStageService
    {
        /// <summary>
        /// Parses <paramref name="selectedStage"/>, moves the application to the new stage,
        /// saves changes, and logs the audit entry when no approval warning is raised.
        /// </summary>
        Task<MoveStageResult> MoveAsync(
            int    applicationId,
            string selectedStage,
            bool   proceedWithoutApproval,
            string userId);

        /// <summary>
        /// Form-POST variant. Returns <c>null</c> when the application is not found,
        /// <c>Success = false</c> when the stage string is invalid,
        /// or a populated result for normal flow (approval warning or success).
        /// Does not write audit log entries.
        /// </summary>
        Task<MoveStageResult?> MoveForFormAsync(
            int    applicationId,
            string selectedStage,
            bool   proceedWithoutApproval,
            string userId);
    }
}
