$fp = "c:\Dev\Application Site\JobPortal\Controllers\AdminController.cs"
$c = [System.IO.File]::ReadAllText($fp, [System.Text.Encoding]::Unicode)
$l = $c -split "`r`n"
Write-Host "TOTAL: $($l.Count)"
for ($i = [int]$args[0]; $i -le [int]$args[1]; $i++) {
    Write-Host "${i}: $($l[$i])"
}
