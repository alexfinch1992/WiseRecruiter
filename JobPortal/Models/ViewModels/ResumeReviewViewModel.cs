namespace JobPortal.Models.ViewModels
{
    public class ResumeReviewViewModel
    {
        public int JobId { get; set; }
        public string JobTitle { get; set; } = string.Empty;

        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }

        // Null when queue is empty or complete
        public int? ApplicationId { get; set; }
        public string? CandidateName { get; set; }
        public string? Email { get; set; }
        public string? ResumeUrl { get; set; }
        public bool HasResume { get; set; }

        // States
        public bool IsQueueEmpty => TotalCount == 0;
        public bool IsQueueComplete => TotalCount > 0 && CurrentIndex >= TotalCount;
    }
}
