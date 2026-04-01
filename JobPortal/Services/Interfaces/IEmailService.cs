using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IEmailService
    {
        Task<List<EmailTemplate>> GetAllAsync();
        Task<bool> SaveTemplateAsync(EmailTemplate template);
        Task<bool> SendMockEmailAsync(int templateId, int candidateId, string userId);
    }
}
