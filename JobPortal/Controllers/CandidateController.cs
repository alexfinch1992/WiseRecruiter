using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;

/// <summary>
/// Owns candidate-facing page actions.
/// CandidateDetails is duplicated from AdminController (kept there for test compatibility).
/// WriteRecommendation and WriteStage2Recommendation are fully moved here.
/// </summary>
[Authorize(Roles = "Admin,Recruiter,HiringManager")]
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
            SetCandidateNavigation(viewModel, ids, idx);
            return View("~/Views/Admin/CandidateDetails.cshtml", viewModel);
        }
        catch (CandidateAccessForbiddenException)
        {
            return Forbid();
        }
    }

    private static void SetCandidateNavigation(CandidateAdminViewModel vm, string? ids, int? idx)
    {
        if (string.IsNullOrEmpty(ids) || !idx.HasValue) return;

        var idList = new List<int>();
        foreach (var s in ids.Split(','))
            if (int.TryParse(s.Trim(), out var v)) idList.Add(v);

        var i = idx.Value;
        if (i < 0 || i >= idList.Count) return;

        vm.NavIds = ids;
        vm.NavIdx = i;
        vm.NavTotal = idList.Count;
        vm.NavPrevId = i > 0 ? idList[i - 1] : (int?)null;
        vm.NavPrevIdx = i > 0 ? i - 1 : (int?)null;
        vm.NavNextId = i < idList.Count - 1 ? idList[i + 1] : (int?)null;
        vm.NavNextIdx = i < idList.Count - 1 ? i + 1 : (int?)null;
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
