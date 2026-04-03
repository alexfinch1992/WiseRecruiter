using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models.ViewModels
{
    public class CreateJobViewModel
    {
        [Required]
        [StringLength(200)]
        public string? Title { get; set; }

        [StringLength(10000)]
        public string? Description { get; set; }

        public int? ScorecardTemplateId { get; set; }

        public string? OwnerUserId { get; set; }

        public List<string> ReviewerUserIds { get; set; } = new();
    }
}
