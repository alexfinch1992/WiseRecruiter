using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public enum ApplicationStatus
    {
        Active,
        Rejected,
        Withdrawn
    }

    public enum ApplicationStage
    {
        Applied,
        Screen,
        Interview,
        Offer,
        Hired,
        Rejected
    }

    public class Application
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? City { get; set; }

        public string? ResumePath { get; set; }

        // Resume metadata
        public string? OriginalFileName { get; set; }
        public string? ResumeContentType { get; set; }
        public DateTime? ResumeUploadDate { get; set; }

        public int? CurrentJobStageId { get; set; }

        public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

        public int JobId { get; set; }

        public int CandidateId { get; set; }

        // Admin-only sensitive field for interview feedback and notes
        public string? InterviewNotes { get; set; }

        public ApplicationStage Stage { get; set; } = ApplicationStage.Applied;

        public Job? Job { get; set; }

        public Candidate? Candidate { get; set; }

        public JobStage? CurrentStage { get; set; }

        // Navigation property for documents
        public ICollection<Document> Documents { get; set; } = new List<Document>();

        // Ownership / traceability fields (pre-multi-tenant)
        public string CreatedByUserId { get; set; } = "System_Seed";
        public string? AssignedToUserId { get; set; }

        // Rejection tracking
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Active;
        public string? RejectionReason { get; set; }
        public string? RejectionNotes { get; set; }

        // Stage transition audit: set when candidate is moved past Screen without a Stage 1 approval
        public bool MovedWithoutStage1Approval { get; set; }
    }
}