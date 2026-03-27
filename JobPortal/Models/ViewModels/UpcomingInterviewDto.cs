namespace JobPortal.Models.ViewModels
{
    public class UpcomingInterviewDto
    {
        public int InterviewId { get; set; }
        public string CandidateName { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string StageName { get; set; } = string.Empty;
        public DateTime ScheduledAt { get; set; }
        public List<string> InterviewerNames { get; set; } = new();
    }
}
