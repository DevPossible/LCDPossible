#Requires -Version 7.0
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    # Run unit tests (all tests in the solution, excluding integration tests in /tests/)
    dotnet test src/LCDPossible.sln --configuration Release --no-build
}
finally {
    Pop-Location
}
