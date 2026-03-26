using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class Candidate
    {
        public int Id { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        public string Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Application> Applications { get; set; } = new List<Application>();

        public ICollection<Scorecard> Scorecards { get; set; } = new List<Scorecard>();
    }
}
