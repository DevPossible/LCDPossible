#Requires -Version 7.0
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    # Run only unit tests (fast) - excludes functional tests which are slow
    dotnet test src/LCDPossible.sln `
        --configuration Release `
        --no-build `
        --filter "FullyQualifiedName!~FunctionalTests"
}
finally {
    Pop-Location
}
