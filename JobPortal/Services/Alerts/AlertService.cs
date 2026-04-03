using JobPortal.Data;
using JobPortal.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Alerts
{
    public class AlertService
    {
        private readonly AppDbContext _context;
        private readonly AlertRecipientResolver _resolver;

        public AlertService(
            AppDbContext context,
            AlertRecipientResolver resolver)
        {
            _context = context;
            _resolver = resolver;
        }

        public async Task CreateJobAlertAsync(
            int jobId,
            string type,
            string message,
            string? linkUrl = null,
            int? relatedEntityId = null,
            string? relatedEntityType = null)
        {
            if (relatedEntityId.HasValue && relatedEntityType != null)
            {
                var exists = await _context.Alerts.AnyAsync(a =>
                    a.Type == type &&
                    a.RelatedEntityId == relatedEntityId &&
                    a.RelatedEntityType == relatedEntityType);

                if (exists)
                    return;
            }

            var userIds = await _resolver.ResolveUserIdsAsync(jobId);

            var alerts = userIds.Select(userId => new Alert
            {
                UserId = userId,
                Type = type,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                LinkUrl = linkUrl,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType
            });

            _context.Alerts.AddRange(alerts);
            await _context.SaveChangesAsync();
        }

        public async Task CreateAlertsAsync(
            List<string> userIds,
            string type,
            string message,
            string? linkUrl = null,
            int? relatedEntityId = null,
            string? relatedEntityType = null)
        {
            if (relatedEntityId.HasValue && relatedEntityType != null)
            {
                var exists = await _context.Alerts.AnyAsync(a =>
                    a.Type == type &&
                    a.RelatedEntityId == relatedEntityId &&
                    a.RelatedEntityType == relatedEntityType);

                if (exists)
                    return;
            }

            var alerts = userIds.Select(userId => new Alert
            {
                UserId = userId,
                Type = type,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                LinkUrl = linkUrl,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType
            });

            _context.Alerts.AddRange(alerts);
            await _context.SaveChangesAsync();
        }
    }
}
