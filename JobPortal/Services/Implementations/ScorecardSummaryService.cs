using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;

namespace JobPortal.Services.Implementations
{
    public class ScorecardSummaryService : IScorecardSummaryService
    {
        private readonly IScorecardService _scorecardService;

        public ScorecardSummaryService(IScorecardService scorecardService)
        {
            _scorecardService = scorecardService ?? throw new ArgumentNullException(nameof(scorecardService));
        }

        public async Task<ScorecardSummaryResult> GetScorecardSummariesAsync(int candidateId)
        {
            var scorecards = await _scorecardService.GetScorecardsByCandidateWithResponsesAsync(candidateId);

            var summaries = scorecards
                .Select(s => new ScorecardSummaryViewModel
                {
                    Id          = s.Id,
                    SubmittedBy = s.SubmittedBy,
                    SubmittedAt = s.SubmittedAt,
                    AverageScore = s.Responses.Count == 0
                        ? 0m
                        : s.Responses.Average(r => r.Score)
                })
                .ToList();

            return new ScorecardSummaryResult(summaries, scorecards);
        }
    }
}
