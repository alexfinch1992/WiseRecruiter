using JobPortal.Data;
using JobPortal.Models;
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
    }
}
