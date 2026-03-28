using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ApplicationStageService : IApplicationStageService
    {
        private readonly AppDbContext _context;
        private readonly IRecommendationService _recommendationService;

        public ApplicationStageService(AppDbContext context, IRecommendationService recommendationService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
        }

        public async Task<StageUpdateResult> UpdateStageAsync(
            int applicationId,
            ApplicationStage newStage,
            bool proceedWithoutApproval,
            string userId)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                return new StageUpdateResult();

            // Soft-gate: warn if moving to Interview without Stage 1 approval
            if (newStage == ApplicationStage.Interview)
            {
                var (_, isApproved) = await _recommendationService.GetOrPrepareStage1RecommendationAsync(
                    applicationId, proceedWithoutApproval, bypassReason: null, userId);

                if (!isApproved && !proceedWithoutApproval)
                {
                    return new StageUpdateResult
                    {
                        RequiresApprovalWarning = true,
                        PendingStage = ApplicationStage.Interview
                    };
                }
            }

            application.Stage = newStage;

            // Caller (controller) is responsible for SaveChangesAsync
            return new StageUpdateResult();
        }
    }
}
