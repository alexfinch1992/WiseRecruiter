using JobPortal.Models;
using JobPortal.Models.ViewModels;

namespace JobPortal.Services.Interfaces
{
    /// <summary>
    /// Result returned by <see cref="IScorecardSummaryService.GetScorecardSummariesAsync"/>.
    /// </summary>
    /// <param name="Summaries">Projected view-model summaries ready for display.</param>
    /// <param name="RawScorecards">
    /// Raw loaded scorecards with Responses eagerly populated.
    /// Pass to <see cref="IScorecardAnalyticsService.GetCandidateAnalyticsFromScorecardsAsync"/>
    /// to avoid re-fetching scorecards for analytics.
    /// </param>
    public record ScorecardSummaryResult(
        List<ScorecardSummaryViewModel> Summaries,
        List<Scorecard> RawScorecards);

    public interface IScorecardSummaryService
    {
        /// <summary>
        /// Loads all non-archived scorecards with responses for the given candidate and
        /// projects them to <see cref="ScorecardSummaryViewModel"/> entries.
        /// The raw scorecards are also returned so the caller can pass them to the analytics
        /// service without issuing a second database query.
        /// </summary>
        Task<ScorecardSummaryResult> GetScorecardSummariesAsync(int candidateId);
    }
}
