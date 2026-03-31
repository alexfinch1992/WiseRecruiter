using JobPortal.Data;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;

namespace JobPortal.Services.Implementations
{
    public class RecommendationCommandService : IRecommendationActionService, IRecommendationDraftService
    {
        private readonly AppDbContext           _context;
        private readonly IRecommendationService _recommendationService;

        public RecommendationCommandService(
            AppDbContext            context,
            IRecommendationService  recommendationService)
        {
            _context               = context               ?? throw new ArgumentNullException(nameof(context));
            _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
        }

        // ── IRecommendationActionService ────────────────────────────────────

        public Task<TransitionResult> SubmitStage1Async(int applicationId, int userId)
            => ExecuteAsync(() => _recommendationService.SubmitStage1RecommendationAsync(applicationId, userId));

        public Task<TransitionResult> SubmitStage2Async(int applicationId, int userId)
            => ExecuteAsync(() => _recommendationService.SubmitStage2RecommendationAsync(applicationId, userId));

        // ── IRecommendationDraftService ─────────────────────────────────────

        public Task<TransitionResult> SaveStage1DraftAsync(
            int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation)
            => ExecuteAsync(() => _recommendationService.SaveStage1DraftAsync(
                applicationId, notes, strengths, concerns, hireRecommendation));

        public Task<TransitionResult> SaveStage2DraftAsync(
            int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation)
            => ExecuteAsync(() => _recommendationService.SaveStage2DraftAsync(
                applicationId, notes, strengths, concerns, hireRecommendation));

        // ── shared helper ───────────────────────────────────────────────────

        private async Task<TransitionResult> ExecuteAsync(Func<Task<TransitionResult>> action)
        {
            var result = await action();
            if (result != TransitionResult.Success) return result;
            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }
    }
}
