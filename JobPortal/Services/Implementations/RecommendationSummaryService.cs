using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class RecommendationSummaryService : IRecommendationSummaryService
    {
        private readonly AppDbContext _context;

        public RecommendationSummaryService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<RecommendationSummaryResult> GetRecommendationSummaryAsync(int applicationId)
        {
            var recommendations = await _context.CandidateRecommendations
                .Where(r => r.ApplicationId == applicationId)
                .OrderBy(r => r.Stage)
                .ToListAsync();

            var stage1Rec = recommendations.FirstOrDefault(r => r.Stage == RecommendationStage.Stage1);
            var requiresWarning = stage1Rec == null || stage1Rec.Status != RecommendationStatus.Approved;

            return new RecommendationSummaryResult(recommendations, requiresWarning);
        }
    }
}
