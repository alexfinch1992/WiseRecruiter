using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IScorecardService
    {
        Task<Scorecard> CreateScorecardAsync(int candidateId, string submittedBy);

        Task<List<ScorecardResponse>> AddResponsesAsync(int scorecardId, IEnumerable<ScorecardResponse> responses);

        Task<Scorecard?> GetScorecardAsync(int scorecardId);

        Task<Scorecard?> GetScorecardWithResponsesAsync(int scorecardId);

        Task<List<Scorecard>> GetScorecardsByCandidateAsync(int candidateId);

        Task<decimal> CalculateAverageScoreAsync(int scorecardId);
    }
}
