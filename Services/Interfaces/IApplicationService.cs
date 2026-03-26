using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Abstraction for application/candidate management operations.
    /// Enables future API integration without controller changes.
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// Retrieves all applications, optionally filtered by job.
        /// </summary>
        Task<List<Application>> GetAllApplicationsAsync(int? jobId = null);

        /// <summary>
        /// Retrieves a single application with all related data.
        /// </summary>
        Task<Application?> GetApplicationByIdAsync(int applicationId);

        /// <summary>
        /// Searches applications by candidate name or email.
        /// </summary>
        Task<List<Application>> SearchApplicationsAsync(string searchTerm);

        /// <summary>
        /// Creates a new application/candidate record.
        /// </summary>
        Task<Application> CreateApplicationAsync(Application application);

        /// <summary>
        /// Updates an existing application.
        /// </summary>
        Task<bool> UpdateApplicationAsync(Application application);

        /// <summary>
        /// Deletes an application.
        /// </summary>
        Task<bool> DeleteApplicationAsync(int applicationId);

        /// <summary>
        /// Transitions an application to a new stage.
        /// Enforces business rules (e.g., valid stage for job).
        /// </summary>
        Task<bool> TransitionToStageAsync(int applicationId, int newStageId);

        /// <summary>
        /// Gets applications for a specific job stage.
        /// </summary>
        Task<List<Application>> GetApplicationsByStageAsync(int stageId);

        /// <summary>
        /// Gets applications sorted by specified criteria.
        /// </summary>
        Task<List<Application>> GetApplicationsSortedAsync(int jobId, ApplicationSortBy sortBy);
    }

    public enum ApplicationSortBy
    {
        Stage = 0,
        Name = 1,
        AppliedDate = 2
    }
}
