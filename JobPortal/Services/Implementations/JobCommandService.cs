using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class JobCommandService : IJobCommandService
    {
        private readonly AppDbContext _context;

        public JobCommandService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Job> CreateJobAsync(Job job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            if (string.IsNullOrWhiteSpace(job.Title))
                throw new ArgumentException("Job title is required.", nameof(job.Title));

            _context.Jobs.Add(job);
            await _context.SaveChangesAsync();

            await CreateDefaultStagesAsync(job.Id);

            return job;
        }

        public async Task UpdateJobAsync(Job job)
        {
            _context.Update(job);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteJobAsync(int id)
        {
            var job = await _context.Jobs.FindAsync(id);
            if (job != null)
            {
                _context.Jobs.Remove(job);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<JobStageCommandResult> AddStageAsync(int jobId, string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName))
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.NameEmpty };

            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.JobNotFound };

            var existingStage = await _context.JobStages
                .FirstOrDefaultAsync(s => s.JobId == jobId && s.Name == stageName);

            if (existingStage != null)
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.StageAlreadyExists };

            var maxOrder = await _context.JobStages
                .Where(s => s.JobId == jobId)
                .MaxAsync(s => (int?)s.Order) ?? -1;

            var newStage = new JobStage
            {
                JobId = jobId,
                Name = stageName,
                Order = maxOrder + 1
            };

            _context.JobStages.Add(newStage);
            await _context.SaveChangesAsync();

            return new JobStageCommandResult { Success = true };
        }

        public async Task<JobStageCommandResult> RemoveStageAsync(int stageId)
        {
            var stage = await _context.JobStages.FindAsync(stageId);
            if (stage == null)
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.StageNotFound };

            _context.JobStages.Remove(stage);
            await _context.SaveChangesAsync();

            return new JobStageCommandResult { Success = true };
        }

        public async Task<JobStageCommandResult> MoveStageAsync(int stageId, int jobId, string direction)
        {
            var stage = await _context.JobStages.FirstOrDefaultAsync(s => s.Id == stageId && s.JobId == jobId);
            if (stage == null)
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.StageNotFound };

            var allStages = await _context.JobStages
                .Where(s => s.JobId == jobId)
                .OrderBy(s => s.Order)
                .ToListAsync();

            var currentIndex = allStages.FindIndex(s => s.Id == stageId);
            if (currentIndex == -1)
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.StageNotFound };

            int swapIndex = -1;
            if (direction == "up" && currentIndex > 0)
                swapIndex = currentIndex - 1;
            else if (direction == "down" && currentIndex < allStages.Count - 1)
                swapIndex = currentIndex + 1;

            if (swapIndex == -1)
                return new JobStageCommandResult { Success = false, Error = JobStageCommandError.CannotMove };

            var temp = allStages[currentIndex].Order;
            allStages[currentIndex].Order = allStages[swapIndex].Order;
            allStages[swapIndex].Order = temp;

            _context.Update(allStages[currentIndex]);
            _context.Update(allStages[swapIndex]);
            await _context.SaveChangesAsync();

            return new JobStageCommandResult { Success = true };
        }

        private async Task CreateDefaultStagesAsync(int jobId)
        {
            var defaultStages = new[]
            {
                new JobStage { JobId = jobId, Name = "Applied", Order = 1 },
                new JobStage { JobId = jobId, Name = "Interview", Order = 2 },
                new JobStage { JobId = jobId, Name = "Offer", Order = 3 }
            };

            _context.JobStages.AddRange(defaultStages);
            await _context.SaveChangesAsync();
        }

        public async Task AssignRecruiterAsync(int jobId, string userId, string role = "Recruiter")
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("UserId is required.", nameof(userId));

            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                throw new ArgumentException("Job not found.", nameof(jobId));

            _context.JobUsers.Add(new JobUser
            {
                JobId = jobId,
                UserId = userId,
                Role = role,
                IsActive = true
            });
            await _context.SaveChangesAsync();
        }

        public async Task DeactivateRecruiterAsync(int jobUserId)
        {
            var jobUser = await _context.JobUsers.FindAsync(jobUserId);
            if (jobUser == null)
                throw new ArgumentException("JobUser not found.", nameof(jobUserId));

            // Check if removing this recruiter would leave the job with none
            var otherActiveCount = await _context.JobUsers
                .CountAsync(ju => ju.JobId == jobUser.JobId && ju.IsActive && ju.Id != jobUserId);

            if (otherActiveCount == 0)
            {
                throw new InvalidOperationException(
                    "A job must have at least one active recruiter assigned.");
            }

            jobUser.IsActive = false;
            await _context.SaveChangesAsync();
        }

        private async Task EnsureJobHasActiveRecruiterAsync(int jobId)
        {
            var hasRecruiter = await _context.JobUsers
                .AnyAsync(ju => ju.JobId == jobId && ju.IsActive);

            if (!hasRecruiter)
            {
                throw new InvalidOperationException(
                    "A job must have at least one active recruiter assigned.");
            }
        }
    }
}
