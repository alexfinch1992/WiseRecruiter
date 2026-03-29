using System.Security.Claims;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(AuthenticationSchemes = "AdminAuth")]
[Route("Admin/Recommendations")]
public class RecommendationAdminController : Controller
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationAdminController(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService ?? throw new ArgumentNullException(nameof(recommendationService));
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
            _                              => RedirectToAction("CandidateDetails", "Admin", new { id = applicationId })
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
            _                              => RedirectToAction("CandidateDetails", "Admin", new { id = applicationId })
        };
    }
}
