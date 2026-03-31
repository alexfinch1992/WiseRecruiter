using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class JobService : IJobService
    {
        private readonly AppDbContext _context;

        public JobService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Job> CreateJobAsync(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            if (string.IsNullOrWhiteSpace(job.Title))
                throw new ArgumentException("Job title is required.", nameof(job.Title));

            // Add job to context and save to get the JobId
            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            // Auto-create default stages for the newly created job
            await CreateDefaultStagesAsync(job.Id);

            return job;
        }

        private async Task CreateDefaultStagesAsync(int jobId)
        {
            // Define default stages with their display order
            var defaultStages = new[]
            {
                new JobStage { JobId = jobId, Name = "Applied", Order = 1 },
                new JobStage { JobId = jobId, Name = "Interview", Order = 2 },
                new JobStage { JobId = jobId, Name = "Offer", Order = 3 }
            };

            _context.JobStages.AddRange(defaultStages);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc />
        public IReadOnlyList<CandidateStageSummaryItem> GetStageSummary(Job job)
        {
            if (job.Applications == null || !job.Applications.Any())
                return Array.Empty<CandidateStageSummaryItem>();

            var customStages = job.Stages ?? Enumerable.Empty<JobStage>();

            return job.Applications
                .GroupBy(a => GetEffectiveStageLabel(a, customStages))
                .OrderBy(g => GetEffectiveSortOrder(g.First(), customStages))
                .Select(g => new CandidateStageSummaryItem(g.Key, g.Count()))
                .ToList();
        }

        /// <summary>
        /// Returns the display label for an application's current stage.
        /// If a custom JobStage is assigned, use its name.
        /// Otherwise fall back to the ApplicationStage enum (e.g. "Applied", "Screen").
        /// </summary>
        private static string GetEffectiveStageLabel(Application app, IEnumerable<JobStage> customStages)
        {
            if (app.CurrentJobStageId != null)
            {
                var customStage = customStages.FirstOrDefault(s => s.Id == app.CurrentJobStageId);
                return customStage?.Name ?? "Unknown stage";
            }
            return app.Stage.ToString();
        }

        /// <summary>
        /// Sort order: system stages use their enum ordinal (×10) so custom stages
        /// (which sit inside the "Interview" system stage) can be interleaved naturally.
        /// </summary>
        private static int GetEffectiveSortOrder(Application app, IEnumerable<JobStage> customStages)
        {
            if (app.CurrentJobStageId != null)
            {
                var customStage = customStages.FirstOrDefault(s => s.Id == app.CurrentJobStageId);
                // Custom stages sit between Interview (20) and Offer (30) in the enum ordering
                return (int)ApplicationStage.Interview * 10 + (customStage?.Order ?? 999);
            }
            return (int)app.Stage * 10;
        }
    }
}
