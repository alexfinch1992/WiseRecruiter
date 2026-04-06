$path = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$enc = [System.Text.Encoding]::Unicode
$text = [System.IO.File]::ReadAllText($path, $enc)

# Check if already patched
if ($text.Contains("private void SetCandidateNavigation")) {
    Write-Host "ALREADY PATCHED"
    exit 0
}

# Insert the helper method after the CandidateDetails closing brace
# Find: "    }\r\n\r\n    [HttpPost]\r\n    [ValidateAntiForgeryToken]\r\n    public async Task<IActionResult> UpdateApplicationStage"
$anchor = "    }" + [char]13 + [char]10 + [char]13 + [char]10 + "    [HttpPost]" + [char]13 + [char]10 + "    [ValidateAntiForgeryToken]" + [char]13 + [char]10 + "    public async Task<IActionResult> UpdateApplicationStage"

$idx = $text.IndexOf($anchor)
Write-Host "Anchor index: $idx"

if ($idx -lt 0) {
    Write-Host "ERROR: Anchor not found"
    exit 1
}

$helperMethod = @"

    private void SetCandidateNavigation(string? ids, int? idx)
    {
        if (string.IsNullOrEmpty(ids) || !idx.HasValue) return;

        var idList = new List<int>();
        foreach (var s in ids.Split(','))
            if (int.TryParse(s.Trim(), out var v)) idList.Add(v);

        var i = idx.Value;
        if (i < 0 || i >= idList.Count) return;

        ViewBag.NavIds = ids;
        ViewBag.NavIdx = i;
        ViewBag.NavTotal = idList.Count;
        ViewBag.NavPrevId = i > 0 ? idList[i - 1] : (int?)null;
        ViewBag.NavPrevIdx = i > 0 ? i - 1 : (int?)null;
        ViewBag.NavNextId = i < idList.Count - 1 ? idList[i + 1] : (int?)null;
        ViewBag.NavNextIdx = i < idList.Count - 1 ? i + 1 : (int?)null;
    }

"@

# Insert the helper method between the closing brace and the [HttpPost]
$replacement = "    }" + [char]13 + [char]10 + $helperMethod + [char]13 + [char]10 + "    [HttpPost]" + [char]13 + [char]10 + "    [ValidateAntiForgeryToken]" + [char]13 + [char]10 + "    public async Task<IActionResult> UpdateApplicationStage"

$text = $text.Remove($idx, $anchor.Length).Insert($idx, $replacement)
[System.IO.File]::WriteAllText($path, $text, $enc)
Write-Host "SUCCESS"
