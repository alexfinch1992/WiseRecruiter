using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class ScorecardResponse
    {
        public int Id { get; set; }

        public int ScorecardId { get; set; }

        [Required]
        public string FacetName { get; set; } = string.Empty;

        public decimal Score { get; set; }

        public string? Notes { get; set; }

        public Scorecard? Scorecard { get; set; }
    }
}
