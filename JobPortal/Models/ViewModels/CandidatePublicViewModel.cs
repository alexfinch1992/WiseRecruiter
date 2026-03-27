using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    /// <summary>
    /// ViewModel for public candidate-facing application details.
    /// Contains ONLY information safe to display to candidates.
    /// </summary>
    public class CandidatePublicViewModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? ResumePath { get; set; }
        public DateTime AppliedDate { get; set; }
        
        // Job and stage information (public)
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int? CurrentJobStageId { get; set; }
        public string? CurrentStageName { get; set; }
        
        // Documents list
        public List<DocumentDto> Documents { get; set; } = new();
        
        // Stage progression for visual reference
        public List<JobStageDto> StageProgression { get; set; } = new();
        public int CurrentStageIndex { get; set; }
        public double ProgressPercentage { get; set; }
    }

    public class DocumentDto
    {
        public int Id { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public DocumentType Type { get; set; }
        public long FileSize { get; set; }
        public DateTime UploadDate { get; set; }
    }

    public class JobStageDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Order { get; set; }
    }
}
