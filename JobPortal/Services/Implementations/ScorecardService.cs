using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ScorecardService : IScorecardService
    {
        private const string NoTemplateFacetsMessage = "Scorecard could not be created because the template has no facets.";

        private readonly AppDbContext _context;
        private readonly IScorecardTemplateService _templateService;

        public ScorecardService(AppDbContext context)
            : this(context, new ScorecardTemplateService(context))
        {
        }

        public ScorecardService(AppDbContext context, IScorecardTemplateService templateService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _templateService = templateService ?? throw new ArgumentNullException(nameof(templateService));
        }

        public async Task<List<ScorecardResponse>> CreateDefaultResponsesFromTemplate()
        {
            var defaultTemplate = await _templateService.GetDefaultTemplate();
            var templateFacets = await _templateService.GetFacetsForTemplate(defaultTemplate.Id);

            return MapTemplateFacetsToDefaultResponses(templateFacets);
        }

        public async Task<List<ScorecardResponse>> CreateDefaultResponsesForApplication(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                throw new InvalidOperationException("Application not found.");

            var templateId = application.Job?.ScorecardTemplateId;
            ScorecardTemplate template;

            if (templateId.HasValue)
            {
                var assignedTemplate = await _templateService.GetTemplateById(templateId.Value);
                if (assignedTemplate == null)
                    throw new InvalidOperationException("Assigned scorecard template was not found.");

                template = assignedTemplate;
            }
            else
            {
                template = await _templateService.GetDefaultTemplate();
            }

            var templateFacets = await _templateService.GetFacetsForTemplate(template.Id);

            return MapTemplateFacetsToDefaultResponses(templateFacets);
        }

        public async Task<Scorecard> CreateScorecardForApplicationAsync(int applicationId, string submittedBy)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                throw new InvalidOperationException("Application not found.");

            if (application.CandidateId <= 0)
                throw new InvalidOperationException("Cannot create scorecard for an application without a valid candidate.");

            var defaultResponses = await CreateDefaultResponsesForApplication(applicationId);
            if (defaultResponses.Count == 0)
                throw new InvalidOperationException(NoTemplateFacetsMessage);

            var scorecard = new Scorecard
            {
                CandidateId = application.CandidateId,
                SubmittedBy = submittedBy ?? string.Empty,
                SubmittedAt = DateTime.UtcNow,
                Responses = defaultResponses
            };

            _context.Scorecards.Add(scorecard);
            await _context.SaveChangesAsync();
            return scorecard;
        }

        private static List<ScorecardResponse> MapTemplateFacetsToDefaultResponses(List<ScorecardTemplateFacet> templateFacets)
        {
            return templateFacets
                .Where(templateFacet => templateFacet.Facet != null)
                .Select(templateFacet => new ScorecardResponse
                {
                    FacetId = templateFacet.FacetId,
                    FacetName = templateFacet.Facet!.Name,
                    Score = 3.0m,
                    Notes = string.Empty
                })
                .ToList();
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

            if (entities.Count == 0)
                throw new InvalidOperationException("Cannot add empty scorecard responses.");

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

        public async Task<ScorecardDetailDto?> GetScorecardById(int scorecardId)
        {
            var scorecard = await _context.Scorecards
                .Include(s => s.Responses)
                .FirstOrDefaultAsync(s => s.Id == scorecardId);

            if (scorecard == null)
                return null;

            var orderedResponses = scorecard.Responses
                .OrderBy(response => response.Id)
                .ToList();

            return new ScorecardDetailDto
            {
                Id = scorecard.Id,
                CandidateId = scorecard.CandidateId,
                Responses = orderedResponses
                    .Select(response => new ScorecardDetailDto.ScorecardResponseDto
                    {
                        FacetName = response.FacetName,
                        Score = response.Score,
                        Notes = response.Notes
                    })
                    .ToList(),
                AverageScore = orderedResponses.Count == 0 ? 0m : orderedResponses.Average(response => response.Score)
            };
        }

        public async Task UpdateScorecard(int scorecardId, List<ScorecardDetailDto.ScorecardResponseDto> responses)
        {
            var scorecardExists = await _context.Scorecards.AnyAsync(s => s.Id == scorecardId);
            if (!scorecardExists)
                throw new InvalidOperationException("Cannot update an invalid scorecard.");

            if (responses == null)
                throw new ArgumentNullException(nameof(responses));

            var replacementResponses = responses.Select(response =>
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

            var existingResponses = await _context.ScorecardResponses
                .Where(response => response.ScorecardId == scorecardId)
                .ToListAsync();

            _context.ScorecardResponses.RemoveRange(existingResponses);

            if (replacementResponses.Count > 0)
                _context.ScorecardResponses.AddRange(replacementResponses);

            await _context.SaveChangesAsync();
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
