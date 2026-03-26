using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Abstraction for job management operations.
    /// Enables future API integration without controller changes.
    /// </summary>
    public interface IJobService
    {
        /// <summary>
        /// Retrieves all jobs with optional filtering and sorting.
        /// </summary>
        Task<List<Job>> GetAllJobsAsync(bool includeApplications = false);

        /// <summary>
        /// Retrieves a single job by ID with related entities.
        /// </summary>
        Task<Job?> GetJobByIdAsync(int jobId, bool includeApplications = true, bool includeStages = true);

        /// <summary>
        /// Searches jobs by title or description.
        /// </summary>
        Task<List<Job>> SearchJobsAsync(string searchTerm);

        /// <summary>
        /// Creates a new job posting.
        /// </summary>
        Task<Job> CreateJobAsync(Job job);

        /// <summary>
        /// Updates an existing job.
        /// </summary>
        Task<bool> UpdateJobAsync(Job job);

        /// <summary>
        /// Deletes a job posting.
        /// </summary>
        Task<bool> DeleteJobAsync(int jobId);

        /// <summary>
        /// Gets all interview stages for a job.
        /// </summary>
        Task<List<JobStage>> GetJobStagesAsync(int jobId);

        /// <summary>
        /// Gets count of applications at each stage for a job.
        /// Used for analytics and reporting.
        /// </summary>
        Task<Dictionary<string, int>> GetApplicationCountByStageAsync(int jobId);
    }
}
