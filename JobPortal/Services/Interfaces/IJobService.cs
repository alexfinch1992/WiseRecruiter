using JobPortal.Models;

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
    }
}
