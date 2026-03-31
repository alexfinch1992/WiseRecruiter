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

            // Application-based results: one entry per application so each candidate+job
            // combination is visible. Results are ordered most-recent-first.
            var candidates = await _context.Applications
                .Where(a => a.Candidate != null &&
                            (a.Candidate.FirstName.ToLower().Contains(lowerQuery) ||
                             a.Candidate.LastName.ToLower().Contains(lowerQuery)))
                .OrderByDescending(a => a.Id)
                .Take(5)
                .Select(a => new GlobalSearchResult
                {
                    Type        = "Candidate",
                    Id          = a.Id,
                    DisplayText = a.Candidate!.FirstName + " " + a.Candidate.LastName,
                    SubText     = a.Job != null ? a.Job.Title ?? string.Empty : string.Empty,
                })
                .ToListAsync();

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
