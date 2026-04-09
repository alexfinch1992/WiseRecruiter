using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Models.ViewModels;
using JobPortal.Services.Interfaces;
using JobPortal.Services.Models;
using System.Security.Claims;

[Authorize]
public class HiringRequestController : Controller
{
    private readonly IHiringRequestService _service;

    public HiringRequestController(IHiringRequestService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var requests = await _service.GetAllAsync();
        return View(requests);
    }

    [HttpGet]
    public IActionResult Create() => View(new HiringRequestViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HiringRequestViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var request = await _service.CreateDraftAsync(userId, model);
        return RedirectToAction(nameof(Details), new { id = request.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var request = await _service.GetByIdAsync(id, userId, GetCurrentUserRole());
        if (request == null)
        {
            TempData["ErrorMessage"] = "The requested hiring request could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new HiringRequestViewModel
        {
            RoleTitle         = request.RoleTitle,
            Department        = request.Department,
            LevelBand         = request.LevelBand,
            Location          = request.Location,
            IsReplacement     = request.IsReplacement,
            ReplacementReason = request.ReplacementReason,
            Headcount         = request.Headcount,
            Justification     = request.Justification
        };
        ViewData["RequestId"] = id;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HiringRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewData["RequestId"] = id;
            return View(model);
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.SaveDraftAsync(id, model, userId);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            TransitionResult.Unauthorized => Forbid(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var request = await _service.GetByIdAsync(id, userId, GetCurrentUserRole());
        if (request == null)
        {
            TempData["ErrorMessage"] = "The requested hiring request could not be found.";
            return RedirectToAction(nameof(Index));
        }

        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.SubmitAsync(id, userId);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            TransitionResult.Unauthorized => Forbid(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    // Stage 1 — Senior Talent Lead

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TalentLead")]
    public async Task<IActionResult> ApproveStage1(int id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            TempData["ErrorMessage"] = "Notes are required when approving a request.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.ApproveStage1Async(id, userId, notes);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TalentLead")]
    public async Task<IActionResult> RejectStage1(int id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            TempData["ErrorMessage"] = "Notes are required when rejecting a request.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.RejectStage1Async(id, userId, notes);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,TalentLead")]
    public async Task<IActionResult> RequestMoreInfoStage1(int id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            TempData["ErrorMessage"] = "Notes are required when requesting more information.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.RequestMoreInfoStage1Async(id, userId, notes);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    // Stage 2 — Senior Executive

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ApprovingExecutive")]
    public async Task<IActionResult> ApproveStage2(int id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            TempData["ErrorMessage"] = "Notes are required when approving a request.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.ApproveStage2Async(id, userId, notes);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ApprovingExecutive")]
    public async Task<IActionResult> RejectStage2(int id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            TempData["ErrorMessage"] = "Notes are required when rejecting a request.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.RejectStage2Async(id, userId, notes);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,ApprovingExecutive")]
    public async Task<IActionResult> RequestMoreInfoStage2(int id, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            TempData["ErrorMessage"] = "Notes are required when requesting more information.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = GetCurrentUserId();
        if (userId == null)
            return Forbid();

        var result = await _service.RequestMoreInfoStage2Async(id, userId, notes);
        return result switch
        {
            TransitionResult.NotFound     => NotFound(),
            TransitionResult.InvalidState => BadRequest(),
            _                             => RedirectToAction(nameof(Details), new { id })
        };
    }

    private string? GetCurrentUserId() =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier);

    private string GetCurrentUserRole()
    {
        if (User.IsInRole("Admin")) return "Admin";
        if (User.IsInRole("TalentLead")) return "TalentLead";
        if (User.IsInRole("ApprovingExecutive")) return "ApprovingExecutive";
        return "HiringManager";
    }
}
