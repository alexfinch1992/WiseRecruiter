$path = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$enc = [System.Text.Encoding]::Unicode
$text = [System.IO.File]::ReadAllText($path, $enc)

$actionCode = @'

    // ===== Manual Candidate Intake =====

    [HttpGet]
    public async Task<IActionResult> AddCandidate(int jobId)
    {
        var job = await _context.Jobs.FindAsync(jobId);
        if (job == null) return NotFound();

        return View(new AddCandidateVm { JobId = jobId, JobTitle = job.Title });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCandidate(AddCandidateVm vm)
    {
        if (!ModelState.IsValid)
        {
            var job = await _context.Jobs.FindAsync(vm.JobId);
            vm.JobTitle = job?.Title;
            return View(vm);
        }

        // Handle resume upload using existing helper
        string? resumePath = null;
        if (vm.Resume != null && vm.Resume.Length > 0)
        {
            var (isValid, errorMessage) = FileUploadHelper.ValidateResume(vm.Resume);
            if (!isValid)
            {
                ModelState.AddModelError("Resume", errorMessage!);
                var job = await _context.Jobs.FindAsync(vm.JobId);
                vm.JobTitle = job?.Title;
                return View(vm);
            }

            var (success, filePath, uploadError) = await FileUploadHelper.SaveResumeAsync(vm.Resume, _webHostEnvironment.WebRootPath);
            if (!success)
            {
                ModelState.AddModelError("Resume", uploadError!);
                var job = await _context.Jobs.FindAsync(vm.JobId);
                vm.JobTitle = job?.Title;
                return View(vm);
            }

            resumePath = filePath;
        }

        // Build Application matching existing apply flow structure
        var application = new Application
        {
            Name = $"{vm.FirstName.Trim()} {vm.LastName.Trim()}",
            Email = vm.Email.Trim(),
            City = vm.City.Trim(),
            JobId = vm.JobId,
            ResumePath = resumePath,
            Stage = ApplicationStage.Applied,
            AppliedDate = DateTime.UtcNow
        };

        try
        {
            await _applicationService.CreateApplicationAsync(application);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            var job = await _context.Jobs.FindAsync(vm.JobId);
            vm.JobTitle = job?.Title;
            return View(vm);
        }

        return RedirectToAction(nameof(JobDetail), new { id = vm.JobId });
    }
'@

# Insert before the final closing brace of the class
$lastBrace = $text.LastIndexOf("}")
$secondLastBrace = $text.LastIndexOf("}", $lastBrace - 1)

$text = $text.Insert($secondLastBrace + 1, "`r`n" + $actionCode)
[System.IO.File]::WriteAllText($path, $text, $enc)
Write-Host "SUCCESS"
