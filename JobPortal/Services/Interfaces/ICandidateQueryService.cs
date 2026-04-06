using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface ICandidateQueryService
    {
        /// <summary>
        /// Server-side faceted search with filtering, sorting, and pagination.
        /// </summary>
        Task<CandidateSearchResultViewModel> SearchAsync(CandidateSearchParams p);

        /// <summary>
        /// Returns applications (non-archived candidates) filtered by name and limited to 100 rows.
        /// </summary>
        Task<List<Application>> GetCandidatesAsync(string? search);

        /// <summary>Returns all applications filtered by name or email.</summary>
        Task<List<Application>> SearchCandidatesAsync(string? searchQuery);

        /// <summary>
        /// Returns the job with its applications filtered by searchQuery and sorted by <paramref name="sort"/>.
        /// Returns null when the job is not found.
        /// </summary>
        Task<Job?> GetJobDetailSearchAsync(int id, string? searchQuery, string? sort, string? dir = "asc");

        /// <summary>
        /// Returns top-10 application items for a job, filtered by searchQuery.
        /// Returns (false, empty) when the job is not found.
        /// </summary>
        Task<(bool JobFound, List<JobSearchApiItem> Items)> GetJobDetailSearchApiAsync(int id, string? searchQuery);

        /// <summary>Returns top-15 candidate items across all jobs, filtered by searchQuery.</summary>
        Task<List<CandidateSearchApiItem>> GetSearchCandidatesApiAsync(string? searchQuery);

        /// <summary>
        /// Returns unified candidates (one row per email) from all non-archived applications,
        /// filtered by search.
        /// </summary>
        Task<IEnumerable<UnifiedCandidateDto>> GetCandidatesJsonAsync(string? search);

        Task<Application?> GetApplicationByIdAsync(int id);
        Task<Application?> GetApplicationWithCandidateAsync(int id);
        Task RejectApplicationAsync(int applicationId, string reason, string notes, bool globalArchive, string userId);
    }
}
