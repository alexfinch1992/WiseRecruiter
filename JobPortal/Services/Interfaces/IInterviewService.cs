using JobPortal.Models;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    /// <summary>Data needed to render the interview-scheduling form on the candidate details page.</summary>
    public record InterviewSchedulingData(
        List<Application> CandidateApplications,
        List<AdminUser> AdminUsers);

    public interface IInterviewService
    {
        Task<Interview> CreateInterviewAsync(int candidateId, int applicationId, int jobStageId, DateTime scheduledAt);

        /// <summary>
        /// Loads all interviews for the given candidate with JobStage and Interviewer data,
        /// ordered for display (active first, past second, cancelled last).
        /// </summary>
        Task<List<InterviewSummaryDto>> GetInterviewSummariesAsync(int candidateId, string fallbackStageName);

        /// <summary>
        /// Loads all applications for the candidate (with Job) and all admin users needed to
        /// render the interview-scheduling form on the candidate details page.
        /// </summary>
        Task<InterviewSchedulingData> GetInterviewSchedulingDataAsync(int candidateId);

        Task<List<UpcomingInterviewDto>> GetUpcomingInterviewsAsync();
    }
}
