$p = "C:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$t = [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::Unicode)

$newActions = "
    // -- Email Templates ---------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> EmailTemplates()
    {
        var templates = await _context.EmailTemplates
            .OrderBy(t => t.Name)
            .ToListAsync();
        return View(templates);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(EmailTemplate template)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(""EmailTemplates"");

        template.LastModified = DateTime.UtcNow;

        if (template.Id == 0)
        {
            _context.EmailTemplates.Add(template);
        }
        else
        {
            var existing = await _context.EmailTemplates.FindAsync(template.Id);
            if (existing == null)
                return NotFound();
            existing.Name         = template.Name;
            existing.Subject      = template.Subject;
            existing.BodyContent  = template.BodyContent;
            existing.LastModified = template.LastModified;
        }

        await _context.SaveChangesAsync();
        TempData[""Success""] = `$""Template '{template.Name}' saved."";
        return RedirectToAction(""EmailTemplates"");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMockEmail(int templateId, int candidateId)
    {
        var template  = await _context.EmailTemplates.FindAsync(templateId);
        var candidate = await _context.Candidates.FindAsync(candidateId);

        if (template == null || candidate == null)
            return Json(new { success = false, error = ""Template or candidate not found."" });

        var userId = User.Identity?.Name ?? ""System"";
        await _auditService.LogAsync(
            entityName: ""EmailTemplate"",
            entityId:   templateId,
            action:     ""EmailSent"",
            changes:    `$""Template: {template.Name} | Recipient: {candidate.Email} | Candidate: {candidate.FirstName} {candidate.LastName}"",
            userId:     userId);

        return Json(new { success = true });
    }

"

$marker = "    [HttpGet]`r`n    public IActionResult Profile()"
$idx = $t.IndexOf($marker)
if ($idx -lt 0) {
    Write-Host "MARKER NOT FOUND - trying alternate"
    $marker = "    [HttpGet]`n    public IActionResult Profile()"
    $idx = $t.IndexOf($marker)
}
Write-Host "Marker idx: $idx"

$newText = $t.Substring(0, $idx) + $newActions + $t.Substring($idx)
[System.IO.File]::WriteAllText($p, $newText, [System.Text.Encoding]::Unicode)
Write-Host "Done. New length: $($newText.Length)"
