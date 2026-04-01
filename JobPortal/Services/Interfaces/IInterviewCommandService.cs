using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IInterviewCommandService
    {
        /// <summary>
        /// Creates an interview for a candidate, assigns interviewers, and prepares the Stage 1
        /// recommendation.  Encapsulates all business logic from the CreateInterview controller action.
        /// </summary>
        Task<InterviewCreateResult> CreateAsync(
            int        candidateId,
            int        applicationId,
            string     selectedStage,
            DateTime   scheduledAt,
            List<int>? selectedInterviewerIds,
            bool       proceedWithoutApproval,
            string?    bypassReason,
            string     userId);

        /// <summary>
        /// Cancels an interview. Returns (false, false) when not found, (false, true) when candidateId
        /// does not match the interview owner, and (true, false) on success.
        /// </summary>
        Task<(bool Success, bool InvalidOwnership)> CancelInterviewAsync(int interviewId, int candidateId);
    }
}
