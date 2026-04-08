namespace JobPortal.Models
{
    public enum HiringRequestStatus
    {
        Draft,
        Submitted,
        NeedsRevision,
        Approved,
        Rejected
    }

    public enum HiringRequestStage
    {
        /// <summary>Awaiting review by the Senior Talent Lead.</summary>
        Stage1,
        /// <summary>Awaiting final approval by the Senior Executive.</summary>
        Stage2
    }

    public enum EmploymentType
    {
        FullTime,
        PartTime,
        Contract
    }

    public enum HiringPriority
    {
        Low,
        Medium,
        High
    }

    public class HiringRequest
    {
        public int Id { get; set; }

        // Request content
        public string JobTitle { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int Headcount { get; set; } = 1;
        public string? Justification { get; set; }
        public string? SalaryBand { get; set; }
        public DateTime? TargetStartDate { get; set; }
        public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTime;
        public HiringPriority Priority { get; set; } = HiringPriority.Medium;

        // Workflow state
        public HiringRequestStage Stage { get; set; } = HiringRequestStage.Stage1;
        public HiringRequestStatus Status { get; set; } = HiringRequestStatus.Draft;

        // Audit
        public int CreatedByUserId { get; set; }
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        // Submission tracking
        public int? SubmittedByUserId { get; set; }
        public DateTime? SubmittedUtc { get; set; }

        // Stage 1 review — Senior Talent Lead
        public int? Stage1ReviewedByUserId { get; set; }
        public DateTime? Stage1ReviewedUtc { get; set; }
        public string? Stage1Feedback { get; set; }

        // Stage 2 review — Senior Executive
        public int? Stage2ReviewedByUserId { get; set; }
        public DateTime? Stage2ReviewedUtc { get; set; }
        public string? Stage2Feedback { get; set; }

        // Rejection (either stage)
        public string? RejectionReason { get; set; }
    }
}
