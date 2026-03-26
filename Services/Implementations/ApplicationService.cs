using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Simplified ApplicationService with focused business logic only.
    /// CRUD operations should be handled by controllers using DbContext directly.
    /// This service only contains methods with actual business rules.
    /// </summary>
    public class ApplicationService : IApplicationService
    {
        private readonly AppDbContext _context;

        public ApplicationService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// Creates application with auto-stage assignment business logic.
        /// Assigns to the first stage of the job if not explicitly specified.
        /// </summary>
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

        /// <summary>
        /// Transitions an application to a new stage with validation.
        /// Ensures the stage exists and belongs to the application's job.
        /// </summary>
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

        /// <summary>
        /// Gets applications for a job sorted by specified strategy.
        /// Handles multiple sort strategies: Name, AppliedDate, Stage order.
        /// </summary>
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
    }
}
