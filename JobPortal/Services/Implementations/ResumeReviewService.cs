using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ResumeReviewService : IResumeReviewService
    {
        private readonly AppDbContext _context;

        public ResumeReviewService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<(Job? Job, List<Application> Applications)> GetResumeReviewDataAsync(int jobId)
        {
            var job = await _context.Jobs.FindAsync(jobId);
            if (job == null)
                return (null, new List<Application>());

            var applications = await _context.Applications
                .Where(a => a.JobId == jobId && a.Stage == ApplicationStage.Applied)
                .OrderBy(a => a.AppliedDate)
                .ToListAsync();

            return (job, applications);
        }

        public async Task<(bool ApplicationFound, bool WrongJob, bool WrongStage)> AdvanceToScreenAsync(int applicationId, int jobId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                return (false, false, false);

            if (application.JobId != jobId)
                return (true, true, false);

            if (application.Stage != ApplicationStage.Applied)
                return (true, false, true);

            application.Stage = ApplicationStage.Screen;
            await _context.SaveChangesAsync();

            return (true, false, false);
        }

        public async Task<bool> SeedResumeReviewDataAsync(int jobId)
        {
            var jobExists = await _context.Jobs.AnyAsync(j => j.Id == jobId);
            if (!jobExists)
                return false;

            var resumes = new[]
            {
                "/uploads/resumes/resume1.pdf",
                "/uploads/resumes/resume2.pdf",
                "/uploads/resumes/resume3.pdf"
            };

            for (int i = 1; i <= 30; i++)
            {
                var candidate = new Candidate
                {
                    FirstName = "Test",
                    LastName = $"Candidate {i}",
                    Email = $"candidate{i}@test.com"
                };

                _context.Candidates.Add(candidate);
                await _context.SaveChangesAsync();

                var hasResume = i % 3 != 0;

                var application = new Application
                {
                    CandidateId = candidate.Id,
                    Name = $"{candidate.FirstName} {candidate.LastName}",
                    Email = candidate.Email,
                    JobId = jobId,
                    Stage = ApplicationStage.Applied,
                    ResumePath = hasResume ? resumes[i % resumes.Length] : null,
                    AppliedDate = DateTime.UtcNow.AddMinutes(-i)
                };

                _context.Applications.Add(application);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<string?> GetResumeInlinePathAsync(int applicationId, string webRootPath)
        {
            var application = await _context.Applications.FindAsync(applicationId);

            if (application == null || string.IsNullOrEmpty(application.ResumePath))
                return null;

            var filePath = Path.Combine(
                webRootPath,
                application.ResumePath.TrimStart('/'));

            if (!File.Exists(filePath))
                return null;

            return filePath;
        }

        public async Task<List<Application>> GetDebugResumePathApplicationsAsync()
        {
            return await _context.Applications.ToListAsync();
        }
    }
}
