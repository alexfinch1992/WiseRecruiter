$fp = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$enc = [System.Text.Encoding]::Unicode
$txt = [System.IO.File]::ReadAllText($fp, $enc)
$lines = [System.Collections.Generic.List[string]]($txt -split "`r`n")
Write-Host "Before: $($lines.Count) lines"

# Find the target: line with just "}" after scorecard template check (line index 146)
# and the line "[Authorize(Roles = ""Admin"")]" for Edit POST (line index 147)
# We need to insert between these two lines.

$insertAt = -1
for ($i = 130; $i -lt 200; $i++) {
    if ($lines[$i].Trim() -eq '}' -and $i -gt 0 -and $lines[$i+1].Trim() -match '^\[Authorize') {
        # Check if we're inside the Create method (previous lines should be scorecard template check)
        if ($lines[$i-1].Trim() -eq '}' -and $lines[$i-2].Trim() -match 'return View') {
            $insertAt = $i + 1
            Write-Host "Found insertion point at line index $insertAt"
            break
        }
    }
}

if ($insertAt -eq -1) {
    Write-Host "ERROR: Could not find insertion point"
    exit 1
}

$newLines = @(
    ''
    '            // Validate owner'
    '            if (!string.IsNullOrEmpty(model.OwnerUserId))'
    '            {'
    '                var ownerUser = await _context.Users.FindAsync(model.OwnerUserId);'
    '                if (ownerUser == null)'
    '                {'
    '                    ModelState.AddModelError("OwnerUserId", "Selected owner does not exist.");'
    '                    await PopulateScorecardTemplateOptions(model.ScorecardTemplateId);'
    '                    await PopulateOwnerAndReviewerOptions();'
    '                    return View(model);'
    '                }'
    '            }'
    ''
    '            var job = new Job'
    '            {'
    '                Title = model.Title,'
    '                Description = model.Description,'
    '                ScorecardTemplateId = model.ScorecardTemplateId,'
    '                OwnerUserId = string.IsNullOrEmpty(model.OwnerUserId) ? null : model.OwnerUserId'
    '            };'
    ''
    '            try'
    '            {'
    '                await _jobCommandService.CreateJobAsync(job);'
    '            }'
    '            catch (Exception)'
    '            {'
    '                ModelState.AddModelError("", "An error occurred while creating the job.");'
    '                await PopulateScorecardTemplateOptions(model.ScorecardTemplateId);'
    '                await PopulateOwnerAndReviewerOptions();'
    '                return View(model);'
    '            }'
    ''
    '            // Assign reviewers'
    '            if (model.ReviewerUserIds != null && model.ReviewerUserIds.Any())'
    '            {'
    '                foreach (var userId in model.ReviewerUserIds)'
    '                {'
    '                    var user = await _context.Users.FindAsync(userId);'
    '                    if (user != null)'
    '                    {'
    '                        var roles = await _context.UserRoles'
    '                            .Where(ur => ur.UserId == userId)'
    '                            .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)'
    '                            .ToListAsync();'
    '                        if (roles.Any(r => r == "Admin" || r == "Recruiter"))'
    '                        {'
    '                            _context.Set<JobUser>().Add(new JobUser'
    '                            {'
    '                                JobId = job.Id,'
    '                                UserId = userId,'
    '                                Role = "Reviewer",'
    '                                IsActive = true'
    '                            });'
    '                        }'
    '                    }'
    '                }'
    '                await _context.SaveChangesAsync();'
    '            }'
    ''
    '            return RedirectToAction(nameof(Index));'
    '        }'
    ''
    '        await PopulateScorecardTemplateOptions(model.ScorecardTemplateId);'
    '        await PopulateOwnerAndReviewerOptions();'
    '        return View(model);'
    '    }'
    ''
    '    // GET: Admin/Edit/5'
    '    public async Task<IActionResult> Edit(int? id)'
    '    {'
    '        if (id == null)'
    '            return NotFound();'
    ''
    '        var job = await _jobQueryService.GetJobForEditAsync(id.Value);'
    '        if (job == null)'
    '            return NotFound();'
    ''
    '        await PopulateScorecardTemplateOptions(job.ScorecardTemplateId);'
    '        return View(job);'
    '    }'
    ''
)

$strArray = [string[]]$newLines
$lines.InsertRange($insertAt, $strArray)
$result = $lines -join "`r`n"
[System.IO.File]::WriteAllText($fp, $result, $enc)
Write-Host "After: $($lines.Count) lines"
Write-Host "Done!"
