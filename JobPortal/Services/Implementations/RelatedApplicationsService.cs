using System.Security.Claims;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class RelatedApplicationsService : IRelatedApplicationsService
    {
        private readonly AppDbContext _context;

        public RelatedApplicationsService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<RelatedApplicationsResult> GetRelatedApplicationsAsync(
            int currentApplicationId,
            string? email,
            ClaimsPrincipal user)
        {
            var relatedAppsQuery = _context.Applications
                .Include(a => a.Job)
                .Where(a => a.Id != currentApplicationId);

            if (!string.IsNullOrWhiteSpace(email))
                relatedAppsQuery = relatedAppsQuery.Where(a => a.Email == email);
            else
                relatedAppsQuery = relatedAppsQuery.Where(a => false); // no match without email

            // Hiring Managers: restrict to their assigned jobs only
            if (user.IsInRole("HiringManager") && !user.IsInRole("Admin"))
            {
                var currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
                var assignedJobIds = await _context.JobAssignments
                    .Where(ja => ja.UserId == currentUserId)
                    .Select(ja => ja.JobId)
                    .ToListAsync();
                relatedAppsQuery = relatedAppsQuery.Where(a => assignedJobIds.Contains(a.JobId));
            }

            var relatedApps = await relatedAppsQuery.OrderByDescending(a => a.AppliedDate).ToListAsync();

            var relatedApplications = relatedApps.Select(a => new OtherApplicationDto
            {
                Id          = a.Id,
                JobTitle    = a.Job?.Title ?? "Unknown",
                Stage       = a.Stage.ToString(),
                AppliedDate = a.AppliedDate,
            }).ToList();

            var crossAppRecommendations = new List<CrossAppRecommendationDto>();
            if (relatedApps.Count > 0 && user.IsInRole("Admin"))
            {
                var relatedAppIds = relatedApps.Select(a => a.Id).ToList();
                var relatedJobMap = relatedApps.ToDictionary(a => a.Id, a => a.Job?.Title ?? "Unknown");
                var crossRecs     = await _context.CandidateRecommendations
                    .Where(r => relatedAppIds.Contains(r.ApplicationId))
                    .OrderBy(r => r.ApplicationId)
                    .ThenBy(r => r.Stage)
                    .ToListAsync();

                crossAppRecommendations = crossRecs.Select(r => new CrossAppRecommendationDto
                {
                    ApplicationId  = r.ApplicationId,
                    JobTitle       = relatedJobMap.GetValueOrDefault(r.ApplicationId, "Unknown"),
                    Stage          = r.Stage,
                    Status         = r.Status,
                    LastUpdatedUtc = r.LastUpdatedUtc,
                }).ToList();
            }

            return new RelatedApplicationsResult(relatedApplications, crossAppRecommendations);
        }
    }
}
