using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class JobStageCommandService : IJobStageCommandService
    {
        private readonly AppDbContext _context;

        public JobStageCommandService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
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
                Name  = stageName,
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

            // Swap Order values
            var temp = allStages[currentIndex].Order;
            allStages[currentIndex].Order = allStages[swapIndex].Order;
            allStages[swapIndex].Order = temp;

            _context.Update(allStages[currentIndex]);
            _context.Update(allStages[swapIndex]);
            await _context.SaveChangesAsync();

            return new JobStageCommandResult { Success = true };
        }
    }
}
