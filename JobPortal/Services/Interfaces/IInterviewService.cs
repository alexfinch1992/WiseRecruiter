using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IInterviewService
    {
        Task<Interview> CreateInterviewAsync(int candidateId, int applicationId, int jobStageId, DateTime scheduledAt);
    }
}
