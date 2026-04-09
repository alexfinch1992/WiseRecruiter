namespace JobPortal.Models
{
    public enum HiringRequestStatus
    {
        Draft,
        Submitted,
        TalentLeadApproved,
        ExecutiveApproved,
        Rejected
    }

    public class HiringRequest
    {
        public int Id { get; set; }

        public string RequestedByUserId { get; set; } = string.Empty;
        public ApplicationUser? RequestedByUser { get; set; }

        public string RoleTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string LevelBand { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool IsReplacement { get; set; }
        public string? ReplacementReason { get; set; }
        public int Headcount { get; set; } = 1;
        public string Justification { get; set; } = string.Empty;
        public HiringRequestStatus Status { get; set; } = HiringRequestStatus.Draft;

        // Stage 1 — Talent Lead review
        public string? TalentLeadReviewedByUserId { get; set; }
        public ApplicationUser? TalentLeadReviewedByUser { get; set; }
        public DateTime? TalentLeadReviewedUtc { get; set; }
        public string? TalentLeadNotes { get; set; }

        // Stage 2 — Executive approval
        public string? ExecutiveApprovedByUserId { get; set; }
        public ApplicationUser? ExecutiveApprovedByUser { get; set; }
        public DateTime? ExecutiveApprovedUtc { get; set; }
        public string? ExecutiveNotes { get; set; }

        // Rejection (either stage)
        public string? RejectedByUserId { get; set; }
        public ApplicationUser? RejectedByUser { get; set; }
        public DateTime? RejectedUtc { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
