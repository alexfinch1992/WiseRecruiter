using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobPortal.Data;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Alerts;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;

[Authorize]
public class RecommendationController : Controller
{
    private readonly IRecommendationService _recommendationService;
    private readonly AppDbContext _context;
    private readonly AlertService? _alertService;
    private readonly ReviewerResolver? _reviewerResolver;

    public RecommendationController(
        IRecommendationService recommendationService,
        AppDbContext context,
        AlertService? alertService = null,
        ReviewerResolver? reviewerResolver = null)
    {
        _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _alertService = alertService;
        _reviewerResolver = reviewerResolver;
    }

    [HttpGet]
    public IActionResult Stage1(int applicationId)
    {
        // Legacy Razor page — redirect to the React-based editor.
        // WriteRecommendation is on CandidateController with route [HttpGet("Admin/WriteRecommendation")].
        return RedirectToAction("WriteRecommendation", "Candidate", new { applicationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stage1(int applicationId, Stage1RecommendationViewModel model)
    {
        var saved = await _recommendationService.SaveStage1DraftAsync(
            applicationId, model.Notes, model.Strengths, model.Concerns, model.HireRecommendation);

        return saved switch
        {
            TransitionResult.NotFound    => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                            => RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = applicationId })
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitStage1(int applicationId)
    {
        var userIdStr = User?.FindFirst("AdminId")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Forbid();

        var result = await _recommendationService.SubmitStage1RecommendationAsync(applicationId, userId);

        if (result == TransitionResult.Success)
            await NotifyReviewersAsync(applicationId);

        return result switch
        {
            TransitionResult.NotFound     => NotFound(new { success = false, error = "Not found" }),
            TransitionResult.InvalidState => BadRequest(new { success = false, error = "Invalid state" }),
            _                             => Json(new { success = true })
        };
    }

    [HttpGet]
    public async Task<IActionResult> Stage2(int applicationId)
    {
        var context = await _recommendationService.GetStage2ContextAsync(applicationId);
        if (context == null)
            return NotFound();

        var (rec, candidateName, jobTitle) = context.Value;

        var model = new Stage2RecommendationViewModel
        {
            Notes = rec?.Summary,
            Strengths = rec?.ExperienceFit,
            Concerns = rec?.Concerns,
            HireRecommendation = rec?.HireRecommendation
        };

        ViewData["ApplicationId"] = applicationId;
        ViewData["CandidateName"] = candidateName;
        ViewData["JobTitle"] = jobTitle;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stage2(int applicationId, Stage2RecommendationViewModel model)
    {
        var saved = await _recommendationService.SaveStage2DraftAsync(
            applicationId, model.Notes, model.Strengths, model.Concerns, model.HireRecommendation);

        return saved switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = applicationId })
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitStage2(int applicationId)
    {
        var userIdStr = User?.FindFirst("AdminId")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Forbid();

        var result = await _recommendationService.SubmitStage2RecommendationAsync(applicationId, userId);

        if (result == TransitionResult.Success)
            await NotifyReviewersAsync(applicationId);

        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = applicationId })
        };
    }

    private async Task NotifyReviewersAsync(int applicationId)
    {
        if (_alertService == null || _reviewerResolver == null)
            return;

        var application = await _context.Applications
            .Include(a => a.Job)
            .Include(a => a.Candidate)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application?.Job == null)
            return;

        var recommendation = await _context.CandidateRecommendations
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.Status == JobPortal.Models.RecommendationStatus.Submitted);

        var jobId = application.Job.Id;
        var candidateName = application.Candidate != null
            ? $"{application.Candidate.FirstName} {application.Candidate.LastName}"
            : application.Name ?? "Unknown";
        var recommendationId = recommendation?.Id;

        var reviewerIds = await _reviewerResolver.ResolveReviewerUserIdsAsync(jobId);
        var userIds = reviewerIds.ToList();

        // Always include Primary Recruiter
        if (application.Job.OwnerUserId != null)
            userIds.Add(application.Job.OwnerUserId);

        // Deduplicate
        userIds = userIds.Distinct().ToList();

        if (userIds.Count == 0)
            return;

        await _alertService.CreateAlertsAsync(
            userIds,
            type: "RecommendationSubmitted",
            message: $"New candidate ready for review: {candidateName}",
            linkUrl: $"/Admin/CandidateDetails/{application.Id}",
            relatedEntityId: recommendationId,
            relatedEntityType: "Recommendation"
        );
    }
}
