---
name: scriban-templates
description: |
  Scriban template syntax and C# string escaping rules for LCDPossible panels.
  Use when: creating HtmlPanel or WidgetPanel templates, debugging template compilation errors,
  working with Scriban syntax in C# interpolated strings, adding template context variables,
  fixing brace escaping issues, or creating new web component layouts.
  Keywords: Scriban, template, escaping, braces, TemplateContext, HtmlPanel, WidgetPanel, GridTemplate
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
---

# Scriban Templates in LCDPossible

This skill covers Scriban template usage in C# interpolated strings for the LCDPossible panel system.

## CRITICAL RULES - Read First

### Rule 1: Count Your Braces

**This is the #1 source of errors.** Before writing ANY template:

| What You Want | Brace Count in C# | Example |
|---------------|-------------------|---------|
| C# interpolation | 1 | `{myVar}` |
| CSS literal brace | 2 | `{{` and `}}` |
| Scriban expression | 4 | `{{{{` and `}}}}` |

### Rule 2: Scriban is NOT Liquid/Jinja

These features DO NOT exist in Scriban:
- `| raw` filter - Scriban doesn't HTML-encode by default
- `| escape` filter - Different syntax
- `{% %}` blocks - Scriban uses `{{ }}`

### Rule 3: The Golden Pattern

```csharp
private string MyTemplate => $@"
<style>
    .class {{              /* CSS: 2 braces for literal {{ */
        padding: {Padding}px;  /* C#: 1 brace for interpolation */
    }}
</style>
<body>
    {{{{ for x in data }}}}    /* Scriban: 4 braces for {{ */
    <div>{{{{ x.name }}}}</div>
    {{{{ end }}}}
</body>";
```

## Pre-Flight Checklist

Before writing a template, verify:

- [ ] **CSS blocks use 2 braces**: `body {{` not `body {{{{`
- [ ] **Scriban uses 4 braces**: `{{{{ for }}}}` not `{{ for }}`
- [ ] **C# vars use 1 brace**: `{PanelPadding}` not `{{PanelPadding}}`
- [ ] **No `| raw` filter**: Just `{{{{ widget.html }}}}` not `{{{{ widget.html | raw }}}}`
- [ ] **Closing braces match**: Count opening and closing braces

## DO vs DON'T Examples

### CSS Blocks

```csharp
// ✅ DO - CSS needs literal { in output, so use 2 braces
$@"body {{
    margin: 0;
}}"

// ❌ DON'T - 4 braces produces {{ which Scriban tries to parse
$@"body {{{{
    margin: 0;
}}}}"
```

### Scriban Expressions

```csharp
// ✅ DO - Scriban expressions need {{ }} in output, so use 4 braces
$@"{{{{ for item in data.items }}}}"

// ❌ DON'T - 2 braces produces { which is just a literal
$@"{{ for item in data.items }}"

// ❌ DON'T - 6 braces produces {{{ which is invalid
$@"{{{{{{ for item in data.items }}}}}}"
```

### Output Without Encoding

```csharp
// ✅ DO - Scriban outputs raw strings by default
$@"{{{{ widget.html }}}}"

// ❌ DON'T - There is no raw filter in Scriban
$@"{{{{ widget.html | raw }}}}"
```

### Mixed C# and Scriban

```csharp
// ✅ DO - C# interpolation with 1 brace, Scriban with 4
$@"<style>
    .grid {{
        gap: {GridGap}px;          /* C# interpolation */
        columns: {{{{ data.cols }}}};  /* Scriban expression */
    }}
</style>"

// ❌ DON'T - Mixing up brace counts
$@"<style>
    .grid {{{{
        gap: {{GridGap}}px;        /* WRONG: GridGap is C#, not Scriban */
    }}}}
</style>"
```

## Decision Tree: How Many Braces?

```
What are you outputting?
│
├─ CSS/JavaScript literal brace { or }
│   └─ Use 2 braces: {{ or }}
│
├─ Scriban expression {{ something }}
│   └─ Use 4 braces: {{{{ something }}}}
│
├─ C# variable from the class
│   └─ Use 1 brace: {MyProperty}
│
└─ Literal text with no braces
    └─ Just write it: <div>text</div>
```

## Quick Reference: Brace Escaping

When using Scriban templates inside C# interpolated strings (`$"..."`):

| In C# String | Output | Use For |
|--------------|--------|---------|
| `{variable}` | interpolated value | C# variable interpolation |
| `{{` | `{` | CSS blocks, literal braces |
| `}}` | `}` | CSS blocks, literal braces |
| `{{{{` | `{{` | Scriban expression start |
| `}}}}` | `}}` | Scriban expression end |

### Common Patterns

```csharp
// CSS block (need single braces in output)
$@"body {{
    margin: 0;
    padding: {PanelPadding}px;  // C# interpolation
}}"
// Output: body { margin: 0; padding: 8px; }

// Scriban expression (need double braces in output)
$@"{{{{ for item in data.items }}}}"
// Output: {{ for item in data.items }}

// Scriban variable
$@"<div>{{{{ item.name }}}}</div>"
// Output: <div>{{ item.name }}</div>
```

## Template Context Variables

When using `HtmlPanel` or `WidgetPanel`, these variables are automatically available:

| Variable | Type | Description |
|----------|------|-------------|
| `data` | object | The data model from `GetDataModelAsync()` |
| `target_width` | int | Target render width in pixels |
| `target_height` | int | Target render height in pixels |
| `assets_path` | string | File URI to html_assets folder |
| `assets` | string | Alias for `assets_path` |
| `colors` | object | Color scheme values (see below) |
| `colors_css` | string | Pre-rendered CSS variables |

### Color Scheme Variables

Access via `colors.property_name`:

```
colors.background           colors.text_primary
colors.background_secondary colors.text_secondary
colors.bar_background       colors.text_muted
colors.bar_border           colors.accent
colors.usage_low            colors.usage_medium
colors.usage_high           colors.usage_critical
```

### WidgetPanel Data Model

For `WidgetPanel`, the `data` object contains:

```javascript
data = {
  panel_data: { /* from GetPanelDataAsync() */ },
  items: [ /* from GetItemsAsync() */ ],
  widgets: [
    {
      component: "lcd-stat-card",
      col_span: 6,
      row_span: 2,
      props: { /* widget properties */ },
      props_json: "{ ... }",
      html: "<lcd-stat-card props='...'></lcd-stat-card>"
    }
  ]
}
```

## Scriban Syntax Reference

### Loops

```scriban
{{ for item in data.items }}
  <div>{{ item.name }}</div>
{{ end }}

{{ for widget in data.widgets }}
  <div style="grid-column: span {{ widget.col_span }}">
    {{ widget.html }}
  </div>
{{ end }}
```

### Conditionals

```scriban
{{ if item.value > 90 }}
  <span class="critical">{{ item.value }}</span>
{{ else if item.value > 70 }}
  <span class="warning">{{ item.value }}</span>
{{ else }}
  <span class="normal">{{ item.value }}</span>
{{ end }}
```

### Variable Access

```scriban
{{ data.panel_data.cpu_name }}
{{ data.items[0].value }}
{{ colors.accent }}
```

### Important: No `raw` Filter

Scriban does NOT have a `| raw` filter (that's Liquid/Jinja). Scriban outputs strings without HTML encoding by default, so just use:

```scriban
{{ widget.html }}
```

NOT:
```scriban
{{ widget.html | raw }}  ❌ ERROR: function 'raw' was not found
```

## Common Errors and Fixes

### Error: "Expecting an expression to the right of the operator"

**Cause**: CSS braces being interpreted as Scriban expressions.

**Wrong**:
```csharp
$@"body {{{{
    margin: 0;
}}}}"
```

**Correct**:
```csharp
$@"body {{
    margin: 0;
}}"
```

### Error: "function 'raw' was not found"

**Cause**: Using Liquid/Jinja syntax instead of Scriban.

**Wrong**: `{{ widget.html | raw }}`
**Correct**: `{{ widget.html }}`

### Error: "Unexpected token"

**Cause**: Missing closing braces or incorrect escaping.

Check that:
1. Every `{{{{` has a matching `}}}}`
2. CSS blocks use `{{` and `}}` (2 braces each)
3. Scriban expressions use `{{{{` and `}}}}` (4 braces each)

## Complete Template Example

```csharp
private string GridTemplate => $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width={{{{ target_width }}}}, height={{{{ target_height }}}}"">
    <link rel=""stylesheet"" href=""file://{{{{ assets_path }}}}/css/theme.css"">
    <script src=""file://{{{{ assets_path }}}}/js/components.js""></script>
    <style>
        {{{{ colors_css }}}}

        body {{
            margin: 0;
            padding: {PanelPadding}px;
            background: var(--color-background);
        }}

        .grid {{
            display: grid;
            grid-template-columns: repeat(12, 1fr);
            gap: {GridGap}px;
        }}
    </style>
</head>
<body>
    <div class=""grid"">
        {{{{ for widget in data.widgets }}}}
        <div style=""grid-column: span {{{{ widget.col_span }}}};"">
            {{{{ widget.html }}}}
        </div>
        {{{{ end }}}}
    </div>
</body>
</html>";
```

## Adding Context Variables

To add new variables to the template context, edit `HtmlPanel.RenderTemplate()`:

```csharp
protected string RenderTemplate(object dataModel)
{
    var context = new TemplateContext();
    var scriptObject = new ScriptObject();

    // Existing variables
    scriptObject["data"] = dataModel;
    scriptObject["target_width"] = TargetWidth;
    scriptObject["target_height"] = TargetHeight;
    scriptObject["assets_path"] = AssetsUri;
    scriptObject["colors_css"] = GenerateColorsCss();

    // Add your new variable
    scriptObject["my_variable"] = "value";

    context.PushGlobal(scriptObject);
    return _compiledTemplate.Render(context);
}
```

## Testing Templates

Use the test verb to verify templates render correctly:

```bash
# Render and check output
./start-app.ps1 test cpu-widget --debug

# Check for errors in output
# [DEBUG] Written: C:\Users\...\cpu-widget.jpg (10211 bytes)  ✓
# Error: Template compilation failed: ...  ✗
```

## Web Components Available

These components are defined in `html_assets/js/components.js`:

| Component | Props | Description |
|-----------|-------|-------------|
| `<lcd-usage-bar>` | value, max, label, color, orientation | Progress bar |
| `<lcd-stat-card>` | title, value, unit, subtitle, status, size | Value display |
| `<lcd-temp-gauge>` | value, max, label | Temperature gauge |
| `<lcd-info-list>` | items (JSON array) | Label/value pairs |
| `<lcd-sparkline>` | values (JSON array), color, label | Time-series chart |
| `<lcd-status-dot>` | status, label | Colored indicator |
| `<lcd-donut>` | value, max, label, color | Circular percentage |

### Component Usage

```csharp
// In DefineWidgets()
yield return new WidgetDefinition(
    Component: "lcd-stat-card",
    ColSpan: 6,
    RowSpan: 1,
    Props: new {
        title = "CPU",
        value = metrics.Usage.ToString("F1"),
        unit = "%",
        status = metrics.Usage > 90 ? "critical" : "success"
    }
);
```

## Files Reference

| File | Purpose |
|------|---------|
| `src/LCDPossible.Sdk/HtmlPanel.cs` | Base class, template compilation |
| `src/LCDPossible.Sdk/WidgetPanel.cs` | Grid layout, widget definitions |
| `src/LCDPossible.Sdk/html_assets/css/theme.css` | CSS variables, responsive styles |
| `src/LCDPossible.Sdk/html_assets/js/components.js` | Web component definitions |
