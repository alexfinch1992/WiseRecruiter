using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;

        public AuditService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task LogAsync(string entityName, int entityId, string action, string changes, string userId)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                EntityName = entityName,
                EntityId   = entityId,
                Action     = action,
                Changes    = changes,
                Timestamp  = DateTime.UtcNow,
                UserId     = userId
            });
            await _context.SaveChangesAsync();
        }
    }
}
