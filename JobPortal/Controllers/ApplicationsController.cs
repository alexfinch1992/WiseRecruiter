using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobPortal.Models;
using JobPortal.Data;
using JobPortal.Helpers;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;

[Authorize]
public class ApplicationsController : Controller
{

    private readonly AppDbContext _context;
    private readonly IFileUploadService _fileUploadService;
    private readonly IApplicationService _applicationService;
    private readonly IScorecardService _scorecardService;

    public ApplicationsController(AppDbContext context, IFileUploadService fileUploadService, IApplicationService applicationService, IScorecardService scorecardService)
    {
        _context = context;
        _fileUploadService = fileUploadService;
        _applicationService = applicationService;
        _scorecardService = scorecardService;
    }

    public async Task<IActionResult> Index()    
    {
        var userEmail = User.Identity?.Name;

        var applications = await _context.Applications
            .Where(a => a.Email == userEmail)
            .ToListAsync();

        return View(applications);
    }

    /// <summary>
    /// Public candidate-facing application details.
    /// Returns CandidatePublicViewModel with ONLY safe public information.
    /// </summary>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var application = await _context.Applications
            .Include(a => a.Job)
            .ThenInclude(j => j.Stages)
            .Include(a => a.CurrentStage)
            .Include(a => a.Documents)
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if (application == null)
            return NotFound();

        if (application.Email != User.Identity?.Name)
            return Forbid();

        // Map to public ViewModel - only safe information
        var viewModel = new CandidatePublicViewModel
        {
            Id = application.Id,
            Name = application.Name,
            Email = application.Email,
            City = application.City,
            ResumePath = application.ResumePath,
            AppliedDate = application.AppliedDate,
            JobId = application.JobId,
            JobTitle = application.Job?.Title,
            CurrentJobStageId = application.CurrentJobStageId,
            CurrentStageName = application.CurrentStage?.Name ?? "Unassigned",
            Documents = application.Documents
                .Select(d => new DocumentDto
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    FilePath = d.FilePath,
                    Type = d.Type,
                    FileSize = d.FileSize,
                    UploadDate = d.UploadDate
                })
                .ToList(),
            StageProgression = (application.Job?.Stages?.OrderBy(s => s.Order) ?? Enumerable.Empty<JobStage>())
                .Select(s => new JobStageDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Order = s.Order
                })
                .ToList()
        };

        // Calculate stage progress
        var allStages = viewModel.StageProgression.ToList();
        if (allStages.Any())
        {
            viewModel.CurrentStageIndex = allStages.FindIndex(s => s.Id == application.CurrentJobStageId);
            if (viewModel.CurrentStageIndex < 0) viewModel.CurrentStageIndex = 0;
            viewModel.ProgressPercentage = ((viewModel.CurrentStageIndex + 1) * 100.0) / allStages.Count;
        }

        return View(viewModel);
    }

    public IActionResult Create(int? jobId)
    {
        var application = new Application();
        if (jobId.HasValue)
            application.JobId = jobId.Value;
        return View(application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Email,City,JobId")] Application application, IFormFile resume)
    {
        var (isValid, errorMessage) = _fileUploadService.ValidateResume(resume);
        if (!isValid)
            ModelState.AddModelError("resume", errorMessage);

        if (ModelState.IsValid && resume != null)
        {
            var result = await _fileUploadService.UploadResumeAsync(resume);
            
            if (!result.Success)
            {
                ModelState.AddModelError("", result.ErrorMessage);
                return View(application);
            }

            application.ResumePath = result.StoredFileName;
            application.OriginalFileName = result.OriginalFileName;
            application.ResumeContentType = result.ContentType;
            application.ResumeUploadDate = result.UploadedAt;

            try
            {
                await _applicationService.CreateApplicationAsync(application);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(application);
            }

            await _scorecardService.CreateScorecardForApplicationAsync(application.Id, "System");

            return RedirectToAction(nameof(Index));
        }

        return View(application);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var application = await _context.Applications
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == id);
        return application == null ? NotFound() : View(application);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Email,City,ResumePath,JobId")] Application application)
    {
        if (id != application.Id)
            return NotFound();

        var existing = await _context.Applications.FindAsync(id);
        if (existing == null)
            return NotFound();
        if (existing.Email != User.Identity.Name)
            return Forbid();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(application);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ApplicationExists(application.Id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Details), new { id = application.Id });
        }

        var editApp = await _context.Applications.Include(a => a.Job).FirstOrDefaultAsync(a => a.Id == id);
        return View(editApp);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var application = await _context.Applications.FirstOrDefaultAsync(m => m.Id == id);
        if (application == null)
            return NotFound();
        if (application.Email != User.Identity.Name)
            return Forbid();
        return View(application);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var application = await _context.Applications.FindAsync(id);
        if (application == null)
            return NotFound();
        if (application.Email != User.Identity.Name)
            return Forbid();

        await _fileUploadService.DeleteResumeAsync(application.ResumePath);
        _context.Applications.Remove(application);

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(int applicationId, IFormFile document, int documentType = 0)
    {
        var application = await _context.Applications.FindAsync(applicationId);
        if (application == null)
            return NotFound();
        if (application.Email != User.Identity.Name)
            return Forbid();

        var (isValid, errorMessage) = _fileUploadService.ValidateDocument(document);
        if (!isValid)
            return Json(new { success = false, message = errorMessage });

        var result = await _fileUploadService.UploadDocumentAsync(document);
        if (!result.Success)
            return Json(new { success = false, message = result.ErrorMessage });

        var newDocument = new Document
        {
            ApplicationId = applicationId,
            FileName = result.OriginalFileName,
            FilePath = result.StoredFileName,
            Type = (DocumentType)documentType,
            FileSize = document.Length
        };

        _context.Documents.Add(newDocument);

        // If uploading a Resume document, also sync it to ResumePath for backward compatibility
        if (documentType == (int)DocumentType.Resume)
        {
            application.ResumePath = result.StoredFileName;
            application.OriginalFileName = result.OriginalFileName;
            application.ResumeContentType = result.ContentType;
            application.ResumeUploadDate = result.UploadedAt;
            _context.Update(application);
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Document uploaded successfully", documentId = newDocument.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDocument(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
            return NotFound();

        var application = await _context.Applications.FindAsync(document.ApplicationId);
        if (application == null || application.Email != User.Identity.Name)
            return Forbid();

        await _fileUploadService.DeleteDocumentAsync(document.FilePath);
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Document deleted successfully" });
    }

    public async Task<IActionResult> DownloadResume(int? id)
    {
        if (id == null)
            return NotFound();

        var application = await _context.Applications.FindAsync(id);
        if (application == null || string.IsNullOrEmpty(application.ResumePath))
            return NotFound();

        if (application.Email != User.Identity?.Name)
            return Forbid();

        var filePath = _fileUploadService.GetResumePhysicalPath(application.ResumePath);
        if (filePath == null)
            return NotFound();

        var downloadName = application.OriginalFileName ?? Path.GetFileName(filePath);
        return PhysicalFile(filePath, application.ResumeContentType ?? "application/octet-stream", downloadName);
    }

    public async Task<IActionResult> DownloadDocument(int? id)
    {
        if (id == null)
            return NotFound();

        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        var application = await _context.Applications.FindAsync(document.ApplicationId);
        if (application == null || application.Email != User.Identity?.Name)
            return Forbid();

        var filePath = _fileUploadService.GetDocumentPhysicalPath(document.FilePath);
        if (filePath == null)
            return NotFound();

        return PhysicalFile(filePath, "application/octet-stream", document.FileName);
    }

    private bool ApplicationExists(int? id) => _context.Applications.Any(e => e.Id == id);
}
