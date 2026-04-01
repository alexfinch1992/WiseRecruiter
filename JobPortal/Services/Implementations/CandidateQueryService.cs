using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    internal enum CandidateSearchMode
    {
        None,
        NameOnly,
        NameOrEmail
    }

    public class CandidateQueryService : ICandidateQueryService
    {
        private readonly AppDbContext _context;

        public CandidateQueryService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Job>> GetApplicationsForJobsAsync()
        {
            return await _context.Jobs
                .Include(j => j.Applications)
                .OrderByDescending(j => j.Id)
                .ToListAsync();
        }

        public async Task<List<Application>> GetCandidatesAsync(string? search)
        {
            var query = BuildCandidateQuery(
                includeCandidate: true,
                includeJob: true,
                excludeArchivedCandidates: true,
                search: search,
                searchMode: CandidateSearchMode.NameOnly);

            return await query
                .OrderBy(a => a.Name)
                .Take(100)
                .ToListAsync();
        }

        public async Task<List<Application>> SearchCandidatesAsync(string? searchQuery)
        {
            var candidates = await BuildCandidateQuery(
                    includeJob: true,
                    includeCurrentStage: true)
                .ToListAsync();

            return ApplyNameOrEmailSearch(candidates, searchQuery);
        }

        public async Task<Job?> GetJobDetailSearchAsync(int id, string? searchQuery, string? sort)
        {
            var job = await _context.Jobs
                .Include(j => j.Applications!)
                .ThenInclude(a => a.CurrentStage)
                .Include(j => j.Stages)
                .Include(j => j.Applications!).ThenInclude(a => a.Candidate)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return null;

            // Filter applications by search query
            if (job.Applications != null && !string.IsNullOrWhiteSpace(searchQuery))
                job.Applications = ApplyNameOrEmailSearch(job.Applications, searchQuery);

            // Sort applications
            if (job.Applications != null)
            {
                job.Applications = sort switch
                {
                    "name" => job.Applications.OrderBy(a => a.Name).ToList(),
                    "date" => job.Applications.OrderByDescending(a => a.AppliedDate).ToList(),
                    _      => job.Applications.OrderBy(a => a.CurrentStage?.Order ?? 0).ThenBy(a => a.Name).ToList()
                };
            }

            return job;
        }

        public async Task<(bool JobFound, List<JobSearchApiItem> Items)> GetJobDetailSearchApiAsync(int id, string? searchQuery)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job == null)
                return (false, new List<JobSearchApiItem>());

            var candidates = await _context.Applications
                .Where(a => a.JobId == id)
                .Include(a => a.CurrentStage)
                .ToListAsync();

            candidates = ApplyNameOrEmailSearch(candidates, searchQuery);

            // Return limited results (top 10)
            var results = candidates
                .Take(10)
                .Select(a => new JobSearchApiItem(a.Id, a.Name, a.Email, a.City, a.CurrentStage?.Name ?? "Unassigned"))
                .ToList();

            return (true, results);
        }

        public async Task<List<CandidateSearchApiItem>> GetSearchCandidatesApiAsync(string? searchQuery)
        {
            var candidates = await BuildCandidateQuery(
                    includeJob: true,
                    includeCurrentStage: true)
                .ToListAsync();

            candidates = ApplyNameOrEmailSearch(candidates, searchQuery);

            // Return limited results (top 15)
            return candidates
                .Take(15)
                .Select(a => new CandidateSearchApiItem(a.Id, a.Name, a.Email, a.Job?.Title ?? "Unknown", a.CurrentStage?.Name ?? "Unassigned"))
                .ToList();
        }

        public async Task<IEnumerable<UnifiedCandidateDto>> GetCandidatesJsonAsync(string? search)
        {
            var query = BuildCandidateQuery(
                includeJob: true,
                excludeArchivedCandidates: true,
                search: search,
                searchMode: CandidateSearchMode.NameOrEmail);

            var raw = await query
                .OrderBy(a => a.Name)
                .ToListAsync();

            return BuildUnifiedCandidates(raw);
        }

        private IQueryable<Application> BuildCandidateQuery(
            bool includeCandidate = false,
            bool includeJob = false,
            bool includeCurrentStage = false,
            bool excludeArchivedCandidates = false,
            string? search = null,
            CandidateSearchMode searchMode = CandidateSearchMode.None)
        {
            IQueryable<Application> query = _context.Applications;

            if (includeCandidate)
                query = query.Include(a => a.Candidate);

            if (includeJob)
                query = query.Include(a => a.Job);

            if (includeCurrentStage)
                query = query.Include(a => a.CurrentStage);

            if (excludeArchivedCandidates)
                query = query.Where(a => a.Candidate == null || !a.Candidate.IsArchived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = searchMode switch
                {
                    CandidateSearchMode.NameOnly => query.Where(a => a.Name != null && a.Name.Contains(search)),
                    CandidateSearchMode.NameOrEmail => query.Where(a =>
                        (a.Name != null && a.Name.Contains(search)) ||
                        (a.Email != null && a.Email.Contains(search))),
                    _ => query
                };
            }

            return query;
        }

        private List<Application> ApplyNameOrEmailSearch(
            IEnumerable<Application> applications,
            string? searchQuery)
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
                return applications.ToList();

            var query = searchQuery.ToLowerInvariant();

            return applications
                .Where(a => (a.Name?.ToLowerInvariant().Contains(query) ?? false) ||
                           (a.Email?.ToLowerInvariant().Contains(query) ?? false))
                .ToList();
        }

        private IEnumerable<UnifiedCandidateDto> BuildUnifiedCandidates(
            IEnumerable<Application> applications)
        {
            var terminalStages = new[] { ApplicationStage.Rejected, ApplicationStage.Hired };

            return applications
                .GroupBy(a => a.Email ?? $"__unknown_{a.Id}__",
                         StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var ordered = g.OrderByDescending(a => a.AppliedDate).ToList();
                    var newest  = ordered.First();
                    var active  = ordered.Where(a => !terminalStages.Contains(a.Stage)).ToList();

                    return new UnifiedCandidateDto
                    {
                        Email                  = g.Key,
                        Name                   = newest.Name ?? string.Empty,
                        ApplicationIds         = ordered.Select(a => a.Id).ToList(),
                        ActiveApplicationCount = active.Count,
                        CurrentStage           = (active.FirstOrDefault() ?? newest).Stage.ToString(),
                        LatestAppliedDate      = newest.AppliedDate,
                    };
                })
                .OrderBy(u => u.Name)
                .ToList();
        }
    }
}
