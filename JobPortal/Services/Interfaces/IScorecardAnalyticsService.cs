using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IScorecardAnalyticsService
    {
        /// <summary>
        /// Computes scorecard analytics for the candidate linked to the given application.
        /// Returns ScorecardCount only when fewer than two scorecards exist.
        /// </summary>
        Task<CandidateAnalyticsDto> GetCandidateAnalyticsAsync(int applicationId);

        /// <summary>
        /// Computes scorecard analytics from an already-loaded list of scorecards (with Responses
        /// eagerly loaded). Avoids the candidateId lookup and the second scorecards DB fetch that
        /// <see cref="GetCandidateAnalyticsAsync"/> performs internally.
        /// </summary>
        Task<CandidateAnalyticsDto> GetCandidateAnalyticsFromScorecardsAsync(List<Scorecard> scorecards);
    }

    public class CandidateAnalyticsDto
    {
        public int ScorecardCount { get; set; }

        /// <summary>
        /// Average of per-scorecard averages. Null when fewer than two scorecards exist.
        /// </summary>
        public decimal? OverallAverageScore { get; set; }

        /// <summary>
        /// Flat average of all raw scores grouped by category across all scorecards.
        /// Empty when fewer than two scorecards exist.
        /// </summary>
        public List<CategoryAverageDto> CategoryAverages { get; set; } = new();
    }

    public class CategoryAverageDto
    {
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal AverageScore { get; set; }
    }
}
