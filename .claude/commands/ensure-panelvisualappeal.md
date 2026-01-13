---
description: Validate and review panel visual appeal against layout and style requirements
allowed-tools: Read, Glob, Grep, Bash
---

# Ensure Panel Visual Appeal Command

Validate panel visual appeal for: $ARGUMENTS

## Workflow

### Step 1: Load Visual Appeal Rules

Read the visual appeal standards:
```
.claude/rules/panel-visual-appeal.md
```

### Step 2: Parse Arguments

Arguments can be:
- `<panel-id>` - Render and analyze the panel
- `<screenshot-path>` - Analyze existing screenshot only
- `<panel-id> <screenshot-path>` - Use provided screenshot for panel

### Step 3: Gather Assets

If panel ID provided:

1. **Build if needed:**
   ```bash
   dotnet build src/LCDPossible.sln -c Release --verbosity quiet
   ```

2. **Render the panel:**
   ```bash
   dotnet run --project src/LCDPossible/LCDPossible.csproj -c Release -- test <panel-id> -w 2 --debug
   ```

3. **Locate generated files:**
   - Screenshot: `~/[panel-id]-cyberpunk.jpg`
   - Debug HTML: `%TEMP%/[panel-id]-debug.html`

### Step 4: Visual Analysis

View the screenshot using the Read tool and evaluate against all 14 rules:

1. Container Filling
2. Element Centering
3. Color Contrast
4. Container Boundaries
5. Panel Utilization
6. Typography Hierarchy
7. Consistent Spacing
8. Grid Alignment
9. Truncation Handling
10. Empty/No-Data States
11. Visual Balance
12. Color Semantics
13. Icon/Symbol Proportionality
14. Theme Compliance

### Step 5: HTML/CSS Analysis

If HTML available, search for:
- Hardcoded colors (grep for `#[0-9a-fA-F]`, `rgb(`, `style=.*color`)
- Grid spans (check col-span values match container needs)
- Font sizes (ensure text-2xl+ for values)
- Centering classes (flex, items-center, justify-center)

### Step 6: Generate Report

Output the report in this format:

```markdown
# Panel Visual Appeal Report

**Panel ID:** [panel-id]
**Theme:** cyberpunk
**Resolution:** 1280x480
**Date:** [date]

## Summary

| Rule | Status | Notes |
|------|--------|-------|
| 1. Container Filling | PASS/WARN/FAIL | |
| ... | ... | ... |

**Overall Score:** X/14 PASS, Y WARN, Z FAIL

## Detailed Findings

[List issues and recommendations]

## Recommended Fixes

[Specific code changes if any issues found]
```

### Step 7: Offer to Fix

If issues found, offer to make the fixes:
- For code issues: Edit the panel source file
- For CSS issues: Edit the WidgetPanel rendering methods

## Output

Report summarizing:
- Pass/Warn/Fail status for all 14 rules
- Specific issues found with locations
- Recommended fixes with code examples
