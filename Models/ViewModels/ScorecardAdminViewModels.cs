using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models.ViewModels
{
    public class ScorecardSummaryViewModel
    {
        public int Id { get; set; }
        public string SubmittedBy { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public decimal AverageScore { get; set; }
    }

    public class ScorecardResponseInputViewModel
    {
        [Required]
        public string FacetName { get; set; } = string.Empty;

        [Range(1.0, 5.0)]
        public decimal Score { get; set; }

        public string? Notes { get; set; }
    }

    public class CreateScorecardViewModel
    {
        public int ApplicationId { get; set; }
        public int CandidateId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;

        public List<ScorecardResponseInputViewModel> Responses { get; set; } = new();
    }
}
