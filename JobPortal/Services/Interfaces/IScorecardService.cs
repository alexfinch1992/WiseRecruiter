using System.ComponentModel.DataAnnotations;
using JobPortal.Models;

namespace JobPortal.Services.Interfaces
{
    public interface IScorecardService
    {
        Task<List<ScorecardResponse>> CreateDefaultResponsesFromTemplate();
        Task<List<ScorecardResponse>> CreateDefaultResponsesForApplication(int applicationId);

        Task<Scorecard> CreateScorecardForApplicationAsync(int applicationId, string submittedBy);

        Task<Scorecard> CreateScorecardAsync(int candidateId, string submittedBy);

        Task<List<ScorecardResponse>> AddResponsesAsync(int scorecardId, IEnumerable<ScorecardResponse> responses);

        Task<Scorecard?> GetScorecardAsync(int scorecardId);

        Task<Scorecard?> GetScorecardWithResponsesAsync(int scorecardId);

        Task<ScorecardDetailDto?> GetScorecardById(int scorecardId);

        Task UpdateScorecard(int scorecardId, List<ScorecardDetailDto.ScorecardResponseDto> responses);

        Task<List<Scorecard>> GetScorecardsByCandidateAsync(int candidateId);

        Task<decimal> CalculateAverageScoreAsync(int scorecardId);
    }

    public class ScorecardDetailDto
    {
        public int Id { get; set; }
        public int CandidateId { get; set; }
        public string? OverallRecommendation { get; set; }
        public List<ScorecardResponseDto> Responses { get; set; } = new();
        public decimal AverageScore { get; set; }

        public class ScorecardResponseDto
        {
            [Required]
            public string FacetName { get; set; } = string.Empty;

            [Range(1.0, 5.0)]
            public decimal Score { get; set; }

            public string? Notes { get; set; }
        }
    }
}
