using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class InterviewCommandService : IInterviewCommandService
    {
        private readonly AppDbContext          _context;
        private readonly IInterviewService     _interviewService;
        private readonly IRecommendationService _recommendationService;

        public InterviewCommandService(
            AppDbContext           context,
            IInterviewService      interviewService,
            IRecommendationService recommendationService)
        {
            _context               = context               ?? throw new ArgumentNullException(nameof(context));
            _interviewService      = interviewService      ?? throw new ArgumentNullException(nameof(interviewService));
            _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
        }

        public async Task<InterviewCreateResult> CreateAsync(
            int        candidateId,
            int        applicationId,
            string     selectedStage,
            DateTime   scheduledAt,
            List<int>? selectedInterviewerIds,
            bool       proceedWithoutApproval,
            string?    bypassReason,
            string     userId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null || application.CandidateId != candidateId)
                return new InterviewCreateResult { Success = false, Error = InterviewCreateError.InvalidApplication };

            int resolvedJobStageId = 0;
            if (selectedStage.StartsWith("stage:"))
            {
                if (!int.TryParse(selectedStage["stage:".Length..], out resolvedJobStageId))
                    return new InterviewCreateResult { Success = false, Error = InterviewCreateError.InvalidStageFormat };
            }
            else if (!selectedStage.StartsWith("enum:"))
            {
                return new InterviewCreateResult { Success = false, Error = InterviewCreateError.InvalidStageFormat };
            }

            // datetime-local inputs arrive as DateTimeKind.Unspecified (browser local time); convert to UTC.
            // If already UTC (e.g. from API callers), preserve as-is.
            var scheduledAtUtc = scheduledAt.Kind == DateTimeKind.Utc
                ? scheduledAt
                : DateTime.SpecifyKind(scheduledAt, DateTimeKind.Local).ToUniversalTime();

            var interview = await _interviewService.CreateInterviewAsync(candidateId, applicationId, resolvedJobStageId, scheduledAtUtc);

            if (selectedInterviewerIds != null)
            {
                selectedInterviewerIds = selectedInterviewerIds
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();
            }

            if (selectedInterviewerIds != null && selectedInterviewerIds.Any())
            {
                var validIds = await _context.AdminUsers
                    .Where(a => selectedInterviewerIds.Contains(a.Id))
                    .Select(a => a.Id)
                    .ToListAsync();

                if (validIds.Count != selectedInterviewerIds.Count)
                    return new InterviewCreateResult { Success = false, Error = InterviewCreateError.InvalidInterviewer };

                foreach (var adminUserId in selectedInterviewerIds)
                {
                    _context.InterviewInterviewers.Add(new InterviewInterviewer
                    {
                        InterviewId = interview.Id,
                        AdminUserId = adminUserId
                    });
                }
                await _context.SaveChangesAsync();
            }

            await _recommendationService.GetOrPrepareStage1RecommendationAsync(applicationId, proceedWithoutApproval, bypassReason, userId);

            return new InterviewCreateResult { Success = true, ApplicationId = applicationId };
        }
    }
}
