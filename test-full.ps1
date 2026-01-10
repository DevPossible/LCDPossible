#Requires -Version 7.0
$ErrorActionPreference = 'Stop'

Push-Location $PSScriptRoot
try {
    # Run all tests from both src/ and tests/ directories
    dotnet test src/LCDPossible.sln --configuration Release --no-build

    # Run integration tests from tests/ directory if they exist
    $integrationTests = Get-ChildItem -Path 'tests' -Recurse -Filter '*.csproj' -ErrorAction SilentlyContinue
    if ($integrationTests) {
        foreach ($testProject in $integrationTests) {
            dotnet test $testProject.FullName --configuration Release --no-build
        }
    }
}
finally {
    Pop-Location
}
