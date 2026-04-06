using System.Security.Claims;
using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Alerts;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize(Roles = "Admin,Recruiter,HiringManager")]
[Route("Admin/Recommendations")]
public class RecommendationAdminController : Controller
{
    private readonly IRecommendationService _recommendationService;
    private readonly AppDbContext _context;
    private readonly IAuditService _auditService;
    private readonly AlertService? _alertService;

    public RecommendationAdminController(
        IRecommendationService recommendationService,
        AppDbContext context,
        IAuditService auditService,
        AlertService? alertService = null)
    {
        _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _alertService = alertService;
    }

    [HttpGet("Pending")]
    public async Task<IActionResult> Pending()
    {
        var pending = await _recommendationService.GetPendingRecommendationsAsync();
        return View(pending);
    }

    [HttpGet("Review/{applicationId}")]
    public async Task<IActionResult> Review(int applicationId, JobPortal.Models.RecommendationStage? stage)
    {
        if (stage == null || !Enum.IsDefined(typeof(JobPortal.Models.RecommendationStage), stage))
            return BadRequest("Valid stage is required");

        if (stage == JobPortal.Models.RecommendationStage.Stage2)
        {
            var vm2 = await _recommendationService.GetStage2ReviewAsync(applicationId);
            if (vm2 == null)
                return NotFound();
            return View("Stage2Review", vm2);
        }

        var vm = await _recommendationService.GetStage1ReviewAsync(applicationId);
        if (vm == null)
            return NotFound();
        return View("Stage1Review", vm);
    }

    [HttpPost("Approve/{applicationId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int applicationId, string? approvalFeedback = null)
    {
        var userIdStr = User?.FindFirst("AdminId")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Forbid();

        var approved = await _recommendationService.ApproveStage1RecommendationAsync(applicationId, userId, approvalFeedback);

        return approved switch
        {
            ApprovalResult.NotFound        => NotFound(),
            ApprovalResult.AlreadyApproved => BadRequest(),
            ApprovalResult.InvalidState    => BadRequest(),
            ApprovalResult.Forbidden       => Forbid(),
            _                              => RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = applicationId })
        };
    }

    [HttpPost("ApproveStage2/{applicationId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveStage2(int applicationId, string? approvalFeedback = null)
    {
        var userIdStr = User?.FindFirst("AdminId")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Forbid();

        var result = await _recommendationService.ApproveStage2RecommendationAsync(applicationId, userId, approvalFeedback);

        return result switch
        {
            ApprovalResult.NotFound        => NotFound(),
            ApprovalResult.AlreadyApproved => BadRequest(),
            ApprovalResult.InvalidState    => BadRequest(),
            ApprovalResult.Forbidden       => Forbid(),
            _                              => RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = applicationId })
        };
    }

    // ===== SetRecommendationOutcome =====

    [HttpPost("SetOutcome")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRecommendationOutcome(int recommendationId, RecommendationOutcome outcome, string? approvalFeedback = null)
    {
        var rec = await _context.CandidateRecommendations
            .FirstOrDefaultAsync(r => r.Id == recommendationId);

        if (rec == null)
            return NotFound();

        rec.Outcome = outcome;
        rec.ReviewedUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(approvalFeedback))
            rec.ApprovalFeedback = approvalFeedback;

        // Mirror outcome back into Status for backward-compatible badge display
        if (outcome == RecommendationOutcome.Proceed)
            rec.Status = RecommendationStatus.Approved;
        else if (outcome == RecommendationOutcome.NotSuitable)
            rec.Status = RecommendationStatus.Rejected;
        else if (outcome == RecommendationOutcome.MoreInfo)
            rec.Status = RecommendationStatus.NeedsRevision;

        await _context.SaveChangesAsync();

        var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var outcomeLabel = outcome switch
        {
            RecommendationOutcome.Proceed     => "marked candidate as Proceed",
            RecommendationOutcome.MoreInfo    => "requested More Info on candidate",
            RecommendationOutcome.NotSuitable => "marked candidate as Not Suitable",
            _                                 => $"set outcome to {outcome}"
        };
        await _auditService.LogAsync(
            "CandidateRecommendation",
            rec.Id,
            "OutcomeSet",
            $"Lead {outcomeLabel}; RecId={rec.Id}; AppId={rec.ApplicationId}",
            callerId);

        if (_alertService != null)
        {
            var application = await _context.Applications.FindAsync(rec.ApplicationId);
            if (application != null)
            {
                var candidateName = application.Name ?? "Unknown";
                var stageLabel = rec.Stage == RecommendationStage.Stage1 ? "Stage 1" : "Stage 2";
                var alertType = rec.Stage == RecommendationStage.Stage1
                    ? "RecommendationStage1Result"
                    : "RecommendationStage2Result";
                var resultLabel = outcome switch
                {
                    RecommendationOutcome.Proceed     => "Proceed",
                    RecommendationOutcome.MoreInfo    => "Needs more info",
                    RecommendationOutcome.NotSuitable => "Not suitable",
                    _                                 => outcome.ToString()
                };
                var message = $"{stageLabel} recommendation for {candidateName}: {resultLabel}";
                await _alertService.CreateJobAlertAsync(
                    application.JobId,
                    alertType,
                    message,
                    linkUrl: $"/Admin/CandidateDetails/{application.Id}",
                    relatedEntityId: rec.Id,
                    relatedEntityType: "Recommendation");
            }
        }

        return RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = rec.ApplicationId });
    }
}
