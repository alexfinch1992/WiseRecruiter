namespace JobPortal.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        /// <summary>Entity type, e.g. "Application" or "Job".</summary>
        public string EntityName { get; set; } = string.Empty;

        /// <summary>Primary key of the affected entity.</summary>
        public int EntityId { get; set; }

        /// <summary>Action that occurred, e.g. "StageMove", "Override", "RecommendationSubmit".</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>Human-readable diff or JSON summary of old vs new values.</summary>
        public string Changes { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Actor. Use "System_Seed" for seeded data, "Legacy_Admin" for pre-auth actions.</summary>
        public string UserId { get; set; } = string.Empty;
    }
}
