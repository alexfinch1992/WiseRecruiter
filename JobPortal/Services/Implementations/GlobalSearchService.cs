using JobPortal.Data;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class GlobalSearchService : IGlobalSearchService
    {
        private readonly AppDbContext _context;

        public GlobalSearchService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<GlobalSearchResult>> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<GlobalSearchResult>();

            var lowerQuery = query.ToLowerInvariant();

            // Fetch candidate applications matching the query, ordered most-recent-first,
            // then deduplicate in-memory so each candidate appears only once.
            var matchingApps = await _context.Applications
                .Where(a => a.Candidate != null &&
                            (a.Candidate.FirstName.ToLower().Contains(lowerQuery) ||
                             a.Candidate.LastName.ToLower().Contains(lowerQuery)))
                .OrderByDescending(a => a.Id)
                .Take(50)
                .Select(a => new
                {
                    ApplicationId = a.Id,
                    CandidateId   = a.CandidateId,
                    DisplayText   = a.Candidate!.FirstName + " " + a.Candidate.LastName,
                    SubText       = a.Job != null ? a.Job.Title ?? string.Empty : string.Empty,
                })
                .ToListAsync();

            var candidates = matchingApps
                .GroupBy(a => a.CandidateId)
                .Select(g => g.First())
                .Take(5)
                .Select(a => new GlobalSearchResult
                {
                    Type        = "Candidate",
                    Id          = a.ApplicationId,
                    DisplayText = a.DisplayText,
                    SubText     = a.SubText,
                })
                .ToList();

            // Search Job table by title
            var jobs = await _context.Jobs
                .Where(j => j.Title != null && j.Title.ToLower().Contains(lowerQuery))
                .OrderBy(j => j.Title)
                .Take(5)
                .Select(j => new GlobalSearchResult
                {
                    Type        = "Job",
                    Id          = j.Id,
                    DisplayText = j.Title!,
                    SubText     = string.Empty,
                })
                .ToListAsync();

            return candidates.Concat(jobs).ToList();
        }
    }
}
