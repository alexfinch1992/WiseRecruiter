using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Interfaces
{
    public interface IResumeReviewService
    {
        Task<(Job? Job, List<Application> Applications)> GetResumeReviewDataAsync(int jobId);
        Task<(bool ApplicationFound, bool WrongJob, bool WrongStage)> AdvanceToScreenAsync(int applicationId, int jobId);
        Task<bool> SeedResumeReviewDataAsync(int jobId);
        Task<string?> GetResumeInlinePathAsync(int applicationId, IFileUploadService fileUploadService);
        Task<List<Application>> GetDebugResumePathApplicationsAsync();
    }
}
