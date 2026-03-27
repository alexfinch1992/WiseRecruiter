namespace JobPortal.Models
{
    public enum RecommendationStatus
    {
        Draft,
        Submitted,
        NeedsRevision,
        Approved,
        Rejected
    }

    public enum RecommendationStage
    {
        Stage1
    }

    public class CandidateRecommendation
    {
        public int Id { get; set; }

        public int ApplicationId { get; set; }
        public Application? Application { get; set; }

        public RecommendationStage Stage { get; set; } = RecommendationStage.Stage1;
        public RecommendationStatus Status { get; set; } = RecommendationStatus.Draft;

        public int? SubmittedByUserId { get; set; }
        public int? ReviewedByUserId { get; set; }

        public DateTime? SubmittedUtc { get; set; }
        public DateTime? ReviewedUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        // Bypass tracking
        public bool BypassedApproval { get; set; } = false;
        public string? BypassReason { get; set; }
        public int? BypassedByUserId { get; set; }
        public DateTime? BypassedUtc { get; set; }

        // Stage 1 recommendation content
        public string? Summary { get; set; }
        public string? CareerTrajectory { get; set; }
        public string? ExperienceFit { get; set; }
        public string? Concerns { get; set; }

        public string? CognitiveNotes { get; set; }
        public string? PersonalityNotes { get; set; }
        public string? TechnicalNotes { get; set; }

        public string? ProposedInterviewersNotes { get; set; }
    }
}
