using System.Security.Claims;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    public record RelatedApplicationsResult(
        List<OtherApplicationDto> RelatedApplications,
        List<CrossAppRecommendationDto> CrossApplicationRecommendations);

    public interface IRelatedApplicationsService
    {
        /// <summary>
        /// Finds all other applications from the same email address, filtered to the caller's
        /// assigned jobs when the caller is a HiringManager, and loads cross-application
        /// recommendations for Admin-role callers.
        /// </summary>
        Task<RelatedApplicationsResult> GetRelatedApplicationsAsync(
            int currentApplicationId,
            string? email,
            ClaimsPrincipal user);
    }
}
