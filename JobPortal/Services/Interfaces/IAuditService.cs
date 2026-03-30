namespace JobPortal.Services.Interfaces
{
    public interface IAuditService
    {
        /// <summary>
        /// Persist a single audit entry. Immediately commits to the AuditLogs table.
        /// </summary>
        Task LogAsync(string entityName, int entityId, string action, string changes, string userId);
    }
}
