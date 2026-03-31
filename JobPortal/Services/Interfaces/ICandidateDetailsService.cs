using System.Security.Claims;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Thrown by <see cref="ICandidateDetailsService.GetCandidateDetailsAsync"/> when the
    /// current user does not have access to the requested application.
    /// </summary>
    public sealed class CandidateAccessForbiddenException : Exception
    {
        public CandidateAccessForbiddenException() : base("Access to this candidate is forbidden.") { }
    }

    public interface ICandidateDetailsService
    {
        /// <summary>
        /// Loads the full CandidateAdminViewModel for the given application.
        /// Returns <c>null</c> when the application does not exist.
        /// Throws <see cref="CandidateAccessForbiddenException"/> when the current user is not
        /// allowed to access the application.
        /// </summary>
        Task<CandidateAdminViewModel?> GetCandidateDetailsAsync(
            int applicationId,
            ClaimsPrincipal user,
            int? stageApprovalWarnApplicationId = null);
    }
}
