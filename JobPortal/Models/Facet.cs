using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public class Facet
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public string? NotesPlaceholder { get; set; }

        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        public bool IsActive { get; set; } = true;
        public int DisplayOrder { get; set; }
    }
}
