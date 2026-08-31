<#
.SYNOPSIS
    Drives the Unity editor headlessly for this project.

.DESCRIPTION
    Wraps the three things we do from the command line - compile, run an editor
    method, run a test suite - and works around two batchmode quirks:

      * Unity exits after recompiling instead of running the requested work, so
        every action is attempted twice before being reported as failed.
      * A crashed run leaves a lock file behind and the next launch aborts with
        "another Unity instance is running"; stale locks are cleared first.

.EXAMPLE
    ./tools/unity.ps1 compile
    ./tools/unity.ps1 run  -Method Granny.EditorTools.ProjectBootstrap.Run
    ./tools/unity.ps1 test -Platform EditMode
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('compile', 'run', 'test')]
    [string]$Action,

    [string]$Method,

    [ValidateSet('EditMode', 'PlayMode')]
    [string]$Platform = 'EditMode',

    [string]$EditorVersion = '6000.5.8f1',

    [string]$LogDir
)

$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $repoRoot 'GrannyGame'
$unityExe   = "C:\Program Files\Unity\Hub\Editor\$EditorVersion\Editor\Unity.exe"

if (-not (Test-Path $unityExe))   { throw "Unity $EditorVersion not found at $unityExe" }
if (-not (Test-Path $projectDir)) { throw "Unity project not found at $projectDir" }

# Unity rejects a -logFile path whose directory name starts with a dot
# ("... is not a valid directory name") and then dies without writing anything,
# so this folder deliberately has no leading dot.
if (-not $LogDir) { $LogDir = Join-Path $repoRoot 'unity-logs' }
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Wait-ForUnityExit {
    # A finished batchmode run keeps a handle on Temp/ for a moment longer. Launching
    # again too early makes Unity declare the project folder read-only and die before
    # it has even opened a log file.
    for ($i = 0; $i -lt 60; $i++) {
        if (-not (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) {
            Start-Sleep -Milliseconds 750
            return
        }
        Start-Sleep -Seconds 1
    }
    throw 'A Unity process is still running after 60s. Close the editor and retry.'
}

function Clear-StaleLock {
    foreach ($lock in @("$projectDir\Temp\UnityLockfile", "$projectDir\Library\EditorInstance.json")) {
        if (-not (Test-Path $lock)) { continue }
        Remove-Item $lock -Force -ErrorAction SilentlyContinue
        Write-Host "Cleared stale lock: $lock" -ForegroundColor DarkYellow
    }
}

function Invoke-Unity {
    param([string[]]$Arguments, [string]$LogPath)

    Wait-ForUnityExit
    Clear-StaleLock

    # Unity.exe is a GUI-subsystem binary, so PowerShell's call operator returns
    # the instant it launches. Without -Wait every check below would race the
    # editor and report "no results" while the run was still going.
    $proc = Start-Process -FilePath $unityExe `
                          -ArgumentList ($Arguments + @('-logFile', $LogPath)) `
                          -NoNewWindow -Wait -PassThru
    return $proc.ExitCode
}

$stamp = Get-Date -Format 'HHmmss'
$common = @('-batchmode', '-nographics', '-projectPath', $projectDir)

switch ($Action) {
    'compile' {
        $log = Join-Path $LogDir "compile-$stamp.log"
        $code = Invoke-Unity -Arguments ($common + '-quit') -LogPath $log
    }
    'run' {
        if (-not $Method) { throw 'run requires -Method, e.g. Granny.EditorTools.ProjectBootstrap.Run' }
        $log = Join-Path $LogDir "run-$stamp.log"
        $args = $common + @('-quit', '-executeMethod', $Method)

        $code = Invoke-Unity -Arguments $args -LogPath $log
        # A recompile can swallow the first attempt entirely; if the method never
        # logged anything, give it one more go against the now-warm Library.
        if ($code -eq 0 -and -not (Select-String -Path $log -Pattern '\[\w+\] done' -Quiet)) {
            Write-Host 'Method did not run (recompile); retrying...' -ForegroundColor DarkYellow
            $log = Join-Path $LogDir "run-$stamp-retry.log"
            $code = Invoke-Unity -Arguments $args -LogPath $log
        }
    }
    'test' {
        $log     = Join-Path $LogDir "test-$Platform-$stamp.log"
        $results = Join-Path $LogDir "TestResults-$Platform.xml"
        if (Test-Path $results) { Remove-Item $results -Force }

        $args = $common + @('-runTests', '-testPlatform', $Platform, '-testResults', $results)

        $code = Invoke-Unity -Arguments $args -LogPath $log
        if (-not (Test-Path $results)) {
            Write-Host 'No results (recompile); retrying...' -ForegroundColor DarkYellow
            $log  = Join-Path $LogDir "test-$Platform-$stamp-retry.log"
            $code = Invoke-Unity -Arguments $args -LogPath $log
        }
    }
}

if (-not (Test-Path $log)) {
    Write-Host "Unity produced no log at $log - it died before opening one." -ForegroundColor Red
    exit 1
}

$errors = @(Select-String -Path $log -Pattern 'error CS' -ErrorAction SilentlyContinue)
if ($errors.Count -gt 0) {
    Write-Host "`nCompile errors:" -ForegroundColor Red
    $errors | Select-Object -First 20 | ForEach-Object { Write-Host "  $($_.Line)" -ForegroundColor Red }
    exit 1
}

if ($Action -eq 'test') {
    if (-not (Test-Path $results)) {
        Write-Host "No test results produced. See $log" -ForegroundColor Red
        exit 1
    }

    [xml]$xml = Get-Content $results
    $run = $xml.'test-run'
    $failed = $xml.SelectNodes("//test-case[@result='Failed']")

    $colour = if ($run.failed -eq '0') { 'Green' } else { 'Red' }
    Write-Host "`n$Platform : $($run.passed)/$($run.total) passed, $($run.failed) failed" -ForegroundColor $colour

    foreach ($case in $failed) {
        Write-Host "  FAIL $($case.name)" -ForegroundColor Red
        Write-Host "       $($case.failure.message.InnerText)" -ForegroundColor DarkRed
    }

    if ($run.failed -ne '0') { exit 1 }
}
else {
    Write-Host "`n$Action finished (exit $code). Log: $log" -ForegroundColor Green
}

exit 0
