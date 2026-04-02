using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Models;
using JobPortal.Services.Interfaces;

namespace JobPortal.Controllers
{
    [Authorize]
    [Route("Admin")]
    public class CandidateSearchController : Controller
    {
        private readonly ICandidateQueryService _candidateQueryService;
        private readonly IJobService _jobService;

        public CandidateSearchController(
            ICandidateQueryService candidateQueryService,
            IJobService jobService)
        {
            _candidateQueryService = candidateQueryService ?? throw new ArgumentNullException(nameof(candidateQueryService));
            _jobService = jobService ?? throw new ArgumentNullException(nameof(jobService));
        }

        [HttpGet("Applications")]
        public async Task<IActionResult> Applications()
        {
            var jobs = await _candidateQueryService.GetApplicationsForJobsAsync();
            return View("~/Views/Admin/Applications.cshtml", jobs);
        }

        [HttpGet("JobDetailSearch")]
        public async Task<IActionResult> JobDetailSearch(int? id, string? searchQuery, string? sort = "stage")
        {
            if (id == null)
                return NotFound();

            var job = await _candidateQueryService.GetJobDetailSearchAsync(id.Value, searchQuery, sort);
            if (job == null)
                return NotFound();

            ViewData["SearchQuery"] = searchQuery;
            ViewBag.HasApplicantsToReview = job.Applications?.Any(a => a.Stage == ApplicationStage.Applied) ?? false;
            ViewBag.StageSummary = _jobService.GetStageSummary(job);
            return View("~/Views/Admin/JobDetail.cshtml", job);
        }

        [HttpGet("Candidates")]
        public async Task<IActionResult> Candidates(string? search)
        {
            var results = await _candidateQueryService.GetCandidatesAsync(search);
            ViewData["Search"] = search;
            return View("~/Views/Admin/Candidates.cshtml", results);
        }

        [HttpGet("SearchCandidates")]
        public async Task<IActionResult> SearchCandidates(string? searchQuery, int? pageNumber = null)
        {
            var candidates = await _candidateQueryService.SearchCandidatesAsync(searchQuery);
            int page = Math.Max(1, pageNumber ?? 1);
            int pageSize = Math.Min(25, 100);
            int totalCount = candidates.Count;
            var paged = candidates
                .OrderByDescending(c => c.AppliedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return View("~/Views/Admin/SearchCandidates.cshtml", paged);
        }

        [HttpGet("JobDetailSearchApi")]
        public async Task<IActionResult> JobDetailSearchApi(int? id, string? searchQuery)
        {
            if (id == null)
                return BadRequest("Job ID is required");

            var (jobFound, results) = await _candidateQueryService.GetJobDetailSearchApiAsync(id.Value, searchQuery);
            if (!jobFound)
                return NotFound();

            return Json(results);
        }

        [HttpGet("SearchCandidatesApi")]
        public async Task<IActionResult> SearchCandidatesApi(string? searchQuery)
        {
            var results = await _candidateQueryService.GetSearchCandidatesApiAsync(searchQuery);
            return Json(results);
        }

        [HttpGet("GetCandidatesJson")]
        public async Task<IActionResult> GetCandidatesJson(string? search)
        {
            var results = await _candidateQueryService.GetCandidatesJsonAsync(search);
            return Json(results);
        }
    }
}
