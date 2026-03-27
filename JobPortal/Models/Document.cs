using System.ComponentModel.DataAnnotations;

namespace JobPortal.Models
{
    public enum DocumentType
    {
        Resume = 0,
        CoverLetter = 1,
        TestScores = 2
    }

    public class Document
    {
        public int Id { get; set; }

        [Required]
        public int ApplicationId { get; set; }

        [Required]
        public string? FileName { get; set; }

        [Required]
        public string? FilePath { get; set; }

        [Required]
        public DocumentType Type { get; set; } = DocumentType.Resume;

        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        public long FileSize { get; set; } // in bytes

        // Navigation property
        public Application? Application { get; set; }
    }
}
