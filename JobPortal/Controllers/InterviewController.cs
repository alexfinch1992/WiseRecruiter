using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;

[Authorize]
[Route("Admin")]
public class InterviewController : Controller
{
    private readonly IInterviewCommandService _interviewCommandService;

    public InterviewController(IInterviewCommandService interviewCommandService)
    {
        _interviewCommandService = interviewCommandService ?? throw new ArgumentNullException(nameof(interviewCommandService));
    }

    // ===== Interviews =====

    [HttpPost("CreateInterview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInterview(int candidateId, int applicationId, string selectedStage, DateTime scheduledAt, List<int>? SelectedInterviewerIds = null, bool proceedWithoutApproval = false, string? bypassReason = null)
    {
        var userId = User?.FindFirst("AdminId")?.Value ?? string.Empty;
        var result = await _interviewCommandService.CreateAsync(
            candidateId, applicationId, selectedStage, scheduledAt,
            SelectedInterviewerIds, proceedWithoutApproval, bypassReason, userId);

        if (!result.Success)
            return result.Error switch
            {
                InterviewCreateError.InvalidApplication => BadRequest("Invalid application for this candidate."),
                InterviewCreateError.InvalidInterviewer => BadRequest("One or more selected interviewers are invalid."),
                _                                       => BadRequest()
            };

        return RedirectToAction("CandidateDetails", "Admin", new { id = result.ApplicationId });
    }

    [HttpPost("CancelInterview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInterview(int interviewId, int candidateId)
    {
        var result = await _interviewCommandService.CancelInterviewAsync(interviewId, candidateId);

        if (!result.Success && result.InvalidOwnership)
            return BadRequest("Invalid interview for this candidate.");

        if (!result.Success)
            return NotFound();

        return Ok();
    }
}
