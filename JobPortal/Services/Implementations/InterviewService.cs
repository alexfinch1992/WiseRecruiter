using JobPortal.Data;
using JobPortal.Models;
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
    }
}
