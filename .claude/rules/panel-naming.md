# Panel Naming Convention

## Panel ID Guidelines

Panel IDs (`PanelId` property) should describe the panel's **content**, not its implementation.

### Do NOT use generic terms:
- `widget` (e.g., ~~cpu-widget~~, ~~network-widget~~)
- `panel` (e.g., ~~cpu-panel~~, ~~info-panel~~)
- `card` (e.g., ~~stat-card~~, ~~data-card~~)
- `display` (e.g., ~~cpu-display~~)
- `view` (e.g., ~~cpu-view~~)

### DO use content-descriptive names:

| Good | Bad |
|------|-----|
| `cpu-info` | `cpu-widget` |
| `cpu-usage` | `cpu-panel` |
| `cpu-usage-graphic` | `cpu-usage-widget` |
| `network-status` | `network-widget` |
| `ram-usage-text` | `ram-panel` |
| `system-thermal` | `thermal-widget` |

### Naming Pattern

```
{subject}-{content-type}[-{variant}]
```

- **subject**: What the panel shows (cpu, gpu, ram, network, system, proxmox)
- **content-type**: Type of information (info, usage, status, thermal, summary)
- **variant** (optional): Presentation style (text, graphic, list)

### Variant Suffix Must Match Content

The variant suffix indicates what visual elements the panel contains. **Content must match the suffix:**

| Suffix | Allowed Content | NOT Allowed |
|--------|-----------------|-------------|
| `-text` | Text only (stat cards, labels, values) | Gauges, charts, progress arcs, sparklines |
| `-graphic` | Text + graphical elements (gauges, charts, bars, donuts) | N/A - anything allowed |
| `-info` | General information, typically text-focused | N/A - flexible |

**Violation example:** `cpu-usage-text` panel containing a gauge arc - the "-text" suffix promises text-only but delivers graphics.

**Why this matters:**
- Users selecting "-text" variants expect minimal visual complexity
- Consistent naming helps users predict panel appearance
- Enables filtering panels by visual style

### Examples

```csharp
// Good - describes content
public override string PanelId => "cpu-info";
public override string PanelId => "cpu-usage-graphic";
public override string PanelId => "network-status";
public override string PanelId => "system-thermal-graphic";

// Bad - uses generic terms
public override string PanelId => "cpu-widget";       // Don't use 'widget'
public override string PanelId => "network-panel";    // Don't use 'panel'
```

### Rationale

1. **User-facing**: Panel IDs appear in CLI commands and config files
2. **Self-documenting**: `cpu-usage-graphic` tells you what to expect
3. **Consistent**: Matches existing panels (cpu-info, ram-usage-text, etc.)
4. **Searchable**: Users can find panels by content type
