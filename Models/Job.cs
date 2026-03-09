using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }

        public string? Description { get; set; }

        public ICollection<Application>? Applications { get; set; }
        public ICollection<JobStage>? Stages { get; set; }
    }
}