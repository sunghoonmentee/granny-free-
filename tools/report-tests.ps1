<#
.SYNOPSIS
    Prints a readable summary of a Unity NUnit results file.

.EXAMPLE
    ./tools/report-tests.ps1 .unity-logs/TestResults-EditMode.xml
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$ResultsPath,

    [switch]$FailuresOnly
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ResultsPath)) {
    Write-Host "No results at $ResultsPath" -ForegroundColor Red
    exit 1
}

[xml]$xml = Get-Content $ResultsPath
$run = $xml.'test-run'

$cases = $xml.SelectNodes('//test-case')
$failures = $xml.SelectNodes("//test-case[@result='Failed']")

$colour = if ($run.failed -eq '0') { 'Green' } else { 'Red' }
Write-Host "$($run.passed)/$($run.total) passed, $($run.failed) failed, $($run.skipped) skipped" -ForegroundColor $colour

if (-not $FailuresOnly) {
    foreach ($case in $cases) {
        $mark = switch ($case.result) {
            'Passed'  { 'ok  ' }
            'Failed'  { 'FAIL' }
            default   { 'skip' }
        }
        $tint = switch ($case.result) {
            'Passed' { 'DarkGray' }
            'Failed' { 'Red' }
            default  { 'DarkYellow' }
        }
        Write-Host "  $mark $($case.name)" -ForegroundColor $tint
    }
}

foreach ($case in $failures) {
    Write-Host "`n$($case.name)" -ForegroundColor Red
    Write-Host "  $($case.failure.message.InnerText)" -ForegroundColor DarkRed
}

if ($run.failed -ne '0') { exit 1 }
exit 0
