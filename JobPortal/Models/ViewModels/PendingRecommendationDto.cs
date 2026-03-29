using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    public class PendingRecommendationDto
    {
        public int ApplicationId { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        public string? SubmittedByUsername { get; set; }
        public string? Summary { get; set; }
        public RecommendationStatus Status { get; set; }
        public RecommendationStage Stage { get; set; }
    }
}
