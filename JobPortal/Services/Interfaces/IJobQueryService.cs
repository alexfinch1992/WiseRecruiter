using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IJobQueryService
    {
        Task<List<Job>> GetJobsAsync(IEnumerable<int>? assignedIds = null);
        Task<Job?> GetJobDetailAsync(int id, string? sort, string? dir = "asc", string? searchQuery = null);
        Task<Job?> GetJobForEditAsync(int id);
        Task<Job?> GetJobForDeleteAsync(int id);
        Task<Job?> GetJobWithTemplateAsync(int id);
        Task<bool> JobExistsAsync(int id);
        Task<Job?> GetJobByIdAsync(int id);
        Task<List<JobStage>> GetStagesForJobAsync(int jobId);
    }
}
