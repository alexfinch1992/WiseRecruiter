using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Implementation of IJobService using Entity Framework.
    /// Can later be replaced with API client implementation without changing controllers.
    /// </summary>
    public class JobService : IJobService
    {
        private readonly AppDbContext _context;

        public JobService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<List<Job>> GetAllJobsAsync(bool includeApplications = false)
        {
            var query = _context.Jobs.AsQueryable();

            if (includeApplications)
                query = query.Include(j => j.Applications);

            return await query.OrderByDescending(j => j.Id).ToListAsync();
        }

        public async Task<Job?> GetJobByIdAsync(int jobId, bool includeApplications = true, bool includeStages = true)
        {
            var query = _context.Jobs.Where(j => j.Id == jobId);

            if (includeApplications)
                query = query
                    .Include(j => j.Applications)
                    .ThenInclude(a => a.CurrentStage)
                    .Include(j => j.Applications)
                    .ThenInclude(a => a.Documents);

            if (includeStages)
                query = query.Include(j => j.Stages);

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<Job>> SearchJobsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Job>();

            var term = searchTerm.ToLower();
            return await _context.Jobs
                .Where(j => j.Title!.ToLower().Contains(term) || j.Description!.ToLower().Contains(term))
                .OrderByDescending(j => j.Id)
                .ToListAsync();
        }

        public async Task<Job> CreateJobAsync(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();
            return job;
        }

        public async Task<bool> UpdateJobAsync(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            try
            {
                _context.Jobs.Update(job);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await JobExistsAsync(job.Id))
                    return false;
                throw;
            }
        }

        public async Task<bool> DeleteJobAsync(int jobId)
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return false;

            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<JobStage>> GetJobStagesAsync(int jobId)
        {
            return await _context.JobStages
                .Where(s => s.JobId == jobId)
                .OrderBy(s => s.Order)
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> GetApplicationCountByStageAsync(int jobId)
        {
            var stages = await _context.JobStages
                .Where(s => s.JobId == jobId)
                .ToListAsync();

            var result = new Dictionary<string, int>();

            foreach (var stage in stages)
            {
                var count = await _context.Applications
                    .CountAsync(a => a.CurrentJobStageId == stage.Id);
                result[stage.Name] = count;
            }

            return result;
        }

        private async Task<bool> JobExistsAsync(int jobId)
        {
            return await _context.Jobs.AnyAsync(j => j.Id == jobId);
        }
    }
}
