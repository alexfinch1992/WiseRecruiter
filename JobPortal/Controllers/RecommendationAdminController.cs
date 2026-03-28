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
        var pending = await _recommendationService.GetPendingStage1RecommendationsAsync();
        return View(pending);
    }

    [HttpGet("Review/{applicationId}")]
    public async Task<IActionResult> Review(int applicationId)
    {
        var review = await _recommendationService.GetStage1ReviewAsync(applicationId);
        if (review == null)
            return NotFound();

        return View(review);
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
            _                              => RedirectToAction("Pending")
        };
    }
}
