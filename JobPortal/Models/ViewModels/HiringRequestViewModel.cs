using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models.ViewModels
{
    public class HiringRequestViewModel
    {
        [Required]
        public string RoleTitle { get; set; } = string.Empty;

        [Required]
        public string Department { get; set; } = string.Empty;

        [Required]
        public string LevelBand { get; set; } = string.Empty;

        [Required]
        public string Location { get; set; } = string.Empty;

        public bool IsReplacement { get; set; }
        public string? ReplacementReason { get; set; }

        [Range(1, int.MaxValue)]
        public int Headcount { get; set; } = 1;

        [Required]
        public string Justification { get; set; } = string.Empty;
    }
}
