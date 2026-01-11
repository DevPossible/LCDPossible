#Requires -Version 7.0
param(
    [switch]$Build  # Trigger a build before running (default: use existing build)
)
$ErrorActionPreference = 'Stop'

# All arguments passed to this script are forwarded to the application
# Usage: ./start-app.ps1 serve                  # Run without building
# Usage: ./start-app.ps1 -Build serve           # Build first, then run
# Usage: ./start-app.ps1 status                 # Check service status

$ProjectName = 'LCDPossible'
$Configuration = 'Release'
$Framework = 'net10.0'

Push-Location $PSScriptRoot
try {
    # Build only if -Build flag specified
    if ($Build) {
        & ./build.ps1
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    }

    # Find and run the compiled executable
    $exePath = Join-Path $PSScriptRoot ".build/$ProjectName/bin/$Configuration/$Framework/$ProjectName.exe"

    if (-not (Test-Path $exePath)) {
        # Fallback for non-Windows or different output
        $exePath = Join-Path $PSScriptRoot ".build/$ProjectName/bin/$Configuration/$Framework/$ProjectName"
    }

    if (-not (Test-Path $exePath)) {
        throw "Executable not found at: $exePath`nRun ./build.ps1 first or use -Build flag."
    }

    # Run the compiled app with all arguments forwarded
    & $exePath @args
}
finally {
    Pop-Location
}
