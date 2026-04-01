using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Services.Interfaces;

public class JobController : Controller
{
    private readonly IJobQueryService _jobQueryService;
    private readonly IJobCommandService _jobCommandService;

    public JobController(IJobQueryService jobQueryService, IJobCommandService jobCommandService)
    {
        _jobQueryService = jobQueryService;
        _jobCommandService = jobCommandService;
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

        ViewBag.ReturnUrl = returnUrl;

        return View(job);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl)
    {
        await _jobCommandService.DeleteJobAsync(id);

        if (!string.IsNullOrEmpty(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }
}
