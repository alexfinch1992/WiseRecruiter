using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public enum CandidateSource
    {
        Applicant,
        LinkedIn,
        Referral,
        Internal,
        Other
    }

    public class Candidate
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Application> Applications { get; set; } = new List<Application>();

        public ICollection<Scorecard> Scorecards { get; set; } = new List<Scorecard>();

        public bool IsArchived { get; set; } = false;

        public CandidateSource Source { get; set; } = CandidateSource.Applicant;
    }
}
