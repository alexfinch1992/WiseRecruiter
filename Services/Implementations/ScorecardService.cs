using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ScorecardService : IScorecardService
    {
        private readonly AppDbContext _context;

        public ScorecardService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Scorecard> CreateScorecardAsync(int candidateId, string submittedBy)
        {
            var candidateExists = await _context.Candidates.AnyAsync(c => c.Id == candidateId);
            if (!candidateExists)
                throw new InvalidOperationException("Cannot create scorecard for an invalid candidate.");

            var scorecard = new Scorecard
            {
                CandidateId = candidateId,
                SubmittedBy = submittedBy ?? string.Empty,
                SubmittedAt = DateTime.UtcNow
            };

            _context.Scorecards.Add(scorecard);
            await _context.SaveChangesAsync();
            return scorecard;
        }

        public async Task<List<ScorecardResponse>> AddResponsesAsync(int scorecardId, IEnumerable<ScorecardResponse> responses)
        {
            var scorecardExists = await _context.Scorecards.AnyAsync(s => s.Id == scorecardId);
            if (!scorecardExists)
                throw new InvalidOperationException("Cannot add responses to an invalid scorecard.");

            if (responses == null)
                throw new ArgumentNullException(nameof(responses));

            var entities = responses.Select(response =>
            {
                if (response.Score < 1.0m || response.Score > 5.0m)
                    throw new ArgumentOutOfRangeException(nameof(response.Score), "Score must be between 1.0 and 5.0.");

                return new ScorecardResponse
                {
                    ScorecardId = scorecardId,
                    FacetName = response.FacetName,
                    Score = response.Score,
                    Notes = response.Notes
                };
            }).ToList();

            _context.ScorecardResponses.AddRange(entities);
            await _context.SaveChangesAsync();
            return entities;
        }

        public async Task<Scorecard?> GetScorecardAsync(int scorecardId)
        {
            return await _context.Scorecards.FirstOrDefaultAsync(s => s.Id == scorecardId);
        }

        public async Task<Scorecard?> GetScorecardWithResponsesAsync(int scorecardId)
        {
            return await _context.Scorecards
                .Include(s => s.Responses)
                .FirstOrDefaultAsync(s => s.Id == scorecardId);
        }

        public async Task<List<Scorecard>> GetScorecardsByCandidateAsync(int candidateId)
        {
            return await _context.Scorecards
                .Where(s => s.CandidateId == candidateId)
                .OrderByDescending(s => s.SubmittedAt)
                .ToListAsync();
        }

        public async Task<decimal> CalculateAverageScoreAsync(int scorecardId)
        {
            var scorecardExists = await _context.Scorecards.AnyAsync(s => s.Id == scorecardId);
            if (!scorecardExists)
                throw new InvalidOperationException("Scorecard not found.");

            var scores = await _context.ScorecardResponses
                .Where(r => r.ScorecardId == scorecardId)
                .Select(r => r.Score)
                .ToListAsync();

            if (scores.Count == 0)
                return 0m;

            return scores.Average();
        }
    }
}
