using System.Security.Claims;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    /// <summary>
    /// Thin orchestrator: delegates each concern to a focused sub-service and assembles
    /// the final <see cref="CandidateAdminViewModel"/>.
    /// </summary>
    public class CandidateDetailsService : ICandidateDetailsService
    {
        private readonly ICandidateCoreService _candidateCoreService;
        private readonly IScorecardSummaryService _scorecardSummaryService;
        private readonly IScorecardAnalyticsService _scorecardAnalyticsService;
        private readonly IInterviewService _interviewService;
        private readonly IRelatedApplicationsService _relatedApplicationsService;
        private readonly IRecommendationSummaryService _recommendationSummaryService;
        private readonly IHiringPipelineService _hiringPipelineService;

        public CandidateDetailsService(
            ICandidateCoreService candidateCoreService,
            IScorecardSummaryService scorecardSummaryService,
            IScorecardAnalyticsService scorecardAnalyticsService,
            IInterviewService interviewService,
            IRelatedApplicationsService relatedApplicationsService,
            IRecommendationSummaryService recommendationSummaryService,
            IHiringPipelineService hiringPipelineService)
        {
            _candidateCoreService     = candidateCoreService     ?? throw new ArgumentNullException(nameof(candidateCoreService));
            _scorecardSummaryService  = scorecardSummaryService  ?? throw new ArgumentNullException(nameof(scorecardSummaryService));
            _scorecardAnalyticsService = scorecardAnalyticsService ?? throw new ArgumentNullException(nameof(scorecardAnalyticsService));
            _interviewService         = interviewService         ?? throw new ArgumentNullException(nameof(interviewService));
            _relatedApplicationsService = relatedApplicationsService ?? throw new ArgumentNullException(nameof(relatedApplicationsService));
            _recommendationSummaryService = recommendationSummaryService ?? throw new ArgumentNullException(nameof(recommendationSummaryService));
            _hiringPipelineService    = hiringPipelineService    ?? throw new ArgumentNullException(nameof(hiringPipelineService));
        }

        public async Task<CandidateAdminViewModel?> GetCandidateDetailsAsync(
            int applicationId,
            ClaimsPrincipal user,
            int? stageApprovalWarnApplicationId = null)
        {
            // 1. Load application with all includes + RBAC check
            var application = await _candidateCoreService.LoadApplicationAsync(applicationId, user);
            if (application == null)
                return null;

            // 2. Build core view model fields from already-loaded data (no extra queries)
            var daysInSystem = (DateTime.UtcNow - application.AppliedDate).Days;
            var timeInSystemDisplay = daysInSystem switch
            {
                0 => "Today",
                1 => "1 day",
                _ => $"{daysInSystem} days"
            };

            var viewModel = new CandidateAdminViewModel
            {
                Id = application.Id,
                CandidateId = application.CandidateId,
                Source = application.Candidate?.Source ?? CandidateSource.Applicant,
                Name = application.Name,
                Email = application.Email,
                City = application.City,
                ResumePath = application.ResumePath,
                AppliedDate = application.AppliedDate,
                JobId = application.JobId,
                JobTitle = application.Job?.Title,
                CurrentJobStageId = application.CurrentJobStageId,
                CurrentStageName = application.CurrentStage?.Name ?? "Applied",
                Documents = application.Documents
                    .Select(d => new DocumentDto
                    {
                        Id = d.Id,
                        FileName = d.FileName,
                        FilePath = d.FilePath,
                        Type = d.Type,
                        FileSize = d.FileSize,
                        UploadDate = d.UploadDate
                    })
                    .ToList(),
                StageProgression = (application.Job?.Stages?.OrderBy(s => s.Order) ?? Enumerable.Empty<JobStage>())
                    .Select(s => new JobStageDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Order = s.Order
                    })
                    .ToList(),
                DaysInSystem = daysInSystem,
                TimeInSystemDisplay = timeInSystemDisplay,
                JobStages = application.Job?.Stages?.OrderBy(s => s.Order).ToList() ?? new()
            };

            // 3. Stage progress (computed from already-loaded data)
            var allStages = viewModel.StageProgression.ToList();
            if (allStages.Any())
            {
                viewModel.CurrentStageIndex = allStages.FindIndex(s => s.Id == application.CurrentJobStageId);
                if (viewModel.CurrentStageIndex < 0) viewModel.CurrentStageIndex = 0;
                viewModel.ProgressPercentage = ((viewModel.CurrentStageIndex + 1) * 100.0) / allStages.Count;
            }

            // 4. Scorecards — raw list returned here is passed to analytics to skip a second fetch
            var scorecardData = await _scorecardSummaryService.GetScorecardSummariesAsync(application.CandidateId);
            viewModel.Scorecards = scorecardData.Summaries;

            // 5. Analytics — reuses the already-loaded scorecards
            viewModel.Analytics = await _scorecardAnalyticsService.GetCandidateAnalyticsFromScorecardsAsync(scorecardData.RawScorecards);

            // 6. Interviews (summaries + scheduling form data)
            viewModel.Interviews = await _interviewService.GetInterviewSummariesAsync(application.CandidateId, application.Stage.ToString());
            var schedulingData = await _interviewService.GetInterviewSchedulingDataAsync(application.CandidateId);
            viewModel.Applications = schedulingData.CandidateApplications;
            viewModel.AdminUsers   = schedulingData.AdminUsers;

            // 7. Related applications + cross-application recommendations
            var relatedData = await _relatedApplicationsService.GetRelatedApplicationsAsync(application.Id, application.Email, user);
            viewModel.RelatedApplications              = relatedData.RelatedApplications;
            viewModel.CrossApplicationRecommendations  = relatedData.CrossApplicationRecommendations;

            // 8. Recommendations for this application
            var recData = await _recommendationSummaryService.GetRecommendationSummaryAsync(application.Id);
            viewModel.Recommendations              = recData.Recommendations;
            viewModel.RequiresStage1ApprovalWarning = recData.RequiresStage1ApprovalWarning;

            // 9. Application pipeline stage + stage-approval warning flag
            viewModel.ApplicationStage = application.Stage;
            bool stageWarningRedirect = stageApprovalWarnApplicationId.HasValue && stageApprovalWarnApplicationId.Value == application.Id;
            viewModel.RequiresStageApprovalWarning = stageWarningRedirect;
            viewModel.PendingApplicationStage = stageWarningRedirect ? ApplicationStage.Interview : (ApplicationStage?)null;
            viewModel.Pipeline = _hiringPipelineService.GetPipeline(application, application.Job?.Stages?.OrderBy(s => s.Order).ToList() ?? new());

            return viewModel;
        }
    }
}
