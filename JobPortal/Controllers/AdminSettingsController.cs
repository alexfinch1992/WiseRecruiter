using JobPortal.Data;
using JobPortal.Models;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Authorize]
public class AdminSettingsController : Controller
{
    private const string AtLeastOneFacetMessage = "A scorecard template must have at least one facet.";

    private readonly AppDbContext _context;
    private readonly IFacetService _facetService;
    private readonly IScorecardTemplateService _templateService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminSettingsController(AppDbContext context, IFacetService facetService, IScorecardTemplateService templateService, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _facetService = facetService;
        _templateService = templateService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var facets = await _facetService.GetAllFacets();
        var templates = await _templateService.GetAllTemplates();

        var templateNamesByFacetId = facets.ToDictionary(f => f.Id, _ => new List<string>());

        foreach (var template in templates)
        {
            var templateFacets = await _templateService.GetFacetsForTemplate(template.Id);
            foreach (var templateFacet in templateFacets)
            {
                if (templateFacet.FacetId == 0)
                    continue;

                if (!templateNamesByFacetId.TryGetValue(templateFacet.FacetId, out var names))
                    continue;

                if (!names.Contains(template.Name, StringComparer.OrdinalIgnoreCase))
                    names.Add(template.Name);
            }
        }

        foreach (var names in templateNamesByFacetId.Values)
            names.Sort(StringComparer.OrdinalIgnoreCase);

        ViewBag.TemplateNamesByFacetId = templateNamesByFacetId;
        return View(facets);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string? description, string? notesPlaceholder, int? categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(name), "Name is required.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View();
        }

        try
        {
            var facet = await _facetService.CreateFacet(name);
            await _facetService.UpdateFacet(facet.Id, facet.Name, description, notesPlaceholder, categoryId);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }

        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var facet = await _facetService.GetFacetById(id);
        if (facet == null)
            return NotFound();

        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(facet);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, string? description, string? notesPlaceholder, int? categoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(name), "Name is required.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(new JobPortal.Models.Facet { Id = id, Name = name, Description = description, NotesPlaceholder = notesPlaceholder, CategoryId = categoryId });
        }

        try
        {
            await _facetService.UpdateFacet(id, name, description, notesPlaceholder, categoryId);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }

        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(new JobPortal.Models.Facet { Id = id, Name = name, Description = description, NotesPlaceholder = notesPlaceholder, CategoryId = categoryId });
    }

    [HttpGet]
    public async Task<IActionResult> Templates()
    {
        var templates = await _templateService.GetAllTemplates();
        return View(templates);
    }

    [HttpGet]
    public IActionResult CreateTemplate()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(name), "Name is required.");

        if (!ModelState.IsValid)
            return View();

        try
        {
            await _templateService.CreateTemplate(name);
            return RedirectToAction(nameof(Templates));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> EditTemplate(int id)
    {
        var template = await _templateService.GetTemplateById(id);
        if (template == null)
            return NotFound();

        return View(template);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(int id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(name), "Name is required.");

        if (!ModelState.IsValid)
            return View(new JobPortal.Models.ScorecardTemplate { Id = id, Name = name });

        try
        {
            await _templateService.UpdateTemplateName(id, name);
            return RedirectToAction(nameof(Templates));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }

        return View(new JobPortal.Models.ScorecardTemplate { Id = id, Name = name });
    }

    [HttpGet]
    public async Task<IActionResult> EditTemplateFacets(int templateId)
    {
        var viewModel = await BuildTemplateFacetEditorViewModel(templateId);
        if (viewModel == null)
            return NotFound();

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplateFacets(int templateId, List<TemplateFacetInput> facets)
    {
        facets ??= new List<TemplateFacetInput>();

        if (facets.Count == 0)
            ModelState.AddModelError(string.Empty, AtLeastOneFacetMessage);

        if (facets.GroupBy(f => f.FacetId).Any(group => group.Count() > 1))
            ModelState.AddModelError(string.Empty, "Duplicate facets are not allowed.");

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildTemplateFacetEditorViewModel(templateId, facets);
            if (invalidModel == null)
                return NotFound();

            return View(invalidModel);
        }

        try
        {
            await _templateService.UpdateTemplateFacets(templateId, facets);
            return RedirectToAction(nameof(Templates));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            if (string.Equals(exception.Message, AtLeastOneFacetMessage, StringComparison.Ordinal))
                ModelState.AddModelError(string.Empty, AtLeastOneFacetMessage);
            else
                ModelState.AddModelError(string.Empty, exception.Message);
        }

        var viewModel = await BuildTemplateFacetEditorViewModel(templateId, facets);
        if (viewModel == null)
            return NotFound();

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ManageTeam()
    {
        var hiringManagers = await _userManager.GetUsersInRoleAsync("HiringManager");
        var allJobs = await _context.Jobs.OrderBy(j => j.Title).ToListAsync();
        var allAssignments = await _context.JobAssignments.ToListAsync();

        var rows = hiringManagers.Select(u => new UserAssignmentRow
        {
            UserId = u.Id,
            UserName = u.UserName ?? u.Email ?? u.Id,
            FullName = string.IsNullOrWhiteSpace(u.FullName) ? (u.UserName ?? u.Email ?? u.Id) : u.FullName,
            AssignedJobIds = allAssignments.Where(a => a.UserId == u.Id).Select(a => a.JobId).ToList()
        }).ToList();

        return View(new ManageTeamViewModel { Users = rows, AllJobs = allJobs });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssignment(string userId, int jobId, bool isAssigned)
    {
        var existing = await _context.JobAssignments
            .FirstOrDefaultAsync(ja => ja.UserId == userId && ja.JobId == jobId);

        if (isAssigned && existing == null)
            _context.JobAssignments.Add(new JobAssignment { UserId = userId, JobId = jobId });
        else if (!isAssigned && existing != null)
            _context.JobAssignments.Remove(existing);

        await _context.SaveChangesAsync();
        return Ok();
    }

    private async Task<EditTemplateFacetsViewModel?> BuildTemplateFacetEditorViewModel(int templateId, List<TemplateFacetInput>? postedFacets = null)
    {
        var template = await _templateService.GetTemplateById(templateId);
        if (template == null)
            return null;

        var allFacets = await _facetService.GetAllFacets();
        var assignedFacets = postedFacets ?? (await _templateService.GetFacetsForTemplate(templateId))
            .Select(f => new TemplateFacetInput { FacetId = f.FacetId })
            .ToList();

        var assignedByFacetId = assignedFacets
            .GroupBy(f => f.FacetId)
            .ToDictionary(group => group.Key, group => group.First());

        return new EditTemplateFacetsViewModel
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            Facets = allFacets
                .OrderBy(f => f.Name)
                .Select(f =>
                {
                    assignedByFacetId.TryGetValue(f.Id, out var assigned);
                    return new TemplateFacetSelectionViewModel
                    {
                        FacetId = f.Id,
                        FacetName = f.Name,
                        IsSelected = assigned != null
                    };
                })
                .ToList()
        };
    }
}