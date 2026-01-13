---
name: ensure-panelvisualappeal
description: |
  Reviews panel visual appeal and layout quality for LCDPossible LCD displays.
  Use when: validating panel layout, checking visual quality, reviewing panel design,
  ensuring style compliance, auditing panel appearance, fixing layout issues.
  Analyzes HTML/CSS and screenshots against visual appeal rules.
allowed-tools: Read, Glob, Grep, Bash, Task
---

# Panel Visual Appeal Validator

This skill validates that LCDPossible panels meet visual design standards for LCD signage displays (1280x480, viewed from 3-6 feet).

## Prerequisites

**IMPORTANT:** Before running this validation, read the visual appeal rules:
```
Read .claude/rules/panel-visual-appeal.md
```

This file contains the 16 visual appeal rules that panels must comply with.

## Usage

```
/ensure-panelvisualappeal <panel-id>
/ensure-panelvisualappeal <screenshot-path>
/ensure-panelvisualappeal <panel-id> <screenshot-path>
```

## Validation Process

### Step 1: Load Rules

Read `.claude/rules/panel-visual-appeal.md` to get the current visual appeal standards.

### Step 2: Gather Assets

If panel ID provided:
1. Render the panel:
   ```bash
   dotnet run --project src/LCDPossible/LCDPossible.csproj -c Release -- test <panel-id> -w 2 --debug
   ```
2. Read the generated HTML from temp: `%TEMP%\<panel-id>-debug.html`
3. Load the screenshot from: `~\<panel-id>-cyberpunk.jpg`

If only screenshot provided:
1. Analyze the screenshot visually
2. Note: HTML/CSS analysis will be limited

### Step 3: Visual Analysis

Review the screenshot for each of the 16 rules. Score each as:
- **PASS** - Meets requirements
- **WARN** - Minor issues, could be improved
- **FAIL** - Violates the rule, needs fixing

### Step 4: HTML/CSS Analysis

If HTML available, inspect:
- CSS classes used (Tailwind/DaisyUI)
- Grid column/row spans
- Padding and margin values
- Font sizes and weights
- Color values (check for hardcoded vs theme)

### Step 5: Generate Report

Output a structured report with findings and recommendations.

---

## Report Template

```markdown
# Panel Visual Appeal Report

**Panel ID:** <panel-id>
**Theme:** <theme-name>
**Resolution:** 1280x480
**Date:** <date>

## Summary

| Rule | Status | Notes |
|------|--------|-------|
| 1. Container Filling | PASS/WARN/FAIL | |
| 2. Element Centering | PASS/WARN/FAIL | |
| 3. Color Contrast | PASS/WARN/FAIL | |
| 4. Container Boundaries | PASS/WARN/FAIL | |
| 5. Panel Utilization | PASS/WARN/FAIL | |
| 6. Typography Hierarchy | PASS/WARN/FAIL | |
| 7. Consistent Spacing | PASS/WARN/FAIL | |
| 8. Grid Alignment | PASS/WARN/FAIL | |
| 9. Truncation Handling | PASS/WARN/FAIL | |
| 10. Every Value Needs a Label | PASS/WARN/FAIL | |
| 11. Avoid Orphaned Empty Space | PASS/WARN/FAIL | |
| 12. Empty/No-Data States | PASS/WARN/FAIL | |
| 13. Visual Balance | PASS/WARN/FAIL | |
| 14. Color Semantics | PASS/WARN/FAIL | |
| 15. Icon/Symbol Proportionality | PASS/WARN/FAIL | |
| 16. Theme Compliance | PASS/WARN/FAIL | |

**Overall Score:** X/16 PASS, Y WARN, Z FAIL

## Detailed Findings

### Issues Found

1. **[FAIL] Rule X: <Rule Name>**
   - Problem: <description>
   - Location: <widget/element>
   - Recommendation: <fix suggestion>

2. **[WARN] Rule Y: <Rule Name>**
   - Problem: <description>
   - Recommendation: <improvement suggestion>

### Positive Observations

- <what's working well>

## Recommended Fixes

1. <specific code/CSS change>
2. <specific code/CSS change>
```

---

## Common Fixes

### Fix: Widgets not filling vertical space
```csharp
// Before: Two 1-row widgets stacked (wastes space, forces tiny text)
yield return new WidgetDefinition("lcd-stat-card", 3, 1, new {  // Only 1 row!
    title = "GPU", value = "0%"
});
yield return new WidgetDefinition("lcd-stat-card", 3, 1, new {  // Only 1 row!
    title = "VRAM", value = "23%"
});

// After: Two 2-row widgets that fill the panel height
yield return new WidgetDefinition("lcd-stat-card", 3, 2, new {  // 2 rows each
    title = "GPU", value = "0%", size = "large"
});
yield return new WidgetDefinition("lcd-stat-card", 3, 2, new {  // 2 rows each
    title = "VRAM", value = "23%", size = "large"
});
```

### Fix: Text too small in container
```csharp
// Change from small text
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new {
    title = "CPU",
    value = cpuName,
    size = "small"  // Too small!
});

// To larger text
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new {
    title = "CPU",
    value = cpuName,
    size = "large"  // Better for the container size
});
```

### Fix: Chart not filling container
```html
<!-- Before: Fixed size -->
<svg width="200" height="100">

<!-- After: Fill container -->
<svg viewBox="0 0 100 60" class="w-full h-full" preserveAspectRatio="none">
```

### Fix: Content not centered
```html
<!-- Before: Default alignment (content at top-left) -->
<div class="card">
  <span class="label">GPU</span>
  <span class="value">0%</span>
</div>

<!-- After: Centered content (equal whitespace on all sides) -->
<div class="card flex flex-col items-center justify-center h-full">
  <span class="label">GPU</span>
  <span class="value">0%</span>
</div>
```

**Key classes for centering:**
- `flex flex-col` - Stack label and value vertically
- `items-center` - Center horizontally
- `justify-center` - Center vertically
- `h-full` - Ensure container takes full height to center within

### Fix: Hardcoded colors
```html
<!-- Before: Hardcoded -->
<div style="color: #00d4ff">

<!-- After: Theme variable -->
<div class="text-primary">
```

### Fix: No empty state
```csharp
if (data.items.Count == 0)
{
    yield return WidgetDefinition.FullWidth("empty-state", 4, new {
        message = "No data available"
    });
    yield break;
}
```

### Fix: Missing labels on values
```csharp
// Before: Donut with no context
yield return new WidgetDefinition("lcd-donut", 4, 2, new {
    value = data.cpuUsage,
    max = 100
    // Missing label!
});

// After: Donut with clear label
yield return new WidgetDefinition("lcd-donut", 4, 2, new {
    value = data.cpuUsage,
    max = 100,
    label = "CPU"  // Now users know what 47% means
});
```

### Fix: Orphaned empty space
```csharp
// Before: Widgets clustered on left, right side empty
yield return new WidgetDefinition("lcd-stat-card", 2, 2, new { ... });  // cols 1-2
yield return new WidgetDefinition("lcd-stat-card", 2, 2, new { ... });  // cols 3-4
yield return new WidgetDefinition("lcd-stat-card", 2, 2, new { ... });  // cols 5-6
// Cols 7-12 empty!

// After: Widgets expanded to fill panel
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { ... });  // cols 1-4
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { ... });  // cols 5-8
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { ... });  // cols 9-12
```

### Fix: Visual monotony (single row of identical widgets)
```csharp
// Before: 6 identical 2×4 stat-cards in a single row with tiny text
yield return new WidgetDefinition("lcd-stat-card", 2, 4, new { title = "CPU", value = "13%", size = "small" });
yield return new WidgetDefinition("lcd-stat-card", 2, 4, new { title = "TEMP", value = "-", size = "small" });
yield return new WidgetDefinition("lcd-stat-card", 2, 4, new { title = "RAM", value = "49%", size = "small" });
yield return new WidgetDefinition("lcd-stat-card", 2, 4, new { title = "USED", value = "31GB", size = "small" });
yield return new WidgetDefinition("lcd-stat-card", 2, 4, new { title = "GPU", value = "1%", size = "small" });
yield return new WidgetDefinition("lcd-stat-card", 2, 4, new { title = "TEMP", value = "47°C", size = "small" });

// After: 2 rows × 3 columns with 4×2 widgets and proper text scaling
// Row 1: CPU metrics
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { title = "CPU", value = "13%", size = "xlarge" });
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { title = "CPU TEMP", value = "--", size = "xlarge" });
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { title = "RAM", value = "49%", size = "xlarge" });
// Row 2: GPU and RAM metrics
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { title = "RAM USED", value = "31.2 GB", size = "xlarge" });
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { title = "GPU", value = "1%", size = "xlarge" });
yield return new WidgetDefinition("lcd-stat-card", 4, 2, new { title = "GPU TEMP", value = "47°C", size = "xlarge" });
```

---

## Quick Checklist

Before approving a panel, verify:

- [ ] All widgets fill their grid cells appropriately
- [ ] Gauges/donuts/charts scale to fill container (~90% of space, not fixed small size)
- [ ] Text is large enough to read from 3-6 feet
- [ ] Colors provide sufficient contrast
- [ ] No content is cut off or overflowing
- [ ] Charts/sparklines stay within bounds - trace edges to check data lines don't cross container borders
- [ ] The full panel area is utilized
- [ ] Typography has clear hierarchy (values > labels > secondary)
- [ ] Spacing is consistent throughout
- [ ] Long text is truncated gracefully
- [ ] Empty/no-data states are handled (panel-level AND widget-level - no labels with blank values)
- [ ] All colors use theme variables
- [ ] Widgets expand to fill available vertical space (no 1-row widgets when 2+ rows would fit)
- [ ] Text scales appropriately with container size (bigger containers = bigger text)
- [ ] Content is centered in stat-card widgets (equal whitespace on all sides)
- [ ] No "tiny island in vast ocean" - if you could fit another copy of the content in the empty space, it's too small
- [ ] Info-list widgets (label/value pairs) have text scaled to container - no tiny text in large widgets
- [ ] Value Prominence: values are 3-5× larger than their labels in stat cards (value should dominate)
- [ ] Every displayed value has a visible label - no unlabeled percentages, temperatures, or numbers
- [ ] No orphaned empty space - no 3+ column × 2+ row areas of unused space (especially bottom-right)
- [ ] No visual monotony - avoid all-identical widgets; vary sizes, types, or use 2D grid layouts instead of single rows
- [ ] Panel name suffix matches content: "-text" panels have no graphics, "-graphic" panels can have gauges/charts
- [ ] Similar content types have consistent widget sizes (e.g., CPU and GPU gauges same size)
- [ ] No redundant data - same value not shown multiple times in different widgets
