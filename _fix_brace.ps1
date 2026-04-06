$fp = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$enc = [System.Text.Encoding]::Unicode
$c = [System.IO.File]::ReadAllText($fp, $enc)
$lines = [System.Collections.Generic.List[string]]($c -split "`r`n")
# Remove the extra } at line index 239
Write-Host "Line 239: '$($lines[239])'"
$lines.RemoveAt(239)
$result = $lines -join "`r`n"
[System.IO.File]::WriteAllText($fp, $result, $enc)
Write-Host "Fixed. Now $($lines.Count) lines"
