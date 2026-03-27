using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class AdminUser
    {
        public int Id { get; set; }

        [Required]
        public string? Username { get; set; }

        [Required]
        public string? PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
