using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IJobQueryService
    {
        Task<List<Job>> GetJobsAsync(IEnumerable<int>? assignedIds = null);
        Task<Job?> GetJobDetailAsync(int id, string? sort);
        Task<Job?> GetJobForEditAsync(int id);
        Task<Job?> GetJobForDeleteAsync(int id);
        Task<bool> JobExistsAsync(int id);
    }
}
