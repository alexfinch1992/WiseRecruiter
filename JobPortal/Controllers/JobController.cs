using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;

public class JobController : Controller
{
    private readonly IJobQueryService _jobQueryService;
    private readonly IJobCommandService _jobCommandService;
    private readonly ILogger<JobController> _logger;

    public JobController(IJobQueryService jobQueryService, IJobCommandService jobCommandService, ILogger<JobController> logger)
    {
        _jobQueryService = jobQueryService;
        _jobCommandService = jobCommandService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _jobQueryService.GetJobsAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var job = await _jobQueryService.GetJobWithTemplateAsync(id.Value);
        return job == null ? NotFound() : View(job);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id, string? returnUrl)
    {
        if (id == null)
            return NotFound();

        var job = await _jobQueryService.GetJobForDeleteAsync(id.Value);
        if (job == null)
            return NotFound();

        var vm = new JobDeleteViewModel { Job = job, ReturnUrl = returnUrl };

        return View(vm);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl)
    {
        await _jobCommandService.DeleteJobAsync(id);
        _logger.LogInformation("Job deleted. JobId: {JobId}", id);

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }
}
