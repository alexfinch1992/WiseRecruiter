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

        public Job? Job { get; set; }

        public JobStage? CurrentStage { get; set; }
    }
}