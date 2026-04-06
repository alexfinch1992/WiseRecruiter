using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;

[Authorize(Roles = "Admin,Recruiter,HiringManager")]
[Route("Admin")]
public class InterviewController : Controller
{
    private readonly IInterviewCommandService _interviewCommandService;
    private readonly ILogger<InterviewController> _logger;

    public InterviewController(IInterviewCommandService interviewCommandService, ILogger<InterviewController> logger)
    {
        _interviewCommandService = interviewCommandService ?? throw new ArgumentNullException(nameof(interviewCommandService));
        _logger = logger;
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
        {
            _logger.LogWarning("Interview creation failed. CandidateId: {CandidateId}, Error: {Error}", candidateId, result.Error);
            return result.Error switch
            {
                InterviewCreateError.InvalidApplication => BadRequest("Invalid application for this candidate."),
                InterviewCreateError.InvalidInterviewer => BadRequest("One or more selected interviewers are invalid."),
                _                                       => BadRequest()
            };
        }

        _logger.LogInformation("Interview created. CandidateId: {CandidateId}, ApplicationId: {AppId}, Stage: {Stage}", candidateId, result.ApplicationId, selectedStage);
        return RedirectToAction(nameof(AdminController.CandidateDetails), "Admin", new { id = result.ApplicationId });
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
