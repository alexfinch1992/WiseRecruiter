using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using JobPortal.Models;
using JobPortal.Data;

[Authorize(AuthenticationSchemes = "AdminAuth")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public AdminController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Jobs.ToListAsync());
    }

    public async Task<IActionResult> JobDetail(int? id, string? sort = "stage")
    {
        if (id == null)
            return NotFound();

        var job = await _context.Jobs
            .Include(j => j.Applications)
            .ThenInclude(a => a.CurrentStage)
            .Include(j => j.Stages)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
            return NotFound();

        // Sort applications
        if (job.Applications != null)
        {
            job.Applications = sort switch
            {
                "name" => job.Applications.OrderBy(a => a.Name).ToList(),
                "date" => job.Applications.OrderByDescending(a => a.AppliedDate).ToList(),
                _ => job.Applications.OrderBy(a => a.CurrentStage?.Order ?? 0).ThenBy(a => a.Name).ToList()
            };
        }

        return View(job);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Description")] Job job)
    {
        if (ModelState.IsValid)
        {
            _context.Add(job);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(job);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var job = await _context.Jobs.FindAsync(id);
        return job == null ? NotFound() : View(job);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description")] Job job)
    {
        if (id != job.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(job);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JobExists(job.Id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(job);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var job = await _context.Jobs.FirstOrDefaultAsync(m => m.Id == id);
        return job == null ? NotFound() : View(job);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var job = await _context.Jobs.FindAsync(id);
        if (job != null)
        {
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Applications()
    {
        var jobsWithApplications = await _context.Jobs
            .Include(j => j.Applications)
            .OrderByDescending(j => j.Id)
            .ToListAsync();

        return View(jobsWithApplications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStage(int applicationId, int jobId, int stageId)
    {
        var application = await _context.Applications.FindAsync(applicationId);
        if (application == null)
            return NotFound();

        // Verify the stage belongs to this job
        var stage = await _context.JobStages.FirstOrDefaultAsync(s => s.Id == stageId && s.JobId == jobId);
        if (stage == null)
            return BadRequest("Invalid stage for this job");

        application.CurrentJobStageId = stageId;
        _context.Update(application);
        await _context.SaveChangesAsync();

        return RedirectToAction("JobDetail", new { id = jobId });
    }

    public async Task<IActionResult> DownloadResume(int? id)
    {
        if (id == null)
            return NotFound();

        var application = await _context.Applications.FindAsync(id);
        if (application == null || string.IsNullOrEmpty(application.ResumePath))
            return NotFound();

        var filePath = Path.Combine(_webHostEnvironment.WebRootPath, application.ResumePath.TrimStart('/'));
        
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        return File(fileBytes, "application/octet-stream", Path.GetFileName(application.ResumePath));
    }

    private bool JobExists(int id) => _context.Jobs.Any(e => e.Id == id);

    [HttpPost]
    public async Task<IActionResult> AddStage(int jobId, string stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName))
            return BadRequest("Stage name cannot be empty");

        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null)
            return NotFound();

        var existingStage = await _context.JobStages
            .FirstOrDefaultAsync(s => s.JobId == jobId && s.Name == stageName);
        
        if (existingStage != null)
            return BadRequest("Stage already exists for this job");

        var maxOrder = await _context.JobStages
            .Where(s => s.JobId == jobId)
            .MaxAsync(s => (int?)s.Order) ?? -1;

        var newStage = new JobStage
        {
            JobId = jobId,
            Name = stageName,
            Order = maxOrder + 1
        };

        _context.JobStages.Add(newStage);
        await _context.SaveChangesAsync();

        return RedirectToAction("JobDetail", new { id = jobId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveStage(int stageId, int jobId)
    {
        var stage = await _context.JobStages.FindAsync(stageId);
        if (stage == null)
            return NotFound();

        _context.JobStages.Remove(stage);
        await _context.SaveChangesAsync();

        // Return JSON for AJAX, redirect for regular requests
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { success = true });

        return RedirectToAction("JobDetail", new { id = jobId });
    }

    [HttpPost]
    public async Task<IActionResult> MoveStage(int stageId, int jobId, string direction)
    {
        var stage = await _context.JobStages.FirstOrDefaultAsync(s => s.Id == stageId && s.JobId == jobId);
        if (stage == null)
            return NotFound();

        var allStages = await _context.JobStages
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.Order)
            .ToListAsync();

        var currentIndex = allStages.FindIndex(s => s.Id == stageId);
        if (currentIndex == -1)
            return NotFound();

        int swapIndex = -1;
        if (direction == "up" && currentIndex > 0)
            swapIndex = currentIndex - 1;
        else if (direction == "down" && currentIndex < allStages.Count - 1)
            swapIndex = currentIndex + 1;

        if (swapIndex == -1)
            return BadRequest("Cannot move stage in that direction");

        // Swap Order values
        var temp = allStages[currentIndex].Order;
        allStages[currentIndex].Order = allStages[swapIndex].Order;
        allStages[swapIndex].Order = temp;

        _context.Update(allStages[currentIndex]);
        _context.Update(allStages[swapIndex]);
        await _context.SaveChangesAsync();

        // Return JSON for AJAX, redirect for regular requests
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return Ok(new { success = true });
        
        return RedirectToAction("JobDetail", new { id = jobId });
    }

    [HttpGet]
    public async Task<IActionResult> JobDetailSearch(int? id, string? searchQuery, string? sort = "stage")
    {
        if (id == null)
            return NotFound();

        var job = await _context.Jobs
            .Include(j => j.Applications)
            .ThenInclude(a => a.CurrentStage)
            .Include(j => j.Stages)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
            return NotFound();

        // Filter applications by search query
        if (job.Applications != null && !string.IsNullOrWhiteSpace(searchQuery))
        {
            var query = searchQuery.ToLowerInvariant();
            job.Applications = job.Applications
                .Where(a => (a.Name?.ToLowerInvariant().Contains(query) ?? false) ||
                           (a.Email?.ToLowerInvariant().Contains(query) ?? false))
                .ToList();
        }

        // Sort applications
        if (job.Applications != null)
        {
            job.Applications = sort switch
            {
                "name" => job.Applications.OrderBy(a => a.Name).ToList(),
                "date" => job.Applications.OrderByDescending(a => a.AppliedDate).ToList(),
                _ => job.Applications.OrderBy(a => a.CurrentStage?.Order ?? 0).ThenBy(a => a.Name).ToList()
            };
        }

        ViewData["SearchQuery"] = searchQuery;
        return View("JobDetail", job);
    }

    [HttpGet]
    public async Task<IActionResult> SearchCandidates(string? searchQuery)
    {
        var candidates = await _context.Applications
            .Include(a => a.Job)
            .Include(a => a.CurrentStage)
            .ToListAsync();

        // Filter by search query
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var query = searchQuery.ToLowerInvariant();
            candidates = candidates
                .Where(a => (a.Name?.ToLowerInvariant().Contains(query) ?? false) ||
                           (a.Email?.ToLowerInvariant().Contains(query) ?? false))
                .ToList();
        }

        return View(candidates);
    }

    [HttpGet]
    public async Task<IActionResult> JobDetailSearchApi(int? id, string? searchQuery)
    {
        if (id == null)
            return BadRequest("Job ID is required");

        var job = await _context.Jobs.FindAsync(id);
        if (job == null)
            return NotFound();

        var candidates = await _context.Applications
            .Where(a => a.JobId == id)
            .Include(a => a.CurrentStage)
            .ToListAsync();

        // Filter by search query
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var query = searchQuery.ToLowerInvariant();
            candidates = candidates
                .Where(a => (a.Name?.ToLowerInvariant().Contains(query) ?? false) ||
                           (a.Email?.ToLowerInvariant().Contains(query) ?? false))
                .ToList();
        }

        // Return limited results (top 10)
        var results = candidates
            .Take(10)
            .Select(a => new
            {
                id = a.Id,
                name = a.Name,
                email = a.Email,
                city = a.City,
                stage = a.CurrentStage?.Name ?? "Unassigned"
            })
            .ToList();

        return Json(results);
    }

    [HttpGet]
    public async Task<IActionResult> SearchCandidatesApi(string? searchQuery)
    {
        var candidates = await _context.Applications
            .Include(a => a.Job)
            .Include(a => a.CurrentStage)
            .ToListAsync();

        // Filter by search query
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var query = searchQuery.ToLowerInvariant();
            candidates = candidates
                .Where(a => (a.Name?.ToLowerInvariant().Contains(query) ?? false) ||
                           (a.Email?.ToLowerInvariant().Contains(query) ?? false))
                .ToList();
        }

        // Return limited results (top 15)
        var results = candidates
            .Take(15)
            .Select(a => new
            {
                id = a.Id,
                name = a.Name,
                email = a.Email,
                job = a.Job?.Title ?? "Unknown",
                stage = a.CurrentStage?.Name ?? "Unassigned"
            })
            .ToList();

        return Json(results);
    }
}
