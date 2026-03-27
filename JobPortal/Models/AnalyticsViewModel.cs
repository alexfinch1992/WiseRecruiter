using JobPortal.Models.ViewModels;

namespace JobPortal.Models
{
    public class AnalyticsViewModel
    {
        public List<CandidateByStageInfo> CandidatesByStage { get; set; } = new();
        public List<JobStatInfo> JobStats { get; set; } = new();
        public List<StageStatInfo> StageStats { get; set; } = new();
        public List<ApplicationOverTimeInfo> ApplicationsOverTime { get; set; } = new();
        public int TotalApplications { get; set; }
        public int TotalJobs { get; set; }
        public List<UpcomingInterviewDto> UpcomingInterviews { get; set; } = new();
    }

    public class CandidateByStageInfo
    {
        public string? StageName { get; set; }
        public int Count { get; set; }
    }

    public class JobStatInfo
    {
        public string? JobTitle { get; set; }
        public int TotalApplications { get; set; }
    }

    public class StageStatInfo
    {
        public string? JobTitle { get; set; }
        public string? StageName { get; set; }
        public int CandidateCount { get; set; }
    }

    public class ApplicationOverTimeInfo
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }
}
