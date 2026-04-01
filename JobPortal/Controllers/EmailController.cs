using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using JobPortal.Models;
using JobPortal.Services.Interfaces;

[Authorize]
[Route("admin")]
public class EmailController : Controller
{
    private readonly IEmailService _emailService;

    public EmailController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpGet("[action]")]
    public async Task<IActionResult> EmailTemplates()
    {
        var templates = await _emailService.GetAllAsync();
        return View(templates);
    }

    [HttpPost("[action]")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate([Bind("Id,Name,Subject,BodyContent")] EmailTemplate template)
    {
        if (!ModelState.IsValid)
            return RedirectToAction("EmailTemplates");

        var saved = await _emailService.SaveTemplateAsync(template);
        if (!saved)
            return NotFound();

        TempData["Success"] = $"Template '{template.Name}' saved.";
        return RedirectToAction("EmailTemplates");
    }

    [HttpPost("[action]")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMockEmail(int templateId, int candidateId)
    {
        var userId  = User.Identity?.Name ?? "System";
        var success = await _emailService.SendMockEmailAsync(templateId, candidateId, userId);

        if (!success)
            return Json(new { success = false, error = "Template or candidate not found." });

        return Json(new { success = true });
    }
}
