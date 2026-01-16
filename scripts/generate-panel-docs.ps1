#Requires -Version 7.0
<#
.SYNOPSIS
    Generates documentation and screenshots for all LCDPossible panels.

.DESCRIPTION
    This script reads all plugin.json files to discover panels, then:
    1. Renders a screenshot of each panel (2 seconds after load)
    2. Generates markdown documentation with examples and parameters
    3. Saves output to /docs/panels/{category}/ organized by panel category

.PARAMETER Resolution
    Target resolution for screenshots (default: 1280x480)

.PARAMETER WaitSeconds
    Seconds to wait before capturing screenshot (default: 2)

.PARAMETER SkipScreenshots
    Skip screenshot generation, only generate markdown

.PARAMETER PanelFilter
    Only generate docs for panels matching this pattern (supports wildcards)

.PARAMETER Build
    Build the solution before running

.PARAMETER Parallel
    Process panels in parallel for faster generation

.PARAMETER ThrottleLimit
    Maximum number of parallel jobs (default: 4, only used with -Parallel)

.PARAMETER EnhanceDescriptions
    Use Ollama to generate enhanced panel descriptions (requires 'ollama' in PATH)

.PARAMETER OllamaModel
    Ollama model to use for description enhancement (default: phi3:mini)

.EXAMPLE
    ./scripts/generate-panel-docs.ps1
    Generate docs for all panels

.EXAMPLE
    ./scripts/generate-panel-docs.ps1 -Parallel
    Generate docs in parallel (4 concurrent jobs)

.EXAMPLE
    ./scripts/generate-panel-docs.ps1 -Parallel -ThrottleLimit 8
    Generate docs with 8 concurrent jobs

.EXAMPLE
    ./scripts/generate-panel-docs.ps1 -PanelFilter "cpu-*"
    Generate docs only for CPU panels

.EXAMPLE
    ./scripts/generate-panel-docs.ps1 -SkipScreenshots
    Regenerate markdown only without screenshots

.EXAMPLE
    ./scripts/generate-panel-docs.ps1 -EnhanceDescriptions
    Generate docs and use Claude CLI to add enhanced overviews

.EXAMPLE
    ./scripts/generate-panel-docs.ps1 -Parallel -ThrottleLimit 8 -SkipScreenshots
    Fast regeneration of markdown only with 8 parallel jobs
#>
param(
    [string]$Resolution = "1280x480",
    [double]$WaitSeconds = 2,
    [switch]$SkipScreenshots,
    [string]$PanelFilter = "*",
    [switch]$Build,
    [switch]$Parallel,
    [int]$ThrottleLimit = 4,
    [switch]$EnhanceDescriptions,
    [string]$OllamaModel = "phi3:mini"
)

$ErrorActionPreference = 'Stop'

# Paths
$RepoRoot = Split-Path $PSScriptRoot -Parent
$PluginsDir = Join-Path $RepoRoot "src/Plugins"
$DocsRoot = Join-Path $RepoRoot "docs"

Push-Location $RepoRoot
try {
    # Build if requested
    if ($Build) {
        Write-Host "Building solution..." -ForegroundColor Cyan
        & ./build.ps1
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    }

    # Find all plugin.json files
    $pluginFiles = Get-ChildItem -Path $PluginsDir -Filter "plugin.json" -Recurse

    if ($pluginFiles.Count -eq 0) {
        Write-Error "No plugin.json files found in $PluginsDir"
        return
    }

    Write-Host "Found $($pluginFiles.Count) plugins" -ForegroundColor Cyan

    # Collect all panels across all plugins
    $allPanels = @()

    foreach ($pluginFile in $pluginFiles) {
        $pluginJson = Get-Content $pluginFile.FullName -Raw | ConvertFrom-Json
        $pluginId = $pluginJson.id
        $pluginName = $pluginJson.name
        $pluginDescription = $pluginJson.description

        # Extract short plugin name from id (e.g., "lcdpossible.core" -> "core")
        $shortPluginName = $pluginId -replace '^lcdpossible\.', ''

        foreach ($panel in $pluginJson.panelTypes) {
            $allPanels += [PSCustomObject]@{
                PluginId = $pluginId
                PluginName = $pluginName
                PluginShortName = $shortPluginName
                PluginDescription = $pluginDescription
                TypeId = $panel.typeId
                DisplayName = $panel.displayName
                Description = $panel.description
                Category = $panel.category
                IsLive = $panel.isLive
                IsAnimated = $panel.isAnimated
                PrefixPattern = $panel.prefixPattern
                Dependencies = $panel.dependencies
                HelpText = $panel.helpText
                Examples = $panel.examples
                Parameters = $panel.parameters
            }
        }
    }

    Write-Host "Found $($allPanels.Count) total panels" -ForegroundColor Cyan

    # Filter panels if pattern specified
    if ($PanelFilter -ne "*") {
        $allPanels = $allPanels | Where-Object { $_.TypeId -like $PanelFilter }
        Write-Host "Filtered to $($allPanels.Count) panels matching '$PanelFilter'" -ForegroundColor Yellow
    }

    # Group by plugin for organization
    $panelsByPlugin = $allPanels | Group-Object PluginShortName

    # Category name mapping (from plugin category to folder name)
    $categoryFolderMap = @{
        'System' = 'system'
        'Screensaver' = 'screensavers'
        'Screensavers' = 'screensavers'
        'Media' = 'media'
        'Proxmox' = 'integrations'
        'Integration' = 'integrations'
        'Integrations' = 'integrations'
    }

    # Helper function to get category folder
    function Get-CategoryFolder($categoryName) {
        if ($categoryFolderMap.ContainsKey($categoryName)) {
            return $categoryFolderMap[$categoryName]
        }
        # Default: lowercase the category name
        return $categoryName.ToLower() -replace '\s+', '-'
    }

    # Generate panel index markdown
    $pluginIndexPath = Join-Path $DocsRoot "panels/README.md"
    $pluginIndexDir = Split-Path $pluginIndexPath -Parent
    if (-not (Test-Path $pluginIndexDir)) {
        New-Item -ItemType Directory -Path $pluginIndexDir -Force | Out-Null
    }

    $indexContent = @"
# Display Panels

LCDPossible includes a variety of display panels organized by category.

## Panel Categories

"@

    # Group by category for the index
    $panelsByCategory = $allPanels | Group-Object Category | Sort-Object Name

    foreach ($category in $panelsByCategory) {
        $categoryFolder = Get-CategoryFolder $category.Name
        $indexContent += "`n### [$($category.Name)]($categoryFolder/)`n`n"
        $indexContent += "| Panel | Description |`n"
        $indexContent += "|-------|-------------|`n"

        foreach ($panel in ($category.Group | Sort-Object DisplayName)) {
            $panelLink = "$categoryFolder/$($panel.TypeId).md"
            $displayId = if ($panel.PrefixPattern) { $panel.PrefixPattern } else { $panel.TypeId }
            $indexContent += "| [$($panel.DisplayName)]($panelLink) | $($panel.Description) |`n"
        }
    }

    $indexContent += @"

## Using Panels

### Display a Panel

``````bash
lcdpossible show cpu-info
``````

### Display Multiple Panels (Slideshow)

``````bash
lcdpossible show cpu-info,gpu-info,ram-info
``````

### Use Wildcards

``````bash
lcdpossible show cpu-*        # All CPU panels
lcdpossible show *-graphic    # All graphic panels
``````

### Apply Modifiers

``````bash
# With effect
lcdpossible show "cpu-info|@effect=matrix-rain"

# With theme
lcdpossible show "cpu-info|@theme=rgb-gaming"

# With duration (seconds)
lcdpossible show "cpu-info|@duration=30"
``````

## Panel Help

Get detailed help for any panel:

``````bash
lcdpossible help-panel proxmox-summary
``````

---

*[Back to Documentation](../README.md)*
"@

    Set-Content -Path $pluginIndexPath -Value $indexContent -Encoding UTF8
    Write-Host "Generated panel index: $pluginIndexPath" -ForegroundColor Green

    # Define the panel processing scriptblock
    $processPanelBlock = {
        param($panel, $DocsRoot, $Resolution, $WaitSeconds, $SkipScreenshots, $RepoRoot, $CategoryFolderMap)

        # Get category folder
        $categoryFolder = if ($CategoryFolderMap.ContainsKey($panel.Category)) {
            $CategoryFolderMap[$panel.Category]
        } else {
            $panel.Category.ToLower() -replace '\s+', '-'
        }

        $panelDir = Join-Path $DocsRoot "panels/$categoryFolder"
        $screenshotDir = Join-Path $panelDir "screenshots"

        # Create directories
        if (-not (Test-Path $panelDir)) {
            New-Item -ItemType Directory -Path $panelDir -Force | Out-Null
        }
        if (-not (Test-Path $screenshotDir)) {
            New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
        }

        $screenshotPath = Join-Path $screenshotDir "$($panel.TypeId).jpg"
        $markdownPath = Join-Path $panelDir "$($panel.TypeId).md"
        $hadError = $false

        # Generate screenshot (unless skipped)
        if (-not $SkipScreenshots) {
            $panelCommand = $panel.TypeId
            $requiresArg = $null -ne $panel.PrefixPattern
            $skipPanels = @('video', 'html', 'web', 'animated-gif', 'image-sequence')
            $baseType = $panel.TypeId -replace ':.*', ''
            $shouldSkip = $skipPanels -contains $baseType

            if ($requiresArg -and -not $shouldSkip) {
                if ($panel.TypeId -eq 'screensaver') {
                    $panelCommand = 'starfield'
                }
                elseif ($panel.TypeId -eq 'falling-blocks') {
                    $panelCommand = 'falling-blocks:1'
                }
                elseif ($panel.TypeId -eq 'bouncing-logo') {
                    $panelCommand = 'bouncing-logo:LCD|color=rainbow|size=large'
                }
            }

            if (-not $shouldSkip) {
                try {
                    Push-Location $RepoRoot
                    & ./start-app.ps1 test $panelCommand -r $Resolution -w $WaitSeconds -o $screenshotDir 2>&1 | Out-Null
                    Pop-Location

                    $possibleOutput = Get-ChildItem -Path $screenshotDir -Filter "*.jpg" -ErrorAction SilentlyContinue | Select-Object -First 1
                    if ($possibleOutput -and $possibleOutput.Name -ne "$($panel.TypeId).jpg") {
                        Move-Item $possibleOutput.FullName $screenshotPath -Force
                    }
                }
                catch {
                    $hadError = $true
                }
            }
        }

        # Generate markdown documentation
        $displayId = if ($panel.PrefixPattern) { $panel.PrefixPattern } else { $panel.TypeId }

        $markdown = @"
# $($panel.DisplayName)

**Panel ID:** ``$displayId``
**Category:** $($panel.Category)
**Plugin:** $($panel.PluginName)
**Live Data:** $(if ($panel.IsLive) { "Yes" } else { "No" })
**Animated:** $(if ($panel.IsAnimated) { "Yes" } else { "No" })

$($panel.Description)

"@

        if (Test-Path $screenshotPath) {
            $markdown += @"

## Screenshot

![$($panel.DisplayName)](screenshots/$($panel.TypeId).jpg)

"@
        }

        if ($panel.HelpText) {
            $markdown += @"

## Details

$($panel.HelpText)

"@
        }

        if ($panel.Parameters -and $panel.Parameters.Count -gt 0) {
            $markdown += @"

## Parameters

| Parameter | Description | Required | Default |
|-----------|-------------|----------|---------|
"@
            foreach ($param in $panel.Parameters) {
                $required = if ($param.required) { "Yes" } else { "No" }
                $default = if ($param.defaultValue) { "``$($param.defaultValue)``" } else { "-" }
                $markdown += "| ``$($param.name)`` | $($param.description) | $required | $default |`n"
            }
            $markdown += "`n"
        }

        if ($panel.Dependencies -and $panel.Dependencies.Count -gt 0) {
            $markdown += @"

## Dependencies

"@
            foreach ($dep in $panel.Dependencies) {
                $markdown += "- $dep`n"
            }
            $markdown += "`n"
        }

        if ($panel.Examples -and $panel.Examples.Count -gt 0) {
            $markdown += @"

## Examples

"@
            foreach ($example in $panel.Examples) {
                $markdown += @"
### $($example.description)

``````bash
$($example.command)
``````

"@
            }
        }

        $markdown += @"

## Profile Usage

### Add to Profile

``````bash
# Add panel to default profile
lcdpossible profile append-panel $displayId

# Add with custom duration (30 seconds)
lcdpossible profile append-panel "$displayId|@duration=30"
"@

        if ($panel.Parameters -and $panel.Parameters.Count -gt 0) {
            $paramExample = ($panel.Parameters | ForEach-Object {
                if ($_.exampleValues -and $_.exampleValues.Count -gt 0) {
                    "$($_.name)=$($_.exampleValues[0])"
                }
            }) -join '|'

            if ($paramExample) {
                $markdown += @"

# Add with custom parameters
lcdpossible profile append-panel "$displayId|$paramExample"
"@
            }
        }

        $markdown += @"

``````

### Quick Show

``````bash
# Display panel immediately
lcdpossible show $displayId
"@

        if ($panel.Parameters -and $panel.Parameters.Count -gt 0) {
            $paramExample = ($panel.Parameters | ForEach-Object {
                if ($_.exampleValues -and $_.exampleValues.Count -gt 0) {
                    "$($_.name)=$($_.exampleValues[0])"
                }
            }) -join '|'

            if ($paramExample) {
                $markdown += @"

# With parameters
lcdpossible show $displayId|$paramExample
"@
            }
        }

        $markdown += @"

``````

---

*Generated by [LCDPossible](https://github.com/DevPossible/lcd-possible)*
"@

        Set-Content -Path $markdownPath -Value $markdown -Encoding UTF8

        # Return result for tracking
        [PSCustomObject]@{
            TypeId = $panel.TypeId
            Success = -not $hadError
            HasScreenshot = Test-Path $screenshotPath
        }
    }

    # Process panels (parallel or sequential)
    $totalPanels = $allPanels.Count

    if ($Parallel) {
        Write-Host "`nProcessing $totalPanels panels in parallel (ThrottleLimit: $ThrottleLimit)..." -ForegroundColor Cyan

        $results = $allPanels | ForEach-Object -Parallel {
            $panel = $_
            $DocsRoot = $using:DocsRoot
            $Resolution = $using:Resolution
            $WaitSeconds = $using:WaitSeconds
            $SkipScreenshots = $using:SkipScreenshots
            $RepoRoot = $using:RepoRoot
            $CategoryFolderMap = $using:categoryFolderMap

            # Get category folder
            $categoryFolder = if ($CategoryFolderMap.ContainsKey($panel.Category)) {
                $CategoryFolderMap[$panel.Category]
            } else {
                $panel.Category.ToLower() -replace '\s+', '-'
            }

            $panelDir = Join-Path $DocsRoot "panels/$categoryFolder"
            $screenshotDir = Join-Path $panelDir "screenshots"

            if (-not (Test-Path $panelDir)) {
                New-Item -ItemType Directory -Path $panelDir -Force | Out-Null
            }
            if (-not (Test-Path $screenshotDir)) {
                New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
            }

            $screenshotPath = Join-Path $screenshotDir "$($panel.TypeId).jpg"
            $markdownPath = Join-Path $panelDir "$($panel.TypeId).md"
            $hadError = $false

            if (-not $SkipScreenshots) {
                $panelCommand = $panel.TypeId
                $requiresArg = $null -ne $panel.PrefixPattern
                $skipPanels = @('video', 'html', 'web', 'animated-gif', 'image-sequence')
                $baseType = $panel.TypeId -replace ':.*', ''
                $shouldSkip = $skipPanels -contains $baseType

                if ($requiresArg -and -not $shouldSkip) {
                    if ($panel.TypeId -eq 'screensaver') { $panelCommand = 'starfield' }
                    elseif ($panel.TypeId -eq 'falling-blocks') { $panelCommand = 'falling-blocks:1' }
                    elseif ($panel.TypeId -eq 'bouncing-logo') { $panelCommand = 'bouncing-logo:LCD|color=rainbow|size=large' }
                }

                if (-not $shouldSkip) {
                    try {
                        Push-Location $RepoRoot
                        & ./start-app.ps1 test $panelCommand -r $Resolution -w $WaitSeconds -o $screenshotDir 2>&1 | Out-Null
                        Pop-Location

                        $possibleOutput = Get-ChildItem -Path $screenshotDir -Filter "*.jpg" -ErrorAction SilentlyContinue | Select-Object -First 1
                        if ($possibleOutput -and $possibleOutput.Name -ne "$($panel.TypeId).jpg") {
                            Move-Item $possibleOutput.FullName $screenshotPath -Force
                        }
                    }
                    catch { $hadError = $true }
                }
            }

            $displayId = if ($panel.PrefixPattern) { $panel.PrefixPattern } else { $panel.TypeId }

            $markdown = @"
# $($panel.DisplayName)

**Panel ID:** ``$displayId``
**Category:** $($panel.Category)
**Plugin:** $($panel.PluginName)
**Live Data:** $(if ($panel.IsLive) { "Yes" } else { "No" })
**Animated:** $(if ($panel.IsAnimated) { "Yes" } else { "No" })

$($panel.Description)

"@

            if (Test-Path $screenshotPath) {
                $markdown += "`n## Screenshot`n`n![$($panel.DisplayName)](screenshots/$($panel.TypeId).jpg)`n"
            }

            if ($panel.HelpText) {
                $markdown += "`n## Details`n`n$($panel.HelpText)`n"
            }

            if ($panel.Parameters -and $panel.Parameters.Count -gt 0) {
                $markdown += "`n## Parameters`n`n| Parameter | Description | Required | Default |`n|-----------|-------------|----------|---------|`n"
                foreach ($param in $panel.Parameters) {
                    $required = if ($param.required) { "Yes" } else { "No" }
                    $default = if ($param.defaultValue) { "``$($param.defaultValue)``" } else { "-" }
                    $markdown += "| ``$($param.name)`` | $($param.description) | $required | $default |`n"
                }
            }

            if ($panel.Dependencies -and $panel.Dependencies.Count -gt 0) {
                $markdown += "`n## Dependencies`n`n"
                foreach ($dep in $panel.Dependencies) { $markdown += "- $dep`n" }
            }

            if ($panel.Examples -and $panel.Examples.Count -gt 0) {
                $markdown += "`n## Examples`n`n"
                foreach ($example in $panel.Examples) {
                    $markdown += "### $($example.description)`n`n``````bash`n$($example.command)`n```````n`n"
                }
            }

            $markdown += @"

## Profile Usage

### Add to Profile

``````bash
# Add panel to default profile
lcdpossible profile append-panel $displayId

# Add with custom duration (30 seconds)
lcdpossible profile append-panel "$displayId|@duration=30"
``````

### Quick Show

``````bash
# Display panel immediately
lcdpossible show $displayId
``````

---

*Generated by [LCDPossible](https://github.com/DevPossible/lcd-possible)*
"@

            Set-Content -Path $markdownPath -Value $markdown -Encoding UTF8

            [PSCustomObject]@{
                TypeId = $panel.TypeId
                Success = -not $hadError
                HasScreenshot = Test-Path $screenshotPath
            }
        } -ThrottleLimit $ThrottleLimit

        $successCount = ($results | Where-Object { $_.Success }).Count
        $errorCount = $totalPanels - $successCount
        $screenshotCount = ($results | Where-Object { $_.HasScreenshot }).Count

        Write-Host "Completed: $successCount successful, $errorCount errors, $screenshotCount screenshots" -ForegroundColor $(if ($errorCount -gt 0) { "Yellow" } else { "Green" })
    }
    else {
        Write-Host "`nProcessing $totalPanels panels sequentially..." -ForegroundColor Cyan
        $processedCount = 0
        $errorCount = 0

        foreach ($panel in $allPanels) {
            $processedCount++
            Write-Host "[$processedCount/$totalPanels] Processing: $($panel.TypeId)" -ForegroundColor Cyan

            $result = & $processPanelBlock $panel $DocsRoot $Resolution $WaitSeconds $SkipScreenshots $RepoRoot $categoryFolderMap

            if ($result.Success) {
                if ($result.HasScreenshot) {
                    Write-Host "  Screenshot + Markdown saved" -ForegroundColor Green
                }
                else {
                    Write-Host "  Markdown saved (no screenshot)" -ForegroundColor Yellow
                }
            }
            else {
                Write-Host "  Error processing panel" -ForegroundColor Red
                $errorCount++
            }
        }
    }

    # Generate category-level index files
    foreach ($category in $panelsByCategory) {
        $categoryFolder = Get-CategoryFolder $category.Name
        $categoryIndexPath = Join-Path $DocsRoot "panels/$categoryFolder/README.md"
        $categoryIndexDir = Split-Path $categoryIndexPath -Parent
        $screenshotDir = Join-Path $categoryIndexDir "screenshots"

        if (-not (Test-Path $categoryIndexDir)) {
            New-Item -ItemType Directory -Path $categoryIndexDir -Force | Out-Null
        }
        if (-not (Test-Path $screenshotDir)) {
            New-Item -ItemType Directory -Path $screenshotDir -Force | Out-Null
        }

        $categoryIndex = @"
# $($category.Name) Panels

| Panel | Description |
|-------|-------------|
"@
        foreach ($panel in ($category.Group | Sort-Object DisplayName)) {
            $displayId = if ($panel.PrefixPattern) { $panel.PrefixPattern } else { $panel.TypeId }
            $categoryIndex += "| [$($panel.DisplayName)]($($panel.TypeId).md) | $($panel.Description) |`n"
        }

        $categoryIndex += @"

---

*[Back to Panels](../README.md)*
"@

        Set-Content -Path $categoryIndexPath -Value $categoryIndex -Encoding UTF8
    }

    # Enhance descriptions using Ollama (if requested)
    if ($EnhanceDescriptions) {
        Write-Host "`nEnhancing panel descriptions using Ollama ($OllamaModel)..." -ForegroundColor Cyan

        # Check if ollama is available
        $ollamaPath = Get-Command ollama -ErrorAction SilentlyContinue
        if (-not $ollamaPath) {
            Write-Host "Warning: 'ollama' command not found in PATH. Skipping description enhancement." -ForegroundColor Yellow
        }
        else {
            # Escape character for ANSI sequence removal
            $esc = [char]27

            foreach ($panel in $allPanels) {
                $categoryFolder = Get-CategoryFolder $panel.Category
                $markdownPath = Join-Path $DocsRoot "panels/$categoryFolder/$($panel.TypeId).md"

                if (Test-Path $markdownPath) {
                    Write-Host "  Enhancing: $($panel.TypeId)" -ForegroundColor DarkGray

                    $currentContent = Get-Content $markdownPath -Raw
                    $displayId = if ($panel.PrefixPattern) { $panel.PrefixPattern } else { $panel.TypeId }

                    # Create prompt for Ollama
                    $prompt = @"
You are a technical writer creating product documentation for LCDPossible, a professional LCD controller application.

Panel: $displayId
Category: $($panel.Category)
Current Description: $($panel.Description)
Help Text: $($panel.HelpText)

Task: Write a concise Overview section (2-3 sentences) using formal, corporate-professional language:
1. Describe the panel's primary function and purpose
2. Highlight key capabilities or typical use cases
3. Maintain a professional, objective tone suitable for enterprise documentation

Output ONLY the overview text. No headers, formatting, markdown, or meta-commentary.
"@

                    try {
                        $enhanced = & ollama run $OllamaModel "$prompt" 2>&1
                        if ($LASTEXITCODE -eq 0 -and $enhanced) {
                            # Clean up the response
                            $enhancedText = ($enhanced -join "`n")

                            # Remove ANSI escape sequences (cursor control, colors, spinner, etc.)
                            $enhancedText = $enhancedText -replace "$esc\[[0-9;?]*[a-zA-Z]", ''
                            $enhancedText = $enhancedText -replace '\[\?[0-9]+[a-z]', ''
                            $enhancedText = $enhancedText -replace '\[[0-9]*[GK]', ''
                            $enhancedText = $enhancedText -replace '[0-9]+[a-z](?=[0-9]+[a-z]|$)', ''

                            # Remove any thinking tags (XML style)
                            $enhancedText = $enhancedText -replace '(?s)<think>.*?</think>', ''

                            # Remove qwen3 thinking output (Thinking... to ...done thinking.)
                            $enhancedText = $enhancedText -replace '(?s)Thinking\.\.\..*?\.\.\.done thinking\.', ''

                            # Remove spinner characters
                            $enhancedText = $enhancedText -replace '[⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏]', ''

                            # Clean up whitespace and trailing garbage
                            $enhancedText = ($enhancedText -split "`n" | Where-Object { $_ -match '[a-zA-Z]' }) -join "`n"
                            $enhancedText = $enhancedText.Trim()

                            if ($enhancedText -and $enhancedText.Length -gt 20) {
                                # Insert overview after the description
                                $insertPoint = $currentContent.IndexOf("`n`n", $currentContent.IndexOf($panel.Description))
                                if ($insertPoint -gt 0) {
                                    $overview = "`n`n## Overview`n`n$enhancedText"
                                    $newContent = $currentContent.Insert($insertPoint, $overview)
                                    Set-Content -Path $markdownPath -Value $newContent -Encoding UTF8
                                    Write-Host "    Enhanced successfully" -ForegroundColor Green
                                }
                            }
                            else {
                                Write-Host "    Skipped (empty or too short response)" -ForegroundColor Yellow
                            }
                        }
                    }
                    catch {
                        Write-Host "    Failed to enhance: $_" -ForegroundColor Yellow
                    }
                }
            }
            Write-Host "Description enhancement complete" -ForegroundColor Green
        }
    }

    # Update main README.md with panel documentation links
    $mainReadmePath = Join-Path $RepoRoot "README.md"
    if (Test-Path $mainReadmePath) {
        Write-Host "`nUpdating main README.md with panel documentation links..." -ForegroundColor Cyan

        $readmeContent = Get-Content $mainReadmePath -Raw

        # Generate the new Available Panels section
        $newPanelSection = @"
## Available Panels

> **Full Documentation:** See [docs/panels/README.md](docs/panels/README.md) for detailed panel documentation with screenshots.

"@

        foreach ($category in $panelsByCategory) {
            $newPanelSection += "`n### $($category.Name)`n`n"
            $newPanelSection += "| Panel | Description |`n"
            $newPanelSection += "|-------|-------------|`n"

            foreach ($panel in ($category.Group | Sort-Object DisplayName)) {
                $displayId = if ($panel.PrefixPattern) { $panel.PrefixPattern } else { $panel.TypeId }
                $categoryFolder = Get-CategoryFolder $panel.Category
                $docLink = "docs/panels/$categoryFolder/$($panel.TypeId).md"
                $newPanelSection += "| [``$displayId``]($docLink) | $($panel.Description) |`n"
            }
        }

        # Find and replace the Available Panels section
        # Match from "## Available Panels" to the next "## " heading
        $pattern = '(?s)## Available Panels.*?(?=\n## [A-Z])'

        if ($readmeContent -match $pattern) {
            $readmeContent = $readmeContent -replace $pattern, $newPanelSection
            Set-Content -Path $mainReadmePath -Value $readmeContent -NoNewline -Encoding UTF8
            Write-Host "Updated README.md panel section" -ForegroundColor Green
        }
        else {
            Write-Host "Could not find '## Available Panels' section in README.md" -ForegroundColor Yellow
        }
    }

    # Summary
    $finalProcessed = if ($Parallel) { $totalPanels } else { $processedCount }
    $finalErrors = if ($Parallel) { $errorCount } else { $errorCount }

    Write-Host "`n=== Generation Complete ===" -ForegroundColor Green
    Write-Host "Processed: $finalProcessed panels" -ForegroundColor Cyan
    Write-Host "Errors: $finalErrors" -ForegroundColor $(if ($finalErrors -gt 0) { "Red" } else { "Green" })
    Write-Host "Output: $DocsRoot" -ForegroundColor Cyan
    Write-Host "Tip: Use -Parallel for faster generation" -ForegroundColor DarkGray
}
finally {
    Pop-Location
}
