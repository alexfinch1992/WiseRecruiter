using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Alerts;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace JobPortal.Services.Implementations
{
    public class ScorecardCommandService : IScorecardCommandService
    {
        private readonly AppDbContext    _context;
        private readonly IScorecardService _scorecardService;
        private readonly AlertService? _alertService;

        public ScorecardCommandService(AppDbContext context, IScorecardService scorecardService, AlertService? alertService = null)
        {
            _context          = context          ?? throw new ArgumentNullException(nameof(context));
            _scorecardService = scorecardService ?? throw new ArgumentNullException(nameof(scorecardService));
            _alertService = alertService;
        }

        public async Task<CreateScorecardViewModel?> GetCreateScorecardModelAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var templateResponses = await _scorecardService.CreateDefaultResponsesForApplication(applicationId);

            var facetIds = templateResponses.Select(r => r.FacetId).ToList();
            var facetMetadata = await _context.Facets
                .Where(f => facetIds.Contains(f.Id))
                .Select(f => new { f.Id, f.Description, f.NotesPlaceholder, CategoryName = f.Category != null ? f.Category.Name : null })
                .ToDictionaryAsync(f => f.Id);

            var model = new CreateScorecardViewModel
            {
                ApplicationId = application.Id,
                CandidateId = application.CandidateId,
                CandidateName = application.Name ?? "Unknown",
                JobTitle = application.Job?.Title ?? "Unknown",
                Responses = templateResponses
                    .Select(response => new ScorecardResponseInputViewModel
                    {
                        FacetId = response.FacetId,
                        FacetName = response.FacetName,
                        Score = response.Score,
                        Notes = response.Notes,
                        Description = facetMetadata.TryGetValue(response.FacetId, out var fm) ? fm.Description : null,
                        NotesPlaceholder = facetMetadata.TryGetValue(response.FacetId, out var fm2) ? fm2.NotesPlaceholder : null,
                        CategoryName = facetMetadata.TryGetValue(response.FacetId, out var fm3) ? fm3.CategoryName : null
                    })
                    .ToList()
            };

            model.AvailableInterviews = await _context.Interviews
                .Include(i => i.JobStage)
                .Where(i => i.CandidateId == application.CandidateId && i.CompletedAt == null && !i.IsCancelled)
                .OrderBy(i => i.ScheduledAt)
                .Select(i => new InterviewSelectItem
                {
                    Id = i.Id,
                    Label = (i.JobStage != null ? i.JobStage.Name : "Interview") + " — " + i.ScheduledAt.ToString("yyyy-MM-dd HH:mm") + " UTC"
                })
                .ToListAsync();

            return model;
        }

        public async Task<bool> PopulateCreateScorecardContextAsync(CreateScorecardViewModel model)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == model.ApplicationId);

            if (application == null)
                return false;

            model.CandidateId = application.CandidateId;
            model.CandidateName = application.Name ?? "Unknown";
            model.JobTitle = application.Job?.Title ?? "Unknown";
            return true;
        }

        public async Task<(CreateScorecardResult Result, int? ScorecardId)> CreateScorecardAsync(
            CreateScorecardViewModel model,
            string submittedBy)
        {
            var scorecard = await _scorecardService.CreateScorecardAsync(model.CandidateId, submittedBy);

            if (model.InterviewId.HasValue)
            {
                var interview = await _context.Interviews.FindAsync(model.InterviewId.Value);
                if (interview == null)
                    return (CreateScorecardResult.InterviewNotFound, null);

                if (interview.CandidateId != model.CandidateId)
                    return (CreateScorecardResult.InvalidCandidateForInterview, null);

                scorecard.InterviewId = interview.Id;
                if (interview.CompletedAt == null)
                {
                    interview.CompletedAt = DateTime.UtcNow;
                }
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(model.OverallRecommendation))
            {
                scorecard.OverallRecommendation = model.OverallRecommendation;
                await _context.SaveChangesAsync();
            }

            var responses = model.Responses!.Select(r => new ScorecardResponse
            {
                FacetId   = r.FacetId,
                FacetName = r.FacetName,
                Score     = r.Score,
                Notes     = r.Notes
            });

            await _scorecardService.AddResponsesAsync(scorecard.Id, responses);

            if (_alertService != null)
            {
                var application = await _context.Applications
                    .FirstOrDefaultAsync(a => a.CandidateId == model.CandidateId);
                if (application != null)
                {
                    var candidateName = model.CandidateName ?? "Unknown";
                    var message = $"Interview completed for {candidateName}";
                    await _alertService.CreateJobAlertAsync(
                        application.JobId,
                        "InterviewCompleted",
                        message,
                        linkUrl: $"/Admin/CandidateDetails/{application.Id}",
                        relatedEntityId: scorecard.Id,
                        relatedEntityType: "Interview");
                }
            }

            return (CreateScorecardResult.Success, scorecard.Id);
        }

        public async Task<(bool ScorecardFound, int? ApplicationId)> ArchiveScorecardAsync(int id)
        {
            var scorecard = await _context.Scorecards.FindAsync(id);
            if (scorecard == null)
                return (false, null);

            scorecard.IsArchived = true;
            scorecard.ArchivedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.CandidateId == scorecard.CandidateId);

            return (true, application?.Id);
        }

        public async Task<bool> UpdateInterviewNotesAsync(int applicationId, string interviewNotes)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null)
                return false;

            application.InterviewNotes = interviewNotes;
            _context.Update(application);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
