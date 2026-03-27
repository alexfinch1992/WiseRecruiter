using JobPortal.Data;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ScorecardAnalyticsService : IScorecardAnalyticsService
    {
        private readonly AppDbContext _context;

        public ScorecardAnalyticsService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<CandidateAnalyticsDto> GetCandidateAnalyticsAsync(int applicationId)
        {
            var candidateId = await _context.Applications
                .Where(a => a.Id == applicationId)
                .Select(a => (int?)a.CandidateId)
                .FirstOrDefaultAsync();

            if (candidateId == null)
                return new CandidateAnalyticsDto();

            var scorecards = await _context.Scorecards
                .Where(s => s.CandidateId == candidateId)
                .Include(s => s.Responses)
                .ToListAsync();

            if (scorecards.Count < 2)
                return new CandidateAnalyticsDto { ScorecardCount = scorecards.Count };

            var allResponses = scorecards.SelectMany(s => s.Responses).ToList();

            var facetIds = allResponses.Select(r => r.FacetId).Distinct().ToList();
            var facetLookup = await _context.Facets
                .Where(f => facetIds.Contains(f.Id))
                .Include(f => f.Category)
                .ToDictionaryAsync(f => f.Id);

            var overallAverage = ComputeOverallAverage(scorecards);
            var categoryAverages = ComputeCategoryAverages(allResponses, facetLookup);

            return new CandidateAnalyticsDto
            {
                ScorecardCount = scorecards.Count,
                OverallAverageScore = overallAverage,
                CategoryAverages = categoryAverages
            };
        }

        /// <summary>
        /// For each scorecard: average its valid (>0) response scores.
        /// Then average those per-scorecard values across all scorecards.
        /// Responses with Score == 0 (unset default) are excluded.
        /// </summary>
        private static decimal? ComputeOverallAverage(List<Models.Scorecard> scorecards)
        {
            var perScorecardAverages = scorecards
                .Select(s =>
                {
                    var validScores = s.Responses
                        .Where(r => r.Score > 0)
                        .Select(r => r.Score)
                        .ToList();
                    return validScores.Count > 0 ? validScores.Average() : (decimal?)null;
                })
                .Where(avg => avg.HasValue)
                .Select(avg => avg!.Value)
                .ToList();

            return perScorecardAverages.Count > 0 ? perScorecardAverages.Average() : null;
        }

        /// <summary>
        /// Groups ALL valid responses across all scorecards by Facet.CategoryId,
        /// then computes a flat average — not an average of per-scorecard averages.
        /// </summary>
        private static List<CategoryAverageDto> ComputeCategoryAverages(
            List<Models.ScorecardResponse> responses,
            Dictionary<int, Models.Facet> facetLookup)
        {
            var responsesWithFacets = responses
                .Where(r => r.Score > 0)
                .Select(r => new
                {
                    r.Score,
                    Facet = facetLookup.TryGetValue(r.FacetId, out var f) ? f : null
                })
                .ToList();

            return responsesWithFacets
                .GroupBy(x => x.Facet?.CategoryId)
                .Select(g => new CategoryAverageDto
                {
                    CategoryId = g.Key,
                    CategoryName = g.Select(x => x.Facet?.Category?.Name).FirstOrDefault(n => n != null),
                    AverageScore = g.Average(x => x.Score)
                })
                .ToList();
        }
    }
}
