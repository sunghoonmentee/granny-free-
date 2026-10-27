<#
.SYNOPSIS
    Compiles the project's C# without starting Unity.

.DESCRIPTION
    Unity will not open without a valid editor licence, and a Personal licence
    lapses about every thirty days. When it does, every check this project has —
    builds, tests, the house tour, the portraits — stops at once, and the only
    way out is for a person to sign in to Unity Hub.

    This is the one check that still works. Unity generates a .csproj per
    assembly definition for IDEs, and those projects reference the engine DLLs
    directly; the .NET SDK can build them, and reading a DLL needs no licence.
    It compiles exactly what Unity would compile, so it catches every syntax
    error and every genuine type error.

    What it cannot tell you is whether the game is any good, or even whether it
    runs: no scene is built, no test is executed, nothing is drawn. Treat a pass
    here as "this would compile", never as "this works".

    The .csproj files are generated, and gitignored. They are regenerated
    whenever Unity opens the project, so on a fresh clone this script has
    nothing to build until Unity has run once.

    That also means this check goes stale the moment a NEW .cs file is added:
    the file is not in any generated project yet, so anything referring to it
    fails here with CS0246 while being perfectly correct. A failure naming a
    type you have just created is this, not your code. Run Unity once — any
    action — and the projects catch up.

.EXAMPLE
    ./tools/compile.ps1
    ./tools/compile.ps1 -Project Granny.Editor.csproj
#>
[CmdletBinding()]
param(
    # One project, instead of all of them.
    [string]$Project
)

$ErrorActionPreference = 'Stop'

$projectDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'GrannyGame'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is not on PATH, so there is no compiler to use.'
}

$projects = if ($Project) {
    @(Join-Path $projectDir $Project)
} else {
    # Editor and the two test assemblies pull in everything else as references,
    # so these three cover every file in the project between them.
    @('Granny.Editor.csproj', 'Granny.Tests.EditMode.csproj', 'Granny.Tests.PlayMode.csproj') |
        ForEach-Object { Join-Path $projectDir $_ }
}

$missing = $projects | Where-Object { -not (Test-Path $_) }

if ($missing) {
    throw "No generated project at $($missing -join ', '). Open the project in Unity once to create them."
}

$failed = 0

foreach ($path in $projects) {
    $name = Split-Path -Leaf $path
    $output = dotnet build $path -nologo -v m 2>&1
    $errors = @($output | Select-String -Pattern ': error ')

    if ($LASTEXITCODE -eq 0 -and $errors.Count -eq 0) {
        Write-Host "$name  ok" -ForegroundColor Green
        continue
    }

    $failed++
    Write-Host "$name  FAILED" -ForegroundColor Red
    $errors | Select-Object -First 25 | ForEach-Object { Write-Host "  $_" }
}

if ($failed -gt 0) { exit 1 }

Write-Host ''
Write-Host 'Compiles. This says nothing about whether it runs or how it looks.' -ForegroundColor DarkYellow
