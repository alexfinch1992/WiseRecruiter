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
    private readonly SignInManager<ApplicationUser>? _signInManager;
    private readonly IWebHostEnvironment? _environment;

    public AdminSettingsController(AppDbContext context, IFacetService facetService, IScorecardTemplateService templateService, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser>? signInManager = null, IWebHostEnvironment? environment = null)
    {
        _context = context;
        _facetService = facetService;
        _templateService = templateService;
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
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
        var admins         = await _userManager.GetUsersInRoleAsync("Admin");
        var recruiters     = await _userManager.GetUsersInRoleAsync("Recruiter");
        var hiringManagers = await _userManager.GetUsersInRoleAsync("HiringManager");

        var allJobs        = await _context.Jobs.OrderBy(j => j.Title).ToListAsync();
        var allAssignments = await _context.JobAssignments.ToListAsync();

        // Aggregate users across all three roles; priority Admin > Recruiter > HiringManager
        var seenIds = new HashSet<string>();
        var rows    = new List<UserAssignmentRow>();

        foreach (var (users, roleName) in new (IList<ApplicationUser>, string)[]
        {
            (admins,         "Admin"),
            (recruiters,     "Recruiter"),
            (hiringManagers, "HiringManager"),
        })
        {
            foreach (var u in users)
            {
                if (!seenIds.Add(u.Id)) continue;
                rows.Add(new UserAssignmentRow
                {
                    UserId         = u.Id,
                    UserName       = u.UserName ?? u.Email ?? u.Id,
                    FullName       = string.IsNullOrWhiteSpace(u.FullName) ? (u.UserName ?? u.Email ?? u.Id) : u.FullName,
                    Role           = roleName,
                    AssignedJobIds = allAssignments.Where(a => a.UserId == u.Id).Select(a => a.JobId).ToList()
                });
            }
        }

        rows = rows.OrderBy(r => r.FullName).ToList();
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

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string fullName, string email, string role)
    {
        var allowedRoles = new[] { "Admin", "Recruiter", "HiringManager" };

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || !allowedRoles.Contains(role))
            return BadRequest(new { error = "Invalid input. Please supply a full name, a valid email, and a recognised role." });

        var existing = await _userManager.FindByEmailAsync(email.Trim());
        if (existing != null)
            return Conflict(new { error = "A user with that email address already exists." });

        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            FullName = fullName.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, "TempPass123!");
        if (!createResult.Succeeded)
        {
            var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return UnprocessableEntity(new { error = errors });
        }

        var roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return UnprocessableEntity(new { error = "User was created but role assignment failed. Please try again." });
        }

        return Ok(new { message = $"User '{fullName.Trim()}' created successfully with role '{role}'." });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == userId)
            return BadRequest(new { error = "You cannot delete your own account." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new { error = "User not found." });

        var assignments = await _context.JobAssignments
            .Where(ja => ja.UserId == userId)
            .ToListAsync();
        _context.JobAssignments.RemoveRange(assignments);
        await _context.SaveChangesAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(" ", result.Errors.Select(e => e.Description));
            return UnprocessableEntity(new { error = errors });
        }

        return Ok(new { message = $"User '{user.FullName ?? user.Email}' deleted successfully." });
    }

    /// <summary>
    /// Replaces all job assignments for a given user in a single atomic operation.
    /// The front-end sends the full desired set of jobIds; existing rows not in
    /// that set are removed and new ones are added.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateJobAccess(string userId, [FromForm] List<int> jobIds)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        jobIds ??= new List<int>();

        var existing = await _context.JobAssignments
            .Where(ja => ja.UserId == userId)
            .ToListAsync();

        var toRemove = existing.Where(e => !jobIds.Contains(e.JobId)).ToList();
        var existingIds = existing.Select(e => e.JobId).ToHashSet();
        var toAdd = jobIds.Where(id => !existingIds.Contains(id))
                          .Select(id => new JobAssignment { UserId = userId, JobId = id });

        _context.JobAssignments.RemoveRange(toRemove);
        _context.JobAssignments.AddRange(toAdd);
        await _context.SaveChangesAsync();

        return Ok(new { assigned = jobIds.Count });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginAs(string userId)
    {
        if (_environment == null || !_environment.IsDevelopment() || _signInManager == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { error = "userId is required." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new { error = "User not found." });

        await _signInManager.SignOutAsync();
        await _signInManager.SignInAsync(user, isPersistent: true);

        return Ok(new { message = $"Logged in as {user.FullName ?? user.Email}" });
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