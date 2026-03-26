using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class Application
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        public string? Email { get; set; }

        [Required]
        public string? City { get; set; }

        public string? ResumePath { get; set; }

        public int? CurrentJobStageId { get; set; }

        public DateTime AppliedDate { get; set; } = DateTime.UtcNow;

        public int JobId { get; set; }

        // Admin-only sensitive field for interview feedback and notes
        public string? InterviewNotes { get; set; }

        public Job? Job { get; set; }

        public JobStage? CurrentStage { get; set; }

        // Navigation property for documents
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}