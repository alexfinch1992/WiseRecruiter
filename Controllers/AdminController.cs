using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


















































































































































































































































</div>    </div>        </div>            </div>                </div>                    </table>                        </tbody>                            }                                </tr>                                    <td colspan="3" class="text-center text-muted py-4">No applications yet</td>                                <tr>                            {                            else                            }                                }                                    </tr>                                        <td class="text-end text-muted">@cumulativeTotal</td>                                        <td class="text-end fw-bold">@dateEntry.Count</td>                                        <td>@dateEntry.Date.ToString("MMM dd, yyyy")</td>                                    <tr>                                    cumulativeTotal += dateEntry.Count;                                {                                @foreach (var dateEntry in applicationsOverTime)                                }                                    int cumulativeTotal = 0;                                @{                            {                            @if (applicationsOverTime.Count > 0)                        <tbody>                        </thead>                            </tr>                                <th class="text-end">Cumulative Total</th>                                <th class="text-end"><i class="fas fa-file-alt me-2"></i>Applications Received</th>                                <th><i class="fas fa-calendar me-2"></i>Date</th>                            <tr>                        <thead>                    <table class="table table-hover mb-0">                <div class="table-responsive">                </div>                    <h5 class="fw-bold mb-0 text-white"><i class="fas fa-calendar me-2"></i>Applications Over Time</h5>                <div class="card-header" style="background: linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%);">            <div class="card shadow-sm border-0">        <div class="col-md-12">    <div class="row">    <!-- Applications Over Time -->    </div>        </div>            </div>                </div>                    </table>                        </tbody>                            }                                </tr>                                    <td colspan="3" class="text-center text-muted py-4">No stage data available</td>                                <tr>                            {                            else                            }                                }                                    </tr>                                        <td class="text-end fw-bold">@stage.CandidateCount</td>                                        </td>                                            <span class="badge" style="background: #7B3FF2;">@stage.StageName</span>                                        <td>                                        <td><strong>@stage.JobTitle</strong></td>                                    <tr>                                {                                @foreach (var stage in stageStats)                            {                            @if (stageStats.Count > 0)                        <tbody>                        </thead>                            </tr>                                <th class="text-end"><i class="fas fa-users me-2"></i>Candidates</th>                                <th><i class="fas fa-tasks me-2"></i>Stage</th>                                <th><i class="fas fa-briefcase me-2"></i>Job Title</th>                            <tr>                        <thead>                    <table class="table table-hover mb-0">                <div class="table-responsive">                </div>                    <h5 class="fw-bold mb-0 text-white"><i class="fas fa-sitemap me-2"></i>Candidate Distribution Across Stages by Job</h5>                <div class="card-header" style="background: linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%);">            <div class="card shadow-sm border-0">        <div class="col-md-12 mb-5">    <div class="row">    <!-- Stage Breakdown by Job -->    </div>        </div>            </div>                </div>                    </table>                        </tbody>                            }                                </tr>                                    <td colspan="2" class="text-center text-muted py-4">No jobs created yet</td>                                <tr>                            {                            else                            }                                }                                    </tr>                                        </td>                                            <span class="badge bg-primary">@job.TotalApplications</span>                                        <td class="text-end">                                        <td><strong>@job.JobTitle</strong></td>                                    <tr>                                {                                @foreach (var job in jobStats)                            {                            @if (jobStats.Count > 0)                        <tbody>                        </thead>                            </tr>                                <th class="text-end"><i class="fas fa-file-alt me-2"></i>Applications</th>                                <th><i class="fas fa-briefcase me-2"></i>Job Title</th>                            <tr>                        <thead>                    <table class="table table-hover mb-0">                <div class="table-responsive">                </div>                    <h5 class="fw-bold mb-0 text-white"><i class="fas fa-briefcase me-2"></i>Applications by Job</h5>                <div class="card-header" style="background: linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%);">            <div class="card shadow-sm border-0">        <div class="col-md-6 mb-5">        <!-- Applications by Job -->        </div>            </div>                </div>                    </table>                        </tbody>                            }                                </tr>                                    <td colspan="3" class="text-center text-muted py-4">No data available</td>                                <tr>                            {                            else                            }                                }                                    </tr>                                        <td class="text-end text-muted">@percentage%</td>                                        <td class="text-end fw-bold">@stage.Count</td>                                        </td>                                            </span>                                                @stage.StageName                                            <span class="badge" style="background: #7B3FF2; font-size: 0.9rem;">                                        <td>                                    <tr>                                    var percentage = totalApplications > 0 ? Math.Round(((double)stage.Count / totalApplications) * 100, 1) : 0;                                {                                @foreach (var stage in candidatesByStage)                            {                            @if (candidatesByStage.Count > 0)                        <tbody>                        </thead>                            </tr>                                <th class="text-end">%</th>                                <th class="text-end"><i class="fas fa-users me-2"></i>Count</th>                                <th><i class="fas fa-tasks me-2"></i>Stage</th>                            <tr>                        <thead>                    <table class="table table-hover mb-0">                <div class="table-responsive">                </div>                    <h5 class="fw-bold mb-0 text-white"><i class="fas fa-filter me-2"></i>Candidates by Interview Stage</h5>                <div class="card-header" style="background: linear-gradient(135deg, #1E1765 0%, #7B3FF2 100%);">            <div class="card shadow-sm border-0">        <div class="col-md-6 mb-5">        <!-- Candidates by Stage -->    <div class="row">    <!-- Tables Row -->    </div>        </div>            </div>                </div>                    <p class="text-muted mb-0"><i class="fas fa-chart-line me-2"></i>Avg per Job</p>                    </h3>                        @avgApps                        }                            var avgApps = totalJobs > 0 ? (totalApplications / totalJobs) : 0;                        @{                    <h3 class="fw-bold display-5" style="color: #ffc107;">                <div class="card-body text-center">            <div class="card shadow-sm border-0 h-100" style="border-left: 4px solid #ffc107;">        <div class="col-md-6 col-lg-3 mb-4">        </div>            </div>                </div>                    <p class="text-muted mb-0"><i class="fas fa-check-circle me-2"></i>Candidates by Stage</p>                    </h3>                        @completedCount                        }                                .Count ?? 0;                                .FirstOrDefault()?                            var completedCount = candidatesByStage                        @{                    <h3 class="fw-bold display-5" style="color: #28a745;">                <div class="card-body text-center">            <div class="card shadow-sm border-0 h-100" style="border-left: 4px solid #28a745;">        <div class="col-md-6 col-lg-3 mb-4">        </div>            </div>                </div>                    <p class="text-muted mb-0"><i class="fas fa-briefcase me-2"></i>Active Jobs</p>                    <h3 class="fw-bold display-5" style="color: #1E1765;">@totalJobs</h3>                <div class="card-body text-center">            <div class="card shadow-sm border-0 h-100" style="border-left: 4px solid #1E1765;">        <div class="col-md-6 col-lg-3 mb-4">        </div>            </div>                </div>                    <p class="text-muted mb-0"><i class="fas fa-file-alt me-2"></i>Total Applications</p>                    <h3 class="fw-bold display-5" style="color: #7B3FF2;">@totalApplications</h3>                <div class="card-body text-center">            <div class="card shadow-sm border-0 h-100" style="border-left: 4px solid #7B3FF2;">        <div class="col-md-6 col-lg-3 mb-4">    <div class="row mb-5">    <!-- Key Metrics -->    </div>        </div>            <p class="text-muted">Overview of recruitment metrics and candidate flow</p>            <h1 class="fw-bold mb-2"><i class="fas fa-chart-bar me-3"></i>Analytics Dashboard</h1>        <div class="col-md-12">    <div class="row mb-5"><div class="container-fluid py-5">}    int totalJobs = Model.TotalJobs;    int totalApplications = Model.TotalApplications;    var candidatesByStage = (List<dynamic>)Model.CandidatesByStage;    var applicationsOverTime = (List<dynamic>)Model.ApplicationsOverTime;    var jobStats = (List<dynamic>)Model.JobStats;    var stageStats = (List<dynamic>)Model.StageStats;    ViewData["Title"] = "Analytics Dashboard";@{using Microsoft.EntityFrameworkCore;
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

    public async Task<IActionResult> Analytics()
    {
        // Total candidates by stage
        var stageStats = await _context.JobStages
            .Include(s => s.Job)
            .Select(s => new
            {
                StageName = s.Name,
                JobTitle = s.Job.Title,
                CandidateCount = s.Job.Applications.Count(a => a.CurrentJobStageId == s.Id)
            })
            .OrderBy(s => s.JobTitle)
            .ThenBy(s => s.StageName)
            .ToListAsync();

        // Applications by job
        var jobStats = await _context.Jobs
            .Include(j => j.Applications)
            .Select(j => new
            {
                JobTitle = j.Title,
                TotalApplications = j.Applications.Count,
                DateCreated = j.Id // placeholder for now
            })
            .OrderByDescending(j => j.TotalApplications)
            .ToListAsync();

        // Applications over time (grouped by date)
        var applicationsOverTime = await _context.Applications
            .GroupBy(a => a.AppliedDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync();

        // Candidates by stage (overall)
        var candidatesByStage = await _context.Applications
            .Include(a => a.CurrentStage)
            .GroupBy(a => a.CurrentJobStageId)
            .Select(g => new
            {
                StageName = g.FirstOrDefault().CurrentStage.Name ?? "Unassigned",
                Count = g.Count()
            })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var analyticsData = new
        {
            StageStats = stageStats,
            JobStats = jobStats,
            ApplicationsOverTime = applicationsOverTime,
            CandidatesByStage = candidatesByStage,
            TotalApplications = await _context.Applications.CountAsync(),
            TotalJobs = await _context.Jobs.CountAsync()
        };

        return View(analyticsData);
    }
}
