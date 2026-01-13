# No Remote Assets Rule

**All core assets must be local.** No CDN, remote URLs, or external dependencies at runtime.

## Applies To

- `src/LCDPossible.Sdk/html_assets/` - All web assets for panel rendering
- HTML templates used in panels
- Any embedded resources

## Prohibited

| Type | Examples |
|------|----------|
| CSS imports | `@import url('https://...')` |
| Stylesheets | `<link href="https://...">` |
| Scripts | `<script src="https://...">` |
| Fonts | Google Fonts, Adobe Fonts, any `@font-face` with remote URLs |
| Images | Remote image URLs in CSS or HTML |
| Any fetch | Runtime requests to external services |

## Required

1. **Download and bundle** all third-party CSS/JS libraries locally
2. **Use system font stacks** instead of web fonts:

```css
/* Sans-serif */
font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;

/* Monospace */
font-family: ui-monospace, 'Cascadia Code', 'Source Code Pro', Menlo, Consolas, monospace;
```

3. **Embed or bundle** any required icons/images as local files or data URIs

## Rationale

1. **Offline operation** - LCD displays may not have internet access
2. **Consistent rendering** - No network latency or failures
3. **Privacy** - No external requests from the display system
4. **Reliability** - CDNs can go down or change URLs
5. **Performance** - No DNS lookups or connection overhead
