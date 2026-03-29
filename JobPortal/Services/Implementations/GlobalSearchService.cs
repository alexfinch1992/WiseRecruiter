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

            // Search Candidate table — one result per candidate, no Application duplicates
            var candidateEntities = await _context.Candidates
                .Where(c => c.FirstName.ToLower().Contains(lowerQuery)
                         || c.LastName.ToLower().Contains(lowerQuery))
                .OrderBy(c => c.FirstName)
                .Take(5)
                .ToListAsync();

            var candidates = new List<GlobalSearchResult>();
            foreach (var c in candidateEntities)
            {
                // Get most recent Application.Id for navigation to Admin/CandidateDetails
                var latestAppId = await _context.Applications
                    .Where(a => a.CandidateId == c.Id)
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.Id)
                    .FirstOrDefaultAsync();

                if (latestAppId != 0)
                {
                    candidates.Add(new GlobalSearchResult
                    {
                        Type = "Candidate",
                        Id = latestAppId,
                        DisplayText = $"{c.FirstName} {c.LastName}"
                    });
                }
            }

            // Search Job table by title
            var jobs = await _context.Jobs
                .Where(j => j.Title != null && j.Title.ToLower().Contains(lowerQuery))
                .OrderBy(j => j.Title)
                .Take(5)
                .Select(j => new GlobalSearchResult
                {
                    Type = "Job",
                    Id = j.Id,
                    DisplayText = j.Title!
                })
                .ToListAsync();

            return candidates.Concat(jobs).ToList();
        }
    }
}
