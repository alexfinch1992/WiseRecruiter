namespace JobPortal.Models.ViewModels
{
    public class CandidateApplicationDto
    {
        public int ApplicationId { get; set; }
        public int JobId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? JobTitle { get; set; }
        public string CurrentStage { get; set; } = string.Empty;
        public DateTime AppliedDate { get; set; }
        public bool IsNonCompliant { get; set; }
        public string? Stage1RecommendationStatus { get; set; }
    }
}
