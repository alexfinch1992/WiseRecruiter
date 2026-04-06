$fp = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$enc = [System.Text.Encoding]::Unicode
$c = [System.IO.File]::ReadAllText($fp, $enc)

# 1. Add field after _userManager field
$c = $c.Replace(
    "private readonly UserManager<ApplicationUser>? _userManager;",
    "private readonly UserManager<ApplicationUser>? _userManager;`r`n    private readonly IJobAssignmentService? _jobAssignmentService;"
)

# 2. Add constructor parameter after userManager
$c = $c.Replace(
    "UserManager<ApplicationUser>? userManager = null)",
    "UserManager<ApplicationUser>? userManager = null,`r`n        IJobAssignmentService? jobAssignmentService = null)"
)

# 3. Add assignment after _userManager = userManager;
$c = $c.Replace(
    "_userManager = userManager;`r`n    }",
    "_userManager = userManager;`r`n        _jobAssignmentService = jobAssignmentService;`r`n    }"
)

# 4. Replace owner validation + job creation + reviewer assignment block
# Find the block from "// Validate owner" through "Console.WriteLine(""Reviewers assigned"");"
# and replace with service calls

$oldBlock = @"
            // Validate owner
            if (!string.IsNullOrEmpty(model.OwnerUserId))
            {
                var ownerUser = await _context.Users.FindAsync(model.OwnerUserId);
                if (ownerUser == null)
                {
                    ModelState.AddModelError("OwnerUserId", "Selected owner does not exist.");
                    await PopulateScorecardTemplateOptions(model.ScorecardTemplateId);
                    await PopulateOwnerAndReviewerOptions();
                    return View(model);
                }
            }

            try
            {
                Console.WriteLine("Creating job...");

                var job = new Job
                {
                    Title = model.Title,
                    Description = model.Description,
                    ScorecardTemplateId = model.ScorecardTemplateId,
                    OwnerUserId = string.IsNullOrEmpty(model.OwnerUserId) ? null : model.OwnerUserId
                };

                await _jobCommandService.CreateJobAsync(job);
                Console.WriteLine($"Job created successfully with Id={job.Id}");

                // Assign reviewers
                if (model.ReviewerUserIds != null && model.ReviewerUserIds.Any())
                {
                    Console.WriteLine($"Assigning {model.ReviewerUserIds.Count} reviewers...");
                    foreach (var userId in model.ReviewerUserIds)
                    {
                        var user = await _context.Users.FindAsync(userId);
                        if (user != null)
                        {
                            var roles = await _context.UserRoles
                                .Where(ur => ur.UserId == userId)
                                .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                                .ToListAsync();
                            if (roles.Any(r => r == "Admin" || r == "Recruiter"))
                            {
                                _context.Set<JobUser>().Add(new JobUser
                                {
                                    JobId = job.Id,
                                    UserId = userId,
                                    Role = "Reviewer",
                                    IsActive = true
                                });
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                    Console.WriteLine("Reviewers assigned");
                }
            }
"@

$newBlock = @"
            try
            {
                Console.WriteLine("Creating job...");

                var job = new Job
                {
                    Title = model.Title,
                    Description = model.Description,
                    ScorecardTemplateId = model.ScorecardTemplateId
                };

                await _jobCommandService.CreateJobAsync(job);
                Console.WriteLine($"Job created successfully with Id={job.Id}");

                await _jobAssignmentService!.AssignOwnerAsync(job.Id, model.OwnerUserId);
                await _jobAssignmentService.AssignReviewersAsync(job.Id, model.ReviewerUserIds);
            }
"@

# Normalize line endings in the search string to match the file
$oldBlock = $oldBlock -replace "`r?`n", "`r`n"
$newBlock = $newBlock -replace "`r?`n", "`r`n"

if ($c.Contains($oldBlock)) {
    $c = $c.Replace($oldBlock, $newBlock)
    Write-Host "Replaced assignment block successfully"
} else {
    Write-Host "ERROR: Could not find old block to replace"
    # Debug: check if partial match exists
    $lines = $oldBlock -split "`r`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if (-not $c.Contains($lines[$i])) {
            Write-Host "First non-matching line $i`: $($lines[$i].Substring(0, [Math]::Min(60, $lines[$i].Length)))"
            break
        }
    }
    exit 1
}

[System.IO.File]::WriteAllText($fp, $c, $enc)
Write-Host "Done!"
