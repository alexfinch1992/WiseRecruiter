using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Implementation of IApplicationService using Entity Framework.
    /// Centralizes all candidate/application business logic.
    /// </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly AppDbContext _context;
        private readonly IJobService _jobService;

        public ApplicationService(AppDbContext context, IJobService jobService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        }

        public async Task<List<Application>> GetAllApplicationsAsync(int? jobId = null)
        {
            var query = _context.Applications
                .Include(a => a.Job)
                .Include(a => a.CurrentStage)
                .Include(a => a.Documents)
                .AsQueryable();

            if (jobId.HasValue)
                query = query.Where(a => a.JobId == jobId.Value);

            return await query.OrderByDescending(a => a.AppliedDate).ToListAsync();
        }

        public async Task<Application?> GetApplicationByIdAsync(int applicationId)
        {
            return await _context.Applications
                .Include(a => a.Job)
                .ThenInclude(j => j!.Stages)
                .Include(a => a.CurrentStage)
                .Include(a => a.Documents)
                .FirstOrDefaultAsync(a => a.Id == applicationId);
        }

        public async Task<List<Application>> SearchApplicationsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Application>();

            var term = searchTerm.ToLower();
            return await _context.Applications
                .Where(a => a.Name!.ToLower().Contains(term) || a.Email!.ToLower().Contains(term))
                .Include(a => a.Job)
                .Include(a => a.CurrentStage)
                .OrderByDescending(a => a.AppliedDate)
                .ToListAsync();
        }

        public async Task<Application> CreateApplicationAsync(Application application)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            // Auto-assign to first stage if not specified
            if (application.CurrentJobStageId == null)
            {
                var firstStage = await _context.JobStages
                    .Where(s => s.JobId == application.JobId)
                    .OrderBy(s => s.Order)
                    .FirstOrDefaultAsync();

                if (firstStage != null)
                    application.CurrentJobStageId = firstStage.Id;
            }

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();
            return application;
        }

        public async Task<bool> UpdateApplicationAsync(Application application)
        {
            if (application == null)
                throw new ArgumentNullException(nameof(application));

            try
            {
                _context.Applications.Update(application);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ApplicationExistsAsync(application.Id))
                    return false;
                throw;
            }
        }

        public async Task<bool> DeleteApplicationAsync(int applicationId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                return false;

            _context.Applications.Remove(application);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> TransitionToStageAsync(int applicationId, int newStageId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return false;

            // Validate the stage belongs to the application's job
            var stage = await _context.JobStages
                .FirstOrDefaultAsync(s => s.Id == newStageId && s.JobId == application.JobId);

            if (stage == null)
                return false;

            application.CurrentJobStageId = newStageId;
            _context.Applications.Update(application);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Application>> GetApplicationsByStageAsync(int stageId)
        {
            return await _context.Applications
                .Where(a => a.CurrentJobStageId == stageId)
                .Include(a => a.Job)
                .Include(a => a.CurrentStage)
                .OrderBy(a => a.Name)
                .ToListAsync();
        }

        public async Task<List<Application>> GetApplicationsSortedAsync(int jobId, ApplicationSortBy sortBy)
        {
            var query = _context.Applications
                .Where(a => a.JobId == jobId)
                .Include(a => a.CurrentStage)
                .Include(a => a.Documents)
                .Include(a => a.Job)
                .AsQueryable();

            return await (sortBy switch
            {
                ApplicationSortBy.Name => query.OrderBy(a => a.Name),
                ApplicationSortBy.AppliedDate => query.OrderByDescending(a => a.AppliedDate),
                ApplicationSortBy.Stage => query.OrderBy(a => a.CurrentStage!.Order).ThenBy(a => a.Name),
                _ => query.OrderBy(a => a.CurrentStage!.Order).ThenBy(a => a.Name)
            }).ToListAsync();
        }

        private async Task<bool> ApplicationExistsAsync(int applicationId)
        {
            return await _context.Applications.AnyAsync(a => a.Id == applicationId);
        }
    }
}
