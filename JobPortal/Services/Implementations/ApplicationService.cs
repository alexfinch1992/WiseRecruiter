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

            if (application.JobId <= 0)
                throw new ArgumentException("A valid JobId is required.", nameof(application.JobId));

            var jobExists = await _context.Jobs.AnyAsync(j => j.Id == application.JobId);
            if (!jobExists)
                throw new InvalidOperationException($"Job with ID {application.JobId} does not exist.");

            if (application.CandidateId <= 0)
            {
                var existingCandidate = await _context.Candidates
                    .FirstOrDefaultAsync(c => c.Email == (application.Email ?? string.Empty));

                if (existingCandidate != null)
                {
                    application.CandidateId = existingCandidate.Id;
                }
                else
                {
                    var (firstName, lastName) = SplitName(application.Name);
                    var candidate = new Candidate
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Email = application.Email ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Candidates.Add(candidate);
                    await _context.SaveChangesAsync();
                    application.CandidateId = candidate.Id;
                }
            }
            else
            {
                var candidateExists = await _context.Candidates.AnyAsync(c => c.Id == application.CandidateId);
                if (!candidateExists)
                    throw new InvalidOperationException("Application references an invalid candidate.");
            }

            // New applications start with no custom stage; the display layer
            // falls back to Application.Stage (Applied) when this is null.
            application.CurrentJobStageId = null;

            _context.Applications.Add(application);
            await _context.SaveChangesAsync();
            return application;
        }

        private static (string firstName, string lastName) SplitName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("Unknown", "Candidate");

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
                return (parts[0], "Candidate");

            return (parts[0], string.Join(' ', parts.Skip(1)));
        }

        /// <summary>
        /// Transitions an application to a new stage with validation.
        /// Ensures the stage exists and belongs to the application's job.
        /// </summary>
        public async Task<bool> TransitionToStageAsync(int applicationId, int newStageId)
        {
            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return false;

            // Validate the stage belongs to the application's job
            var stage = await _context.JobStages
                .FirstOrDefaultAsync(s => s.Id == newStageId && s.JobId == application.JobId);

            if (stage == null)
                return false;

            if (application.CurrentJobStageId.HasValue)
            {
                var currentStage = await _context.JobStages
                    .FirstOrDefaultAsync(s => s.Id == application.CurrentJobStageId.Value && s.JobId == application.JobId);

                if (currentStage == null)
                    return false;

                // Prevent skipping mandatory forward stages in the pipeline.
                if (stage.Order > currentStage.Order + 1)
                    return false;
            }

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
