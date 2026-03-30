using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class EmailTemplate
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(10000)]
        public string BodyContent { get; set; } = string.Empty;

        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        /// <summary>Replaces {{FirstName}} placeholder with the provided name.</summary>
        public string GetParsedBody(string firstName) =>
            BodyContent.Replace("{{FirstName}}", firstName);
    }
}
