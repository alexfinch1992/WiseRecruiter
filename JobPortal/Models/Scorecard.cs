using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class Scorecard
    {
        public int Id { get; set; }

        public int CandidateId { get; set; }

        [Required]
        public string SubmittedBy { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        public Candidate? Candidate { get; set; }

        public string? OverallRecommendation { get; set; }

        public ICollection<ScorecardResponse> Responses { get; set; } = new List<ScorecardResponse>();
    }
}
