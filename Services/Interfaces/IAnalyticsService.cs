namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// DTO for analytics data. Prevents view from depending on ORM entities.
    /// Consolidates multiple queries into single method.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Gets comprehensive analytics data for all jobs.
        /// Single consolidated query replaces 4 separate queries.
        /// </summary>
        Task<AnalyticsReportDto> GetAnalyticsReportAsync();

        /// <summary>
        /// Gets analytics data for a specific job.
        /// </summary>
        Task<JobAnalyticsDto> GetJobAnalyticsAsync(int jobId);
    }

    /// <summary>
    /// Data transfer object for analytics dashboard.
    /// Safe for direct view access (immutable from view perspective).
    /// </summary>
    public class AnalyticsReportDto
    {
        public int TotalApplications { get; set; }
        public int TotalJobs { get; set; }
        public double AverageApplicationsPerJob { get; set; }
        public List<CandidateByStageDto> CandidatesByStage { get; set; } = new();
        public List<JobStatDto> JobStats { get; set; } = new();
        public List<StageStatDto> StageStats { get; set; } = new();
        public List<ApplicationTrendDto> ApplicationTrends { get; set; } = new();
    }

    public class CandidateByStageDto
    {
        public string? StageName { get; set; }
        public int Count { get; set; }
        public double PercentageOfTotal { get; set; }
    }

    public class JobStatDto
    {
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int TotalApplications { get; set; }
    }

    public class StageStatDto
    {
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int StageId { get; set; }
        public string? StageName { get; set; }
        public int CandidateCount { get; set; }
    }

    public class ApplicationTrendDto
    {
        public DateTime Date { get; set; }
        public int ApplicationsReceived { get; set; }
        public int CumulativeTotal { get; set; }
    }

    public class JobAnalyticsDto
    {
        public int JobId { get; set; }
        public string? JobTitle { get; set; }
        public int TotalApplications { get; set; }
        public List<CandidateByStageDto> StageDistribution { get; set; } = new();
    }
}
