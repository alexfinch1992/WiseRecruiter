using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Implementation of IAnalyticsService.
    /// Consolidates analytics queries that were previously scattered across view and controller.
    /// Enables single source of truth for reporting logic.
    /// </summary>
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;

        public AnalyticsService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<AnalyticsReportDto> GetAnalyticsReportAsync()
        {
            // All queries executed efficiently
            var jobs = await _context.Jobs.ToListAsync();
            var applications = await _context.Applications
                .Include(a => a.CurrentStage)
                .ToListAsync();
            var stages = await _context.JobStages.ToListAsync();

            var totalApplications = applications.Count;
            var totalJobs = jobs.Count;
            var avgApps = totalJobs > 0 ? (double)totalApplications / totalJobs : 0;

            // Compute aggregations in service, not view
            var candidatesByStage = stages
                .Select(s => new CandidateByStageDto
                {
                    StageName = s.Name,
                    Count = applications.Count(a => a.CurrentJobStageId == s.Id),
                    PercentageOfTotal = totalApplications > 0
                        ? Math.Round((double)applications.Count(a => a.CurrentJobStageId == s.Id) / totalApplications * 100, 1)
                        : 0
                })
                .ToList();

            var jobStats = jobs
                .Select(j => new JobStatDto
                {
                    JobId = j.Id,
                    JobTitle = j.Title,
                    TotalApplications = applications.Count(a => a.JobId == j.Id)
                })
                .OrderByDescending(x => x.TotalApplications)
                .ToList();

            var stageStats = stages
                .GroupJoin(jobs, s => s.JobId, j => j.Id, (s, j) => new { Stage = s, Job = j.FirstOrDefault() })
                .Select(x => new StageStatDto
                {
                    JobId = x.Stage.JobId,
                    JobTitle = x.Job?.Title,
                    StageId = x.Stage.Id,
                    StageName = x.Stage.Name,
                    CandidateCount = applications.Count(a => a.CurrentJobStageId == x.Stage.Id)
                })
                .OrderBy(x => x.JobTitle)
                .ThenBy(x => x.StageName)
                .ToList();

            var trends = applications
                .GroupBy(a => a.AppliedDate.Date)
                .Select(g => new ApplicationTrendDto
                {
                    Date = g.Key,
                    ApplicationsReceived = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            // Add cumulative totals
            var cumulativeTotal = 0;
            foreach (var trend in trends)
            {
                cumulativeTotal += trend.ApplicationsReceived;
                trend.CumulativeTotal = cumulativeTotal;
            }

            return new AnalyticsReportDto
            {
                TotalApplications = totalApplications,
                TotalJobs = totalJobs,
                AverageApplicationsPerJob = avgApps,
                CandidatesByStage = candidatesByStage,
                JobStats = jobStats,
                StageStats = stageStats,
                ApplicationTrends = trends
            };
        }

        public async Task<JobAnalyticsDto> GetJobAnalyticsAsync(int jobId)
        {
            var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null)
                throw new KeyNotFoundException($"Job with ID {jobId} not found");

            var applications = await _context.Applications
                .Where(a => a.JobId == jobId)
                .Include(a => a.CurrentStage)
                .ToListAsync();

            var stages = await _context.JobStages
                .Where(s => s.JobId == jobId)
                .ToListAsync();

            var totalApplications = applications.Count;

            var stageDistribution = stages
                .Select(s => new CandidateByStageDto
                {
                    StageName = s.Name,
                    Count = applications.Count(a => a.CurrentJobStageId == s.Id),
                    PercentageOfTotal = totalApplications > 0
                        ? Math.Round((double)applications.Count(a => a.CurrentJobStageId == s.Id) / totalApplications * 100, 1)
                        : 0
                })
                .ToList();

            return new JobAnalyticsDto
            {
                JobId = jobId,
                JobTitle = job.Title,
                TotalApplications = totalApplications,
                StageDistribution = stageDistribution
            };
        }
    }
}
