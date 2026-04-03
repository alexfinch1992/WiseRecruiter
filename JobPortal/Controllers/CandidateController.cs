using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Services.Interfaces;

/// <summary>
/// Owns candidate-facing page actions.
/// CandidateDetails is duplicated from AdminController (kept there for test compatibility).
/// WriteRecommendation and WriteStage2Recommendation are fully moved here.
/// </summary>
[Authorize]
public class CandidateController : Controller
{
    private readonly ICandidateDetailsService  _candidateDetailsService;
    private readonly IWriteRecommendationService _writeRecommendationService;

    public CandidateController(
        ICandidateDetailsService  candidateDetailsService,
        IWriteRecommendationService writeRecommendationService)
    {
        _candidateDetailsService    = candidateDetailsService    ?? throw new ArgumentNullException(nameof(candidateDetailsService));
        _writeRecommendationService = writeRecommendationService ?? throw new ArgumentNullException(nameof(writeRecommendationService));
    }

    // Route: /Candidate/CandidateDetails  (AdminController still owns /Admin/CandidateDetails)
    public async Task<IActionResult> CandidateDetails(int? id, string? ids = null, int? idx = null)
    {
        if (id == null)
            return NotFound();

        int? stageApprovalWarnAppId = TempData["StageApprovalWarning"] is int warnId ? warnId : (int?)null;
        try
        {
            var viewModel = await _candidateDetailsService.GetCandidateDetailsAsync(id.Value, User, stageApprovalWarnAppId);
            if (viewModel == null) return NotFound();
            SetCandidateNavigation(ids, idx);
            return View("~/Views/Admin/CandidateDetails.cshtml", viewModel);
        }
        catch (CandidateAccessForbiddenException)
        {
            return Forbid();
        }
    }

    private void SetCandidateNavigation(string? ids, int? idx)
    {
        if (string.IsNullOrEmpty(ids) || !idx.HasValue) return;

        var idList = new List<int>();
        foreach (var s in ids.Split(','))
            if (int.TryParse(s.Trim(), out var v)) idList.Add(v);

        var i = idx.Value;
        if (i < 0 || i >= idList.Count) return;

        ViewBag.NavIds = ids;
        ViewBag.NavIdx = i;
        ViewBag.NavTotal = idList.Count;
        ViewBag.NavPrevId = i > 0 ? idList[i - 1] : (int?)null;
        ViewBag.NavPrevIdx = i > 0 ? i - 1 : (int?)null;
        ViewBag.NavNextId = i < idList.Count - 1 ? idList[i + 1] : (int?)null;
        ViewBag.NavNextIdx = i < idList.Count - 1 ? i + 1 : (int?)null;
    }

    // Preserve existing route /Admin/WriteRecommendation
    [HttpGet("Admin/WriteRecommendation")]
    public async Task<IActionResult> WriteRecommendation(int applicationId)
    {
        var vm = await _writeRecommendationService.GetStage1ViewModelAsync(applicationId);
        if (vm == null) return NotFound();
        return View("~/Views/Admin/WriteRecommendation.cshtml", vm);
    }

    // Preserve existing route /Admin/WriteStage2Recommendation
    [HttpGet("Admin/WriteStage2Recommendation")]
    public async Task<IActionResult> WriteStage2Recommendation(int applicationId)
    {
        var vm = await _writeRecommendationService.GetStage2ViewModelAsync(applicationId);
        if (vm == null) return NotFound();
        return View("~/Views/Admin/WriteStage2Recommendation.cshtml", vm);
    }
}
