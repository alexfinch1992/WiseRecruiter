using System.Security.Claims;
using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface ICandidateCoreService
    {
        /// <summary>
        /// Loads the <see cref="Application"/> with Job (and Stages), CurrentStage, Documents,
        /// and Candidate eagerly included, then enforces HiringManager access rules.
        /// </summary>
        /// <returns>
        /// The loaded application, or <c>null</c> if no application with
        /// <paramref name="applicationId"/> exists.
        /// Throws <see cref="CandidateAccessForbiddenException"/> when the current user is a
        /// HiringManager without access to the job.
        /// </returns>
        Task<Application?> LoadApplicationAsync(int applicationId, ClaimsPrincipal user);
    }
}
