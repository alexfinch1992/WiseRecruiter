using JobPortal.Models;
using JobPortal.Services.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IJobCommandService
    {
        Task<Job> CreateJobAsync(Job job);
        Task UpdateJobAsync(Job job);
        Task DeleteJobAsync(int id);
        Task<JobStageCommandResult> AddStageAsync(int jobId, string stageName);
        Task<JobStageCommandResult> RemoveStageAsync(int stageId);
        Task<JobStageCommandResult> MoveStageAsync(int stageId, int jobId, string direction);
        Task AssignRecruiterAsync(int jobId, string userId, string role = "Recruiter");
        Task DeactivateRecruiterAsync(int jobUserId);
        Task ToggleReviewerAsync(int jobId, string userId);
        Task SeedCandidatesAsync(int jobId, int count = 150);
    }
}
