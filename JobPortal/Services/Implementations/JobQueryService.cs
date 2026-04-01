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

        public async Task<Job?> GetJobDetailAsync(int id, string? sort)
        {
            var job = await _context.Jobs
                .Include(j => j.Applications!)
                .ThenInclude(a => a.CurrentStage)
                .Include(j => j.Applications!)
                .ThenInclude(a => a.Documents)
                .Include(j => j.Stages)
                .Include(j => j.Applications!).ThenInclude(a => a.Candidate)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return null;

            if (job.Applications != null)
                job.Applications = job.Applications.Where(a => !(a.Candidate?.IsArchived ?? false)).ToList();

            if (job.Applications != null)
            {
                job.Applications = sort switch
                {
                    "name" => job.Applications.OrderBy(a => a.Name).ToList(),
                    "date" => job.Applications.OrderByDescending(a => a.AppliedDate).ToList(),
                    _ => job.Applications.OrderBy(a => a.CurrentStage?.Order ?? 0).ThenBy(a => a.Name).ToList()
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

        public async Task<bool> JobExistsAsync(int id)
        {
            return await _context.Jobs.AnyAsync(e => e.Id == id);
        }
    }
}
