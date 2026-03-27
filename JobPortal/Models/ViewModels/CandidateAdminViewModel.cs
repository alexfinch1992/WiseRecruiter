using JobPortal.Models;

namespace JobPortal.Models.ViewModels
{
    /// <summary>
    /// ViewModel for admin-only application details.
    /// Contains ALL information including sensitive internal data.
    /// This should NEVER be used to render candidate-facing views.
    /// </summary>
    public class CandidateAdminViewModel
    {
        public int Id { get; set; }
        public int CandidateId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? ResumePath { get; set; }
        public DateTime AppliedDate { get; set; }
        
        // Job and stage information
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int? CurrentJobStageId { get; set; }
        public string? CurrentStageName { get; set; }
        
        // Documents list (including all types)
        public List<DocumentDto> Documents { get; set; } = new();
        
        // Stage progression
        public List<JobStageDto> StageProgression { get; set; } = new();
        public int CurrentStageIndex { get; set; }
        public double ProgressPercentage { get; set; }
        
        // Time tracking
        public int DaysInSystem { get; set; }
        public string? TimeInSystemDisplay { get; set; }

        // Scorecards
        public List<ScorecardSummaryViewModel> Scorecards { get; set; } = new();
    }
}
