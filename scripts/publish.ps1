#Requires -Version 7.0
param(
    [ValidateSet('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$ProjectName = "LCDPossible"
$ProjectPath = "src/$ProjectName/$ProjectName.csproj"
$PublishDir = Join-Path $PSScriptRoot '..' '.build' 'publish' $ProjectName $Runtime

Push-Location (Join-Path $PSScriptRoot '..')
try {
    Write-Host "Publishing $ProjectName for $Runtime..." -ForegroundColor Cyan

    dotnet publish $ProjectPath `
        --configuration Release `
        --runtime $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        --output $PublishDir

    Write-Host "Published to: $PublishDir" -ForegroundColor Green
}
finally {
    Pop-Location
}
