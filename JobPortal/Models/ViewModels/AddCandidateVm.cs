using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models.ViewModels
{
    public class AddCandidateVm
    {
        public int JobId { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        public IFormFile? Resume { get; set; }

        public string? JobTitle { get; set; }
    }
}
