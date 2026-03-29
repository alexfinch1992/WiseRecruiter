using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    public class Stage2ReviewViewModel
    {
        public int ApplicationId { get; set; }
        public string? CandidateName { get; set; }
        public string? CandidateEmail { get; set; }
        public string? JobTitle { get; set; }
        public CandidateRecommendation? Recommendation { get; set; }
    }
}
