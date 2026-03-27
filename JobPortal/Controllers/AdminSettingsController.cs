using JobPortal.Data;
using JobPortal.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JobPortal.Models.ViewModels;

[Authorize(AuthenticationSchemes = "AdminAuth")]
public class AdminSettingsController : Controller
{
    private const string AtLeastOneFacetMessage = "A scorecard template must have at least one facet.";

    private readonly AppDbContext _context;
    private readonly IFacetService _facetService;
    private readonly IScorecardTemplateService _templateService;

    public AdminSettingsController(AppDbContext context, IFacetService facetService, IScorecardTemplateService templateService)
    {
        _context = context;
        _facetService = facetService;
        _templateService = templateService;
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
                if (templateFacet.ScorecardFacetId == 0)
                    continue;

                if (!templateNamesByFacetId.TryGetValue(templateFacet.ScorecardFacetId, out var names))
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
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(name), "Name is required.");

        if (!ModelState.IsValid)
            return View();

        try
        {
            await _facetService.CreateFacet(name, displayOrder);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
            return View();
        }
        catch (InvalidOperationException exception)
        {
            var modelStateKey = exception.Message.Contains("display order", StringComparison.OrdinalIgnoreCase)
                ? nameof(displayOrder)
                : nameof(name);
            ModelState.AddModelError(modelStateKey, exception.Message);
            return View();
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var facet = (await _facetService.GetAllFacets()).FirstOrDefault(f => f.Id == id);
        if (facet == null)
            return NotFound();

        return View(facet);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string name, int displayOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            ModelState.AddModelError(nameof(name), "Name is required.");

        if (!ModelState.IsValid)
        {
            return View(new JobPortal.Models.ScorecardFacet
            {
                Id = id,
                Name = name,
                DisplayOrder = displayOrder,
                IsActive = isActive
            });
        }

        try
        {
            await _facetService.UpdateFacet(id, name, displayOrder, isActive);
            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(name), exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            var modelStateKey = exception.Message.Contains("display order", StringComparison.OrdinalIgnoreCase)
                ? nameof(displayOrder)
                : nameof(name);
            ModelState.AddModelError(modelStateKey, exception.Message);
        }

        return View(new JobPortal.Models.ScorecardFacet
        {
            Id = id,
            Name = name,
            DisplayOrder = displayOrder,
            IsActive = isActive
        });
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

        if (facets.GroupBy(f => f.DisplayOrder).Any(group => group.Count() > 1))
            ModelState.AddModelError(string.Empty, "Duplicate display order values are not allowed.");

        if (facets.Any(f => f.DisplayOrder <= 0))
            ModelState.AddModelError(string.Empty, "Display order must be a positive integer.");

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

    private async Task<EditTemplateFacetsViewModel?> BuildTemplateFacetEditorViewModel(int templateId, List<TemplateFacetInput>? postedFacets = null)
    {
        var template = await _templateService.GetTemplateById(templateId);
        if (template == null)
            return null;

        var allFacets = await _facetService.GetAllFacets();
        var assignedFacets = postedFacets ?? (await _templateService.GetFacetsForTemplate(templateId))
            .Select(f => new TemplateFacetInput
            {
                FacetId = f.ScorecardFacetId,
                DisplayOrder = f.DisplayOrder,
                Description = f.Description,
                NotesPlaceholder = f.NotesPlaceholder,
                CategoryId = f.CategoryId
            })
            .ToList();

        var assignedByFacetId = assignedFacets
            .GroupBy(f => f.FacetId)
            .ToDictionary(group => group.Key, group => group.First());

        var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

        return new EditTemplateFacetsViewModel
        {
            TemplateId = template.Id,
            TemplateName = template.Name,
            Categories = categories,
            Facets = allFacets
                .OrderBy(f => f.DisplayOrder)
                .Select(f =>
                {
                    assignedByFacetId.TryGetValue(f.Id, out var assigned);
                    return new TemplateFacetSelectionViewModel
                    {
                        FacetId = f.Id,
                        FacetName = f.Name,
                        IsSelected = assigned != null,
                        DisplayOrder = assigned?.DisplayOrder ?? f.DisplayOrder,
                        Description = assigned?.Description,
                        NotesPlaceholder = assigned?.NotesPlaceholder,
                        CategoryId = assigned?.CategoryId
                    };
                })
                .ToList()
        };
    }
}