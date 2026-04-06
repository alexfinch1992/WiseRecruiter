using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;

/// <summary>
/// JSON API surface for inline recommendation editing on the CandidateDetails panel.
/// Preserves all existing /Admin/* routes from AdminController.
/// </summary>
[Authorize(Roles = "Admin,Recruiter,HiringManager")]
[Route("Admin")]
public class RecommendationApiController : Controller
{
    private readonly IRecommendationService      _recommendationService;
    private readonly IRecommendationActionService _recommendationActionService;
    private readonly IRecommendationDraftService  _recommendationDraftService;

    public RecommendationApiController(
        IRecommendationService      recommendationService,
        IRecommendationActionService recommendationActionService,
        IRecommendationDraftService  recommendationDraftService)
    {
        _recommendationService      = recommendationService      ?? throw new ArgumentNullException(nameof(recommendationService));
        _recommendationActionService = recommendationActionService ?? throw new ArgumentNullException(nameof(recommendationActionService));
        _recommendationDraftService  = recommendationDraftService  ?? throw new ArgumentNullException(nameof(recommendationDraftService));
    }

    [HttpGet("GetStage1RecJson")]
    public async Task<IActionResult> GetStage1RecJson(int applicationId)
    {
        var ctx = await _recommendationService.GetStage1ContextAsync(applicationId);
        if (ctx == null) return NotFound();
        var (rec, _, _) = ctx.Value;
        return Json(new
        {
            status             = rec?.Status.ToString() ?? "None",
            notes              = rec?.Summary,
            strengths          = rec?.ExperienceFit,
            concerns           = rec?.Concerns,
            hireRecommendation = rec?.HireRecommendation,
        });
    }

    [HttpPost("SaveRecDraftJson")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveRecDraftJson(
        int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation)
    {
        var result = await _recommendationDraftService.SaveStage1DraftAsync(
            applicationId, notes, strengths, concerns, hireRecommendation);
        if (result == TransitionResult.NotFound)
            return NotFound(new { error = "Application not found." });
        if (result != TransitionResult.Success)
            return BadRequest(new { error = result.ToString() });
        return Json(new { success = true, status = "Draft" });
    }

    [HttpPost("SubmitRecJson")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SubmitRecJson(int applicationId)
    {
        var userIdStr = User?.FindFirst("AdminId")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Forbid();
        var result = await _recommendationActionService.SubmitStage1Async(applicationId, userId);
        if (result == TransitionResult.NotFound)
            return NotFound(new { error = "Recommendation not found." });
        if (result != TransitionResult.Success)
            return BadRequest(new { error = result.ToString() });
        return Json(new { success = true, status = "Submitted" });
    }

    [HttpGet("GetStage2RecJson")]
    public async Task<IActionResult> GetStage2RecJson(int applicationId)
    {
        var ctx = await _recommendationService.GetStage2ContextAsync(applicationId);
        if (ctx == null) return NotFound();
        var (rec, _, _) = ctx.Value;
        return Json(new
        {
            status             = rec?.Status.ToString() ?? "None",
            notes              = rec?.Summary,
            strengths          = rec?.ExperienceFit,
            concerns           = rec?.Concerns,
            hireRecommendation = rec?.HireRecommendation,
        });
    }

    [HttpPost("SaveStage2RecDraftJson")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SaveStage2RecDraftJson(
        int applicationId, string? notes, string? strengths, string? concerns, bool? hireRecommendation)
    {
        var result = await _recommendationDraftService.SaveStage2DraftAsync(
            applicationId, notes, strengths, concerns, hireRecommendation);
        if (result == TransitionResult.NotFound)
            return NotFound(new { error = "Application not found." });
        if (result != TransitionResult.Success)
            return BadRequest(new { error = result.ToString() });
        return Json(new { success = true, status = "Draft" });
    }

    [HttpPost("SubmitStage2RecJson")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SubmitStage2RecJson(int applicationId)
    {
        var userIdStr = User?.FindFirst("AdminId")?.Value;
        if (!int.TryParse(userIdStr, out var userId)) return Forbid();
        var result = await _recommendationActionService.SubmitStage2Async(applicationId, userId);
        if (result == TransitionResult.NotFound)
            return NotFound(new { error = "Stage 2 recommendation not found." });
        if (result != TransitionResult.Success)
            return BadRequest(new { error = result.ToString() });
        return Json(new { success = true, status = "Submitted" });
    }
}
