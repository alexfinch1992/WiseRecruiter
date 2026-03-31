using JobPortal.Models;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Service layer for job-specific business logic.
    /// </summary>
    public interface IJobService
    {
        /// <summary>
        /// Creates a new job with auto-generated default stages.
        /// Business logic: Automatically creates "Applied", "Interview", "Offer" stages.
        /// </summary>
        Task<Job> CreateJobAsync(Job job);

        /// <summary>
        /// Returns a per-stage candidate count summary for the JobDetail page.
        /// Groups by the candidate's effective stage: custom stage name when
        /// CurrentJobStageId is set, otherwise the ApplicationStage enum label.
        /// No candidate should ever appear as "Unassigned".
        /// </summary>
        IReadOnlyList<CandidateStageSummaryItem> GetStageSummary(Job job);
    }
}
