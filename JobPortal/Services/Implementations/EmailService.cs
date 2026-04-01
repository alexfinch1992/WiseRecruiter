using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class EmailService : IEmailService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _auditService;

        public EmailService(AppDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<List<EmailTemplate>> GetAllAsync()
        {
            return await _context.EmailTemplates
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<bool> SaveTemplateAsync(EmailTemplate template)
        {
            template.LastModified = DateTime.UtcNow;

            if (template.Id == 0)
            {
                _context.EmailTemplates.Add(template);
            }
            else
            {
                var existing = await _context.EmailTemplates.FindAsync(template.Id);
                if (existing == null)
                    return false;
                existing.Name         = template.Name;
                existing.Subject      = template.Subject;
                existing.BodyContent  = template.BodyContent;
                existing.LastModified = template.LastModified;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SendMockEmailAsync(int templateId, int candidateId, string userId)
        {
            var template  = await _context.EmailTemplates.FindAsync(templateId);
            var candidate = await _context.Candidates.FindAsync(candidateId);

            if (template == null || candidate == null)
                return false;

            await _auditService.LogAsync(
                entityName: "EmailTemplate",
                entityId:   templateId,
                action:     "EmailSent",
                changes:    $"Template: {template.Name} | Recipient: {candidate.Email} | Candidate: {candidate.FirstName} {candidate.LastName}",
                userId:     userId);

            return true;
        }
    }
}
