using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        public int? ScorecardTemplateId { get; set; }

        public ICollection<Application>? Applications { get; set; }
        public ICollection<JobStage>? Stages { get; set; }
        public ScorecardTemplate? ScorecardTemplate { get; set; }

        // Ownership / traceability fields (pre-multi-tenant)
        public string CreatedByUserId { get; set; } = "System_Seed";
        public string? AssignedToUserId { get; set; }

        public string? OwnerUserId { get; set; }
        public ApplicationUser? OwnerUser { get; set; }
    }
}