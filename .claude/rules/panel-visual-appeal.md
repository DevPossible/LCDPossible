# Panel Visual Appeal Standards

These rules define the visual quality standards for LCDPossible panels displayed on 1280x480 LCD screens viewed from 3-6 feet.

**IMPORTANT:** Apply these rules when creating or reviewing any WidgetPanel.

## The 16 Visual Appeal Rules

### Rule 1: Container Filling
Elements should fill their containers appropriately.

- Graphs/charts: 80-100% of cell area
- Text: Sized to fill available space (not tiny in large containers)
- No "tiny island in vast ocean" syndrome
- Allow 8-16px padding for breathing room

**Core Principle:** When a container is bigger than 1×1, text size should scale up. Larger containers = larger fonts.

**Gauge/Donut/Chart Scaling:** Graphical elements must also scale to fill their container:
- A gauge in a 4-row container should use ~90% of the vertical space
- Don't use fixed-size graphics in variable-size containers
- The graphic should be as large as possible while maintaining padding

**Bad:** Temperature gauge in 4-row container using only 50% of the height
**Good:** Temperature gauge expanded to fill the container, with minimal padding

**Bad:** A 4-column widget with text-sm creating vast empty space
**Good:** A 4-column widget with text-2xl or text-3xl filling the area

**Info-List Widget Rule:** Info lists (label/value pairs like "LOAD: 10%", "POWER: 0.0 W") must also scale text to container:
- A 6-col × 2-row info list with only 2-3 items should use text-xl labels and text-2xl values
- Don't use text-base for a list that occupies a large widget area
- If the list items cluster in one corner with vast empty space, text is too small

**Bad:** STATUS panel (6×2) with "LOAD 10%" in text-base - tiny text, huge empty space
**Good:** STATUS panel (6×2) with "LOAD 10%" in text-xl/text-2xl filling the widget

**Title + Qualifier Layout:** When a widget has a title and a short qualifier/description:
- Consider placing them on the same line if horizontal space permits
- Don't waste a full line for a short qualifier like "VM on node1"
- Format: "WEB-SERVER · VM on node1" or "WEB-SERVER (VM on node1)" on one line

**Bad:** Title on line 1, short qualifier on line 2, empty space to the right of both
**Good:** Title and qualifier on the same line, using horizontal space efficiently

**Row Span Rule:** When widgets are stacked vertically in a column and don't fill all 4 rows, expand them:
- 2 widgets stacked → 2 rows each (not 1 row each with empty space)
- 3 widgets stacked → Consider 2+1+1 or expanding key widgets
- Single widget in column → Should span 3-4 rows if content permits

**Bad:** Two 1-row stat cards stacked vertically, leaving rows empty or underutilized
**Good:** Two 2-row stat cards that each use half the panel height with properly scaled text

### Rule 2: Element Centering
Center elements unless there's a UI reason not to.

- Center content horizontally and vertically within cells
- Exceptions: left-aligned labels with right-aligned values in **list/table layouts only**

**Visual Check:** Equal whitespace above and below content, equal whitespace left and right.

**Stat Card Rule:** For widgets displaying a label + single value (like "GPU: 0%"):
- Both label and value should be vertically centered as a group
- Content should be horizontally centered
- There should NOT be more whitespace below than above

**Bad:** Label at top-left of card, value below it, large empty space at bottom/right
**Good:** Label+value group centered in the middle of the card with equal space on all sides

**Classes:** `flex flex-col items-center justify-center`, `text-center`

**Screenshot Check:** If you can fit another copy of the content in the empty space below or beside it, it's not centered.

### Rule 3: Color Contrast
Ensure sufficient contrast for LCD signage.

- Primary values: High contrast (WCAG AA: 4.5:1 minimum)
- Labels: Slightly lower but still readable
- Critical for distance viewing

**Good:** `text-primary` on `bg-base-200` (cyan on dark blue)
**Bad:** `text-base-content/30` on `bg-base-200` (too dim)

### Rule 4: Container Boundaries
Content must stay within widget bounds.

- No clipping at edges
- Standard padding maintained
- Handle overflow with ellipsis, wrap, or scroll

**Applies to all content types:**
- Text: Use `truncate` or `break-words`
- Images: Use `object-fit: contain` or `cover`
- **Charts/Sparklines:** SVG paths, canvas drawings, and data lines must not extend beyond container
- Gauges/Progress bars: Arc endpoints must stay within bounds

**Chart overflow check:** Look at sparklines, line charts, and graphs - data points at min/max values often overflow if the chart doesn't account for stroke width or padding.

**Common causes of chart overflow:**
- SVG viewBox not accounting for stroke width
- Data scaled to exact container size without margin for line thickness
- Missing `overflow-hidden` on chart container

**Classes:** `overflow-hidden`, `truncate`, `break-words`

**Screenshot check:** Trace the edges of every chart/sparkline - if any line touches or crosses the widget border, it's overflowing.

### Rule 5: Panel Utilization
Use the full 1280x480 area effectively.

- No large empty regions
- Widget grid should fill the panel
- Balance content distribution

**Check:** All 4 rows used, no empty columns

**Vertical Fill Principle:** Widgets should expand vertically to eliminate wasted space:
- If a column has widgets totaling less than 4 rows, expand them to fill
- Prefer fewer, larger widgets over many cramped small widgets
- A widget with only a label+value should be at least 2 rows to allow proper text scaling

**Bad:** Two 1-row widgets in a column (total 2 rows) with implicit empty space
**Good:** Two 2-row widgets in a column (total 4 rows) filling the panel height

### Rule 6: Typography Hierarchy
Clear visual hierarchy through size and weight.

| Element | Size | Weight | Example |
|---------|------|--------|---------|
| Primary values | text-3xl to text-5xl | font-bold | "64 GB", "49%" |
| Labels/titles | text-lg to text-xl | font-normal/uppercase | "MEMORY", "USAGE" |
| Secondary info | text-sm to text-base | font-normal | "of 64GB", descriptions |

**Minimum for LCD viewing:** Labels ≥16px (text-base), Values ≥24px (text-2xl)

**Container-Based Text Scaling:** Text size must scale with container size.

| Widget Size (cols × rows) | Label Size | Value Size |
|---------------------------|------------|------------|
| 2-3 cols × 1 row | text-sm | text-lg to text-xl |
| 2-3 cols × 2 rows | text-lg to text-xl | text-3xl to text-4xl |
| 4-6 cols × 1 row | text-base | text-xl to text-2xl |
| 4-6 cols × 2 rows | text-xl | text-4xl to text-5xl |
| Any × 3-4 rows | text-xl to text-2xl | text-5xl to text-6xl |

**The scaling principle:** Doubling row height should roughly double text size.

**Bad:** A 3-col × 2-row widget with text-sm label and text-lg value (too small for container)
**Good:** A 3-col × 2-row widget with text-xl label and text-4xl value (fills the space)

### Value Prominence Principle

When displaying a label with a single featured value (stat cards, KPIs, gauges), the **value should dominate** the visual hierarchy.

**When to apply (hero values):**
- Stat cards showing a single metric (CPU: 47%)
- Primary KPIs meant for at-a-glance monitoring
- Featured metrics in dashboards
- Any value the user needs to read from 3-6 feet away

**Sizing ratio:** Value text should be **3-5× larger** than its label.

| Label Size | Value Size | Ratio | Use Case |
|------------|------------|-------|----------|
| text-sm | text-2xl | 3× | Compact stat cards |
| text-base | text-3xl | 3× | Standard stat cards |
| text-lg | text-4xl | 3.5× | Medium emphasis |
| text-xl | text-5xl | 4× | High emphasis |
| text-xl | text-6xl | 5× | Hero/primary metric |

**When NOT to apply (supporting data):**
- Info lists with multiple label/value pairs (use 1.5-2× ratio instead)
- Tables and dense data grids
- Secondary/contextual information
- Inline values within sentences

**Visual test:** If someone glances at the panel from across the room, they should read the *value* first, then notice the label explains what it is.

**Bad:**
```
CPU        ← text-lg (large)
47%        ← text-xl (slightly larger) - label competes with value
```

**Good:**
```
CPU        ← text-base (small, subdued)
47%        ← text-5xl (huge, eye-catching) - value dominates
```

### Rule 7: Consistent Spacing
Uniform spacing throughout.

- Grid gap: `gap-4` (16px) consistently
- Widget padding: `p-4` (16px) internally
- No uneven margins

### Rule 8: Grid Alignment
All elements align to the 12-column grid.

- Widgets snap to column boundaries
- Consistent row heights
- No "floating" misaligned elements

**WidgetPanel uses:** 12 columns × 4 rows

### Rule 9: Truncation Handling
Handle long text gracefully.

- Use ellipsis for truncated single-line text: `truncate` class
- Word-wrap for multi-line: `break-words` class
- Never cut mid-character

```csharp
// Helper for long names
private static string TruncateName(string name, int maxLength)
{
    if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
        return name;
    return name[..(maxLength - 3)] + "...";
}
```

### Rule 10: Every Value Needs a Label
All displayed values must have a visible label or title providing context.

**Applies to:**
- Stat cards showing percentages, temperatures, or counts
- Donut/gauge charts with numeric values
- Any standalone numeric display

**Why:** A value without context is meaningless. "47" could be temperature, percentage, count, or anything else. Users should never have to guess what a number represents.

**Bad Examples:**
- A donut chart showing "13%" with no label - what is 13% of?
- A gauge showing "47°C" but no indication it's CPU/GPU/system temperature
- A tiny "2%" with only a colored dot indicator but no text label

**Good Examples:**
- Donut chart with "CPU" label below the percentage
- Temperature gauge with "GPU TEMP" title above
- Stat card with "RAM" title and "47%" value

**Exception:** If multiple values in a row share a single heading (like a labeled group), individual labels may be omitted if the group context is clear.

**Check:** For each numeric value on the panel, ask "Would a new user know what this number represents without any other context?"

### Rule 11: Avoid Orphaned Empty Space
Large contiguous empty areas indicate poor layout utilization.

**Definition:** "Orphaned empty space" is an area of 3+ columns × 2+ rows that contains no widgets.

**Common causes:**
- Uneven widget distribution (clustering on one side)
- Widgets sized too small for their content
- Failure to expand widgets to fill available space
- Missing widgets that should complete the layout

**When empty space is acceptable:**
- Intentional visual grouping/separation
- Reserved space for dynamic content that may expand
- When all data fits naturally in fewer widgets

**When empty space is a problem:**
- Bottom-right corner of panel is visually unbalanced
- One half of the panel is dense, the other half is sparse
- Widgets could have been larger to fill the space
- Additional relevant data could have been displayed

**Resolution strategies:**
1. **Expand existing widgets:** Increase colSpan/rowSpan to fill space
2. **Emphasize key metrics:** Give the most important elements more space for greater visual impact
3. **Add complementary data:** Show related metrics that users would find useful
4. **Redistribute layout:** Rearrange widgets for better balance
5. **Combine small widgets:** Merge related stats into fewer, larger widgets

**Bad:** 6 small widgets clustered in left half, right half empty
**Good:** Widgets distributed across full panel width, or fewer larger widgets

### Rule 12: Empty/No-Data States
Show meaningful placeholders when data unavailable - at both panel and widget level.

**Panel-level empty state:** When entire panel has no data:
```csharp
if (!data.hasData)
{
    yield return WidgetDefinition.FullWidth("empty-state", 4, new {
        message = "Data Unavailable"
    });
    yield break;
}
```

**Widget-level empty values:** When a widget has a label but the value is missing/null:
- **Never show blank values** - a label with no value looks broken
- Use meaningful placeholder text: "--", "N/A", "No data", or "0" as appropriate
- For charts/sparklines with no data: show "No data" message or placeholder line

**Placeholder guidelines:**
| Data Type | Placeholder | Example |
|-----------|-------------|---------|
| Temperature | "--" or "-- °C" | TEMP: -- °C |
| Percentage | "--" or "0%" | USAGE: -- |
| Count/Number | "0" or "--" | CORES: 0 |
| Text/Name | "Unknown" or "--" | CPU: Unknown |
| Chart/Graph | "No data" message | Empty sparkline with "No data" |

**Screenshot check:** Every label must have a visible value next to it. If you see a label followed by whitespace or an empty box, it's a violation.

**Don't:** Leave broken/collapsed widgets, show labels with blank values, or render empty charts

### Rule 13: Visual Balance
Distribute content evenly across panel.

- Avoid clustering on one side
- Balance visual weight
- Natural eye flow (left-to-right, top-to-bottom)

**Avoid Visual Monotony:** When all widgets have identical dimensions and styling, the panel becomes visually boring and lacks hierarchy.

**Signs of monotony:**
- All widgets same size (e.g., 6 identical 2×4 cards in a row)
- No variation in widget types (all stat-cards, no gauges/charts/donuts)
- Uniform text sizes throughout
- Single-row or single-column layouts when 2D grid would work better

**Breaking monotony:**
- Vary widget sizes to create hierarchy (hero widget + supporting widgets)
- Mix widget types (stat-cards, donuts, gauges, sparklines)
- Use 2×3 or 3×2 grids instead of 1×6 rows
- Make primary metrics larger than secondary metrics

**Bad:** 6 identical stat-cards in a single row, each 2 cols × 4 rows with tiny text
**Good:** 2 rows × 3 columns of 4×2 widgets with properly scaled text, or a mix of a large hero stat with smaller supporting stats

**Widget Size Consistency:** Similar content types should have consistent sizing:
- If showing CPU and GPU gauges, make them the same size
- If showing multiple info lists, use consistent dimensions
- Size differences should be intentional and meaningful (hero vs supporting), not arbitrary

**Avoid Redundant Data:** Don't show the same value multiple times in different widgets:
- Don't show CPU temp in a stat card AND a gauge AND an info list
- Pick the best visualization for each metric and use it once
- Redundancy wastes space and creates visual clutter

**Bad:** CPU temp 0°C shown in stat card, small gauge, AND info list (3 times!)
**Good:** CPU temp shown once in an appropriately-sized gauge OR info list

### Rule 14: Color Semantics
Colors convey consistent meaning.

| Color | Meaning | Class |
|-------|---------|-------|
| Green | Good, healthy, success | `text-success` |
| Yellow/Orange | Warning, elevated | `text-warning` |
| Red | Critical, error, danger | `text-error` |
| Cyan | Primary accent, neutral highlight | `text-primary` |

**Temperature colors:**
- Cold (<40°C): `text-info` or `text-primary`
- Normal (40-70°C): `text-success`
- Warm (70-85°C): `text-warning`
- Hot (>85°C): `text-error`

### Rule 15: Icon/Symbol Proportionality
Visual elements sized appropriately.

- Gauge stroke width: 6-10px (`--thickness: 8px`)
- Sparkline stroke: 2-3px
- Icons match accompanying text size
- Progress bars: 1-2rem height

### Rule 16: Theme Compliance
All colors from theme palette.

**Do use:**
- CSS variables: `oklch(var(--p))`, `var(--color-primary)`
- DaisyUI classes: `text-primary`, `bg-base-200`, `text-success`
- Tailwind opacity: `text-base-content/70`

**Don't use:**
- Hardcoded hex: `#00d4ff`, `#ff0000`
- Inline styles with colors: `style="color: red"`

---

## Quick Reference: Widget Sizing

| Widget Content | Recommended Span | Value Size |
|----------------|------------------|------------|
| Single large value | 3-4 cols | text-4xl to text-5xl |
| Value with label | 4-6 cols | text-2xl to text-3xl |
| Info list (2-4 items) | 4-5 cols | text-xl values |
| Donut/gauge chart | 3-4 cols | text-3xl to text-4xl |
| Sparkline/graph | 6-8 cols | N/A |
| Device name + details | 5-6 cols | text-2xl to text-4xl |

## Quick Reference: Component Props for Sizing

```csharp
// lcd-stat-card sizes
new { title = "CPU", value = "...", size = "small" }  // text-xl value
new { title = "CPU", value = "...", size = "medium" } // text-2xl value (default)
new { title = "CPU", value = "...", size = "large" }  // text-4xl value

// lcd-donut sizes (adjust via container)
new WidgetDefinition("lcd-donut", 3, 2, ...)  // Smaller
new WidgetDefinition("lcd-donut", 4, 2, ...)  // Standard
new WidgetDefinition("lcd-donut", 5, 3, ...)  // Larger
```
