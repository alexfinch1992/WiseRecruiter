using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class JobQueryService : IJobQueryService
    {
        private readonly AppDbContext _context;

        public JobQueryService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Job>> GetJobsAsync(IEnumerable<int>? assignedIds = null)
        {
            if (assignedIds != null)
                return await _context.Jobs.Where(j => assignedIds.Contains(j.Id)).ToListAsync();

            return await _context.Jobs.ToListAsync();
        }

        public async Task<Job?> GetJobDetailAsync(int id, string? sort, string? dir = "asc", string? searchQuery = null)
        {
            var job = await _context.Jobs
                .Include(j => j.Applications!)
                .ThenInclude(a => a.CurrentStage)
                .Include(j => j.Applications!)
                .ThenInclude(a => a.Documents)
                .Include(j => j.Stages)
                .Include(j => j.Applications!).ThenInclude(a => a.Candidate)
                .Include(j => j.OwnerUser)
                .Include(j => j.JobUsers!)
                    .ThenInclude(ju => ju.User)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return null;

            if (job.Applications != null)
                job.Applications = job.Applications.Where(a => !(a.Candidate?.IsArchived ?? false)).ToList();

            if (job.Applications != null && !string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.ToLowerInvariant();
                job.Applications = job.Applications
                    .Where(a => (a.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                               (a.Email?.ToLowerInvariant().Contains(q) ?? false))
                    .ToList();
            }

            if (job.Applications != null)
            {
                var ascending = dir == "asc";
                job.Applications = sort switch
                {
                    "name" => ascending
                        ? job.Applications.OrderBy(a => a.Candidate?.LastName).ThenBy(a => a.Candidate?.FirstName).ToList()
                        : job.Applications.OrderByDescending(a => a.Candidate?.LastName).ThenByDescending(a => a.Candidate?.FirstName).ToList(),
                    "date" => ascending
                        ? job.Applications.OrderBy(a => a.AppliedDate).ToList()
                        : job.Applications.OrderByDescending(a => a.AppliedDate).ToList(),
                    _ => ascending
                        ? job.Applications.OrderBy(a => a.Stage).ThenBy(a => a.CurrentStage?.Order ?? int.MaxValue).ThenBy(a => a.Candidate?.LastName).ThenBy(a => a.Candidate?.FirstName).ToList()
                        : job.Applications.OrderByDescending(a => a.Stage).ThenByDescending(a => a.CurrentStage?.Order ?? int.MinValue).ThenByDescending(a => a.Candidate?.LastName).ThenByDescending(a => a.Candidate?.FirstName).ToList()
                };
            }

            return job;
        }

        public async Task<Job?> GetJobForEditAsync(int id)
        {
            return await _context.Jobs.FindAsync(id);
        }

        public async Task<Job?> GetJobForDeleteAsync(int id)
        {
            return await _context.Jobs.FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Job?> GetJobWithTemplateAsync(int id)
        {
            return await _context.Jobs
                .Include(j => j.ScorecardTemplate)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<bool> JobExistsAsync(int id)
        {
            return await _context.Jobs.AnyAsync(e => e.Id == id);
        }

        public async Task<Job?> GetJobByIdAsync(int id)
        {
            return await _context.Jobs.FindAsync(id);
        }

        public async Task<List<JobStage>> GetStagesForJobAsync(int jobId)
        {
            return await _context.JobStages
                .Where(s => s.JobId == jobId)
                .OrderBy(s => s.Order)
                .ToListAsync();
        }
    }
}
