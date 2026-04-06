using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class InterviewService : IInterviewService
    {
        private readonly AppDbContext _context;

        public InterviewService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Interview> CreateInterviewAsync(int candidateId, int applicationId, int jobStageId, DateTime scheduledAt)
        {
            var candidateExists = await _context.Candidates.AnyAsync(c => c.Id == candidateId);
            if (!candidateExists)
                throw new InvalidOperationException($"Candidate with Id {candidateId} not found.");

            var applicationExists = await _context.Applications.AnyAsync(a => a.Id == applicationId);
            if (!applicationExists)
                throw new InvalidOperationException($"Application with Id {applicationId} not found.");

            var jobStageExists = jobStageId > 0 && await _context.JobStages.AnyAsync(js => js.Id == jobStageId);
            if (jobStageId > 0 && !jobStageExists)
                throw new InvalidOperationException($"JobStage with Id {jobStageId} not found.");

            var interview = new Interview
            {
                CandidateId = candidateId,
                ApplicationId = applicationId,
                JobStageId = jobStageId,
                ScheduledAt = scheduledAt.Kind == DateTimeKind.Utc ? scheduledAt : scheduledAt.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Interviews.Add(interview);
            await _context.SaveChangesAsync();

            return interview;
        }

        public async Task<List<InterviewSummaryDto>> GetInterviewSummariesAsync(int candidateId, string fallbackStageName)
        {
            var now = DateTime.UtcNow;
            var interviews = await _context.Interviews
                .Include(i => i.JobStage)
                .Include(i => i.InterviewInterviewers)
                    .ThenInclude(ii => ii.AdminUser)
                .Where(i => i.CandidateId == candidateId)
                .ToListAsync();

            return interviews
                .Select(i => new InterviewSummaryDto
                {
                    Id = i.Id,
                    ScheduledAt = i.ScheduledAt,
                    StageName = i.JobStage?.Name ?? fallbackStageName,
                    IsCancelled = i.IsCancelled,
                    CompletedAt = i.CompletedAt,
                    InterviewerNames = i.InterviewInterviewers
                        .Where(ii => ii.AdminUser != null)
                        .Select(ii => ii.AdminUser!.Username ?? string.Empty)
                        .ToList()
                })
                .OrderBy(i => i.IsCancelled ? 3 : (i.CompletedAt != null ? 2 : (i.ScheduledAt < now ? 1 : 0)))
                .ThenBy(i => i.ScheduledAt)
                .ToList();
        }

        public async Task<InterviewSchedulingData> GetInterviewSchedulingDataAsync(int candidateId)
        {
            var candidateApplications = await _context.Applications
                .Where(a => a.CandidateId == candidateId)
                .Include(a => a.Job)
                .ToListAsync();

            var adminUsers = await _context.AdminUsers.OrderBy(a => a.Username).ToListAsync();

            return new InterviewSchedulingData(candidateApplications, adminUsers);
        }

        public async Task<List<UpcomingInterviewDto>> GetUpcomingInterviewsAsync()
        {
            var interviews = await _context.Interviews
                .Include(i => i.Candidate)
                .Include(i => i.Application).ThenInclude(a => a.Job)
                .Include(i => i.JobStage)
                .Include(i => i.InterviewInterviewers).ThenInclude(ii => ii.AdminUser)
                .Where(i => !i.IsCancelled && i.CompletedAt == null && i.ScheduledAt >= DateTime.UtcNow)
                .OrderBy(i => i.ScheduledAt)
                .ToListAsync();

            return interviews.Select(i => new UpcomingInterviewDto
            {
                InterviewId = i.Id,
                CandidateName = i.Candidate != null ? i.Candidate.FirstName + " " + i.Candidate.LastName : "Unknown",
                JobTitle = i.Application?.Job?.Title ?? "Unknown",
                StageName = i.JobStage?.Name ?? i.Application?.Stage.ToString() ?? string.Empty,
                ScheduledAt = i.ScheduledAt,
                InterviewerNames = i.InterviewInterviewers
                    .Where(ii => ii.AdminUser != null)
                    .Select(ii => ii.AdminUser!.Username ?? string.Empty)
                    .ToList()
            }).ToList();
        }
    }
}
