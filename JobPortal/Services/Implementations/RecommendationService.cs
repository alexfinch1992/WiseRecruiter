using JobPortal.Data;
using JobPortal.Domain.Recommendations;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace JobPortal.Services.Implementations
{
    public class RecommendationService : IRecommendationService
    {
        private readonly AppDbContext _context;
        private readonly IStageStateMachine<Stage1TransitionContext> _machine;
        private readonly IStageStateMachine<Stage2TransitionContext> _machine2;
        private readonly IStageAuthorizationService _authService;
        private readonly IStageOrderService _stageOrderService;

        public RecommendationService(
            AppDbContext context,
            IStageOrderService stageOrderService,
            IStageStateMachine<Stage1TransitionContext>? machine = null,
            IStageStateMachine<Stage2TransitionContext>? machine2 = null,
            IStageAuthorizationService? authService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _stageOrderService = stageOrderService ?? throw new ArgumentNullException(nameof(stageOrderService));
            _machine = machine ?? new Stage1StateMachine();
            _machine2 = machine2 ?? new Stage2StateMachine();
            _authService = authService ?? new StageAuthorizationService();
        }

        public async Task<(CandidateRecommendation? Rec, string? CandidateName, string? JobTitle)?> GetStage1ContextAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            return (rec, application.Name, application.Job?.Title);
        }

        public async Task<TransitionResult> SaveStage1DraftAsync(int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation)
        {
            var applicationExists = await _context.Applications.AnyAsync(a => a.Id == applicationId);
            if (!applicationExists)
                return TransitionResult.NotFound;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            if (rec == null)
            {
                rec = new CandidateRecommendation
                {
                    ApplicationId = applicationId,
                    Stage = RecommendationStage.Stage1,
                    Status = RecommendationStatus.Draft,
                    LastUpdatedUtc = DateTime.UtcNow
                };
                _context.CandidateRecommendations.Add(rec);
                _machine.ApplyTransition(rec, RecommendationStatus.Draft,
                    Stage1TransitionContext.ForDraftSave(notes, strengths, concerns, hireRecommendation));
            }
            else if (rec.Status == RecommendationStatus.Approved)
            {
                // Approval is permanent — bypass state machine and update content only
                rec.Summary = notes;
                rec.ExperienceFit = strengths;
                rec.Concerns = concerns;
                rec.HireRecommendation = hireRecommendation;
                rec.LastUpdatedUtc = DateTime.UtcNow;
            }
            else
            {
                if (_machine.ApplyTransition(rec, RecommendationStatus.Draft,
                    Stage1TransitionContext.ForDraftSave(notes, strengths, concerns, hireRecommendation)) != TransitionResult.Success)
                    return TransitionResult.InvalidState;
            }

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> SubmitStage1RecommendationAsync(int applicationId, int userId)
        {
            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            if (rec == null)
                return TransitionResult.NotFound;

            if (_machine.ApplyTransition(rec, RecommendationStatus.Submitted,
                Stage1TransitionContext.ForSubmit(userId)) != TransitionResult.Success)
                return TransitionResult.InvalidState;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<List<PendingRecommendationDto>> GetPendingRecommendationsAsync()
        {
            var pending = await _context.CandidateRecommendations
                .Include(r => r.Application)
                .Where(r => r.Status == RecommendationStatus.Submitted)
                .ToListAsync();

            var submitterIds = pending
                .Where(r => r.SubmittedByUserId.HasValue)
                .Select(r => r.SubmittedByUserId!.Value)
                .Distinct()
                .ToList();

            var submitters = submitterIds.Count > 0
                ? await _context.AdminUsers
                    .Where(u => submitterIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Username)
                : new Dictionary<int, string?>();

            return pending.Select(r => new PendingRecommendationDto
            {
                ApplicationId = r.ApplicationId,
                CandidateName = r.Application?.Name,
                CandidateEmail = r.Application?.Email,
                SubmittedByUsername = r.SubmittedByUserId.HasValue &&
                    submitters.TryGetValue(r.SubmittedByUserId.Value, out var username)
                    ? username : null,
                Summary = r.Summary,
                Status = r.Status,
                Stage = r.Stage
            }).ToList();
        }

        public async Task<Stage1ReviewViewModel?> GetStage1ReviewAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            return new Stage1ReviewViewModel
            {
                ApplicationId = applicationId,
                CandidateName = application.Name,
                CandidateEmail = application.Email,
                JobTitle = application.Job?.Title,
                Recommendation = rec
            };
        }

        public async Task<Stage2ReviewViewModel?> GetStage2ReviewAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);

            return new Stage2ReviewViewModel
            {
                ApplicationId = applicationId,
                CandidateName = application.Name,
                CandidateEmail = application.Email,
                JobTitle = application.Job?.Title,
                Recommendation = rec
            };
        }

        public async Task<ApprovalResult> ApproveStage1RecommendationAsync(int applicationId, int userId, string? approvalFeedback = null)
        {
            if (!_authService.CanApproveStage1(userId))
                return ApprovalResult.Forbidden;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            if (rec == null)
                return ApprovalResult.NotFound;

            if (rec.Status == RecommendationStatus.Approved)
                return ApprovalResult.AlreadyApproved;

            if (_machine.ApplyTransition(rec, RecommendationStatus.Approved,
                Stage1TransitionContext.ForApproval(userId)) != TransitionResult.Success)
                return ApprovalResult.InvalidState;

            rec.ApprovedByUserId = userId;
            rec.ApprovedUtc = rec.ReviewedUtc;  // ReviewedUtc is set by state machine; mirror it
            rec.ApprovalFeedback = approvalFeedback;

            // Auto-advance: if the candidate is still at Screen (Stage 1), move to the next stage
            var application = await _context.Applications.FindAsync(applicationId);
            if (application != null && application.Stage == ApplicationStage.Screen)
            {
                var nextStage = _stageOrderService.GetNextStage(application.Stage);
                if (nextStage.HasValue)
                    application.Stage = nextStage.Value;
            }

            // Auto-create Stage 2 recommendation if one does not already exist
            var stage2Exists = await _context.CandidateRecommendations
                .AnyAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);
            if (!stage2Exists)
            {
                _context.CandidateRecommendations.Add(new CandidateRecommendation
                {
                    ApplicationId = applicationId,
                    Stage = RecommendationStage.Stage2,
                    Status = RecommendationStatus.Draft,
                    LastUpdatedUtc = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();

            return ApprovalResult.Approved;
        }

        public async Task<ApprovalResult> ApproveStage2RecommendationAsync(int applicationId, int userId, string? approvalFeedback = null)
        {
            if (!_authService.CanApproveStage2(userId))
                return ApprovalResult.Forbidden;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);

            if (rec == null)
                return ApprovalResult.NotFound;

            if (rec.Status == RecommendationStatus.Approved)
                return ApprovalResult.AlreadyApproved;

            if (_machine2.ApplyTransition(rec, RecommendationStatus.Approved,
                Stage2TransitionContext.ForApproval(userId)) != TransitionResult.Success)
                return ApprovalResult.InvalidState;

            rec.ApprovedByUserId = userId;
            rec.ApprovedUtc = rec.ReviewedUtc;
            rec.ApprovalFeedback = approvalFeedback;

            await _context.SaveChangesAsync();

            return ApprovalResult.Approved;
        }

        public async Task<(CandidateRecommendation? Recommendation, bool IsApproved)> GetOrPrepareStage1RecommendationAsync(            int applicationId,
            bool proceedWithoutApproval,
            string? bypassReason,
            string userId)
        {
            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage1);

            bool isApproved = rec != null && rec.Status == RecommendationStatus.Approved;

            if (!isApproved && proceedWithoutApproval)
            {
                if (rec == null)
                {
                    rec = new CandidateRecommendation
                    {
                        ApplicationId = applicationId,
                        Stage = RecommendationStage.Stage1,
                        Status = RecommendationStatus.Draft,
                        LastUpdatedUtc = DateTime.UtcNow
                    };
                    _context.CandidateRecommendations.Add(rec);
                }

                if (!rec.BypassedApproval)
                {
                    rec.BypassedApproval = true;
                    rec.BypassedUtc = DateTime.UtcNow;
                    rec.LastUpdatedUtc = DateTime.UtcNow;
                    if (int.TryParse(userId, out var bypassUserId))
                        rec.BypassedByUserId = bypassUserId;
                }

                if (!string.IsNullOrEmpty(bypassReason))
                    rec.BypassReason = bypassReason;

                await _context.SaveChangesAsync();
            }

            return (rec, isApproved);
        }

        public async Task<(CandidateRecommendation? Rec, string? CandidateName, string? JobTitle)?> GetStage2ContextAsync(int applicationId)
        {
            var application = await _context.Applications
                .Include(a => a.Job)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return null;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);

            return (rec, application.Name, application.Job?.Title);
        }

        public async Task<TransitionResult> SaveStage2DraftAsync(int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation)
        {
            var applicationExists = await _context.Applications.AnyAsync(a => a.Id == applicationId);
            if (!applicationExists)
                return TransitionResult.NotFound;

            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);

            if (rec == null)
            {
                rec = new CandidateRecommendation
                {
                    ApplicationId = applicationId,
                    Stage = RecommendationStage.Stage2,
                    Status = RecommendationStatus.Draft,
                    LastUpdatedUtc = DateTime.UtcNow
                };
                _context.CandidateRecommendations.Add(rec);
                _machine2.ApplyTransition(rec, RecommendationStatus.Draft,
                    Stage2TransitionContext.ForDraftSave(notes, strengths, concerns, hireRecommendation));
            }
            else if (rec.Status == RecommendationStatus.Approved)
            {
                rec.Summary = notes;
                rec.ExperienceFit = strengths;
                rec.Concerns = concerns;
                rec.HireRecommendation = hireRecommendation;
                rec.LastUpdatedUtc = DateTime.UtcNow;
            }
            else
            {
                if (_machine2.ApplyTransition(rec, RecommendationStatus.Draft,
                    Stage2TransitionContext.ForDraftSave(notes, strengths, concerns, hireRecommendation)) != TransitionResult.Success)
                    return TransitionResult.InvalidState;
            }

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }

        public async Task<TransitionResult> SubmitStage2RecommendationAsync(int applicationId, int userId)
        {
            var rec = await _context.CandidateRecommendations
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Stage == RecommendationStage.Stage2);

            if (rec == null)
                return TransitionResult.NotFound;

            if (_machine2.ApplyTransition(rec, RecommendationStatus.Submitted,
                Stage2TransitionContext.ForSubmit(userId)) != TransitionResult.Success)
                return TransitionResult.InvalidState;

            await _context.SaveChangesAsync();
            return TransitionResult.Success;
        }
    }
}
