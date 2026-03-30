using System.Collections.Generic;
using System.Threading.Tasks;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Encapsulates the query for a user's allowed job IDs, making the
    /// filtering logic injectable and independently testable.
    /// </summary>
    public interface IJobAccessService
    {
        /// <summary>Returns the list of Job IDs assigned to <paramref name="userId"/>.</summary>
        Task<List<int>> GetAssignedJobIdsAsync(string userId);

        /// <summary>Returns true when <paramref name="userId"/> is assigned to <paramref name="jobId"/>.</summary>
        Task<bool> CanAccessJobAsync(string userId, int jobId);
    }
}
