$p = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$e = [System.Text.Encoding]::Unicode
$c = [System.IO.File]::ReadAllText($p, $e)
$orig = $c.Length
Write-Host "Original length: $orig"

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 1: // ===== Applications ===== section comment + Applications() action
# ──────────────────────────────────────────────────────────────────────────────
$b1 = "`r`n`r`n    // ===== Applications =====`r`n`r`n    public async Task<IActionResult> Applications()`r`n    {`r`n        var jobsWithApplications = await _context.Jobs`r`n            .Include(j => j.Applications)`r`n            .OrderByDescending(j => j.Id)`r`n            .ToListAsync();`r`n`r`n        return View(jobsWithApplications);`r`n    }"
if ($c.Contains($b1)) { $c = $c.Replace($b1, ""); Write-Host "Block 1 (Applications) removed." }
else { Write-Warning "Block 1 NOT FOUND - check exact text" }

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 2: // ===== Search ===== comment + JobDetailSearch() action
# ──────────────────────────────────────────────────────────────────────────────
# Use regex because the method signature wraps and block is large
$searchStart = $c.IndexOf("`r`n// ===== Search =====")
if ($searchStart -lt 0) { Write-Warning "Block 2 start NOT FOUND"; }
else {
    # Find the end: right before the [HttpGet] that precedes the Search() action
    # The Search() action is preceded by two [HttpGet] annotations
    $searchEnd = $c.IndexOf("    [HttpGet]`r`n    [HttpGet]`r`n    public async Task<IActionResult> Search(")
    if ($searchEnd -lt 0) { Write-Warning "Block 2 end NOT FOUND" }
    else {
        $c = $c.Substring(0, $searchStart) + "`r`n`r`n" + $c.Substring($searchEnd)
        Write-Host "Block 2 (Search comment + JobDetailSearch) removed."
    }
}

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 3: Candidates() action
# ──────────────────────────────────────────────────────────────────────────────
$candsStart = $c.IndexOf("`r`n`r`n    public async Task<IActionResult> Candidates(string? search)")
if ($candsStart -lt 0) { Write-Warning "Block 3 (Candidates) start NOT FOUND" }
else {
    # Ends with ViewData["Search"] = search; + return View(results); + }
    $candsMarker = "        return View(results);`r`n    }`r`n`r`n`r`n    public async Task<IActionResult> SearchCandidates("
    $candsEnd = $c.IndexOf($candsMarker, $candsStart)
    if ($candsEnd -lt 0) {
        # Try single blank line separator
        $candsMarker = "        return View(results);`r`n    }`r`n`r`n    public async Task<IActionResult> SearchCandidates("
        $candsEnd = $c.IndexOf($candsMarker, $candsStart)
    }
    if ($candsEnd -lt 0) { Write-Warning "Block 3 end NOT FOUND" }
    else {
        # Find the length of just the Candidates block (up to the closing })
        $afterCands = $c.IndexOf("`r`n`r`n    public async Task<IActionResult> SearchCandidates(", $candsStart)
        $c = $c.Substring(0, $candsStart) + $c.Substring($afterCands)
        Write-Host "Block 3 (Candidates) removed."
    }
}

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 4: SearchCandidates() action
# ──────────────────────────────────────────────────────────────────────────────
$scStart = $c.IndexOf("`r`n`r`n    public async Task<IActionResult> SearchCandidates(string? searchQuery)")
if ($scStart -lt 0) { Write-Warning "Block 4 (SearchCandidates) start NOT FOUND" }
else {
    $scNext = $c.IndexOf("`r`n`r`n    [HttpGet]`r`n    public async Task<IActionResult> JobDetailSearchApi(", $scStart)
    if ($scNext -lt 0) { Write-Warning "Block 4 end NOT FOUND" }
    else {
        $c = $c.Substring(0, $scStart) + $c.Substring($scNext)
        Write-Host "Block 4 (SearchCandidates) removed."
    }
}

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 5: [HttpGet] + JobDetailSearchApi() action
# ──────────────────────────────────────────────────────────────────────────────
$jdsaStart = $c.IndexOf("`r`n`r`n    [HttpGet]`r`n    public async Task<IActionResult> JobDetailSearchApi(")
if ($jdsaStart -lt 0) { Write-Warning "Block 5 (JobDetailSearchApi) start NOT FOUND" }
else {
    $jdsaNext = $c.IndexOf("`r`n`r`n    [HttpGet]`r`n    public async Task<IActionResult> SearchCandidatesApi(", $jdsaStart)
    if ($jdsaNext -lt 0) { Write-Warning "Block 5 end NOT FOUND" }
    else {
        $c = $c.Substring(0, $jdsaStart) + $c.Substring($jdsaNext)
        Write-Host "Block 5 (JobDetailSearchApi) removed."
    }
}

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 6: [HttpGet] + SearchCandidatesApi() action
# ──────────────────────────────────────────────────────────────────────────────
$scaStart = $c.IndexOf("`r`n`r`n    [HttpGet]`r`n    public async Task<IActionResult> SearchCandidatesApi(")
if ($scaStart -lt 0) { Write-Warning "Block 6 (SearchCandidatesApi) start NOT FOUND" }
else {
    $scaNext = $c.IndexOf("`r`n`r`n    // ===== Resume Review =====", $scaStart)
    if ($scaNext -lt 0) { Write-Warning "Block 6 end NOT FOUND" }
    else {
        $c = $c.Substring(0, $scaStart) + $c.Substring($scaNext)
        Write-Host "Block 6 (SearchCandidatesApi) removed."
    }
}

# ──────────────────────────────────────────────────────────────────────────────
# BLOCK 7: GetCandidatesJson() + GetUnifiedCandidatesQuery() helper
# ──────────────────────────────────────────────────────────────────────────────
# Find start: the line with public async Task<IActionResult> GetCandidatesJson
$gjStart = $c.LastIndexOf("`r`n") 
# Actually find the \r\n before GetCandidatesJson
$gjMarker = "public async Task<IActionResult> GetCandidatesJson("
$gjIdx = $c.IndexOf($gjMarker)
if ($gjIdx -lt 0) { Write-Warning "Block 7 (GetCandidatesJson) NOT FOUND" }
else {
    # Go back to find the preceding \r\n (start of line)
    $gjLineStart = $c.LastIndexOf("`r`n", $gjIdx)
    # Find the end: just before [HttpGet]\r\n    public async Task<IActionResult> GetAvailableStages
    $gjNext = $c.IndexOf("`r`n    [HttpGet]`r`n    public async Task<IActionResult> GetAvailableStages(", $gjIdx)
    if ($gjNext -lt 0) { Write-Warning "Block 7 end NOT FOUND" }
    else {
        $c = $c.Substring(0, $gjLineStart) + $c.Substring($gjNext)
        Write-Host "Block 7 (GetCandidatesJson + GetUnifiedCandidatesQuery) removed."
    }
}

Write-Host "New length: $($c.Length)  (removed $($orig - $c.Length) chars)"
[System.IO.File]::WriteAllText($p, $c, $e)
Write-Host "File written successfully."
