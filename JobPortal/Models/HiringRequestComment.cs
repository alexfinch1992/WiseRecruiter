namespace JobPortal.Models
{
    public class HiringRequestComment
    {
        public int Id { get; set; }

        public int HiringRequestId { get; set; }
        public HiringRequest? HiringRequest { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public string Comment { get; set; } = string.Empty;

        /// <summary>
        /// The action that triggered this comment entry.
        /// One of: "Submitted", "TalentLeadApproved", "ExecutiveApproved",
        /// "Rejected", "MoreInfoRequested".
        /// </summary>
        public string Action { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
