using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Minimal service layer for application-specific business logic.
    /// CRUD operations are handled directly by controllers using DbContext for simplicity.
    /// Only encapsulates logic that contains real business rules.
    /// </summary>
    public interface IApplicationService
    {
        /// <summary>
        /// Creates a new application with auto-stage assignment.
        /// Business logic: Auto-assigns to first stage if not specified.
        /// </summary>
        Task<Application> CreateApplicationAsync(Application application);

        /// <summary>
        /// Transitions an application to a new stage.
        /// Business logic: Validates stage belongs to the application's job.
        /// </summary>
        Task<bool> TransitionToStageAsync(int applicationId, int newStageId);

        /// <summary>
        /// Gets applications sorted by specified strategy.
        /// Business logic: Handles complex sorting rules (by stage order, name, date).
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
