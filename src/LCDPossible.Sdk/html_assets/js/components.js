/**
 * LCDPossible Web Components
 * Vanilla JavaScript web components for panel rendering.
 * All components are responsive and scale based on container/viewport size.
 * No external dependencies required.
 */

// Helper to get CSS variable value
function getCssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

// Helper to get usage color based on percentage
function getUsageColor(percentage) {
    if (percentage >= 90) return getCssVar('--color-usage-critical') || '#ff4444';
    if (percentage >= 70) return getCssVar('--color-usage-high') || '#ffaa00';
    if (percentage >= 50) return getCssVar('--color-usage-medium') || '#ffff00';
    return getCssVar('--color-usage-low') || '#00ff88';
}

// Helper to get temperature color
function getTempColor(celsius) {
    if (celsius >= 85) return getCssVar('--color-temp-hot') || '#ff4444';
    if (celsius >= 70) return getCssVar('--color-temp-warm') || '#ffaa00';
    return getCssVar('--color-temp-cool') || '#00d4ff';
}

/**
 * <lcd-usage-bar> - Horizontal or vertical progress bar
 *
 * Attributes:
 *   value - Current value (0-100)
 *   max - Maximum value (default: 100)
 *   label - Optional label text
 *   color - Color override (or uses auto based on value)
 *   orientation - "horizontal" (default) or "vertical"
 *   show-percent - Show percentage text (default: true)
 */
class LcdUsageBar extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'color', 'orientation', 'show-percent', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        // Parse props if provided as JSON
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        const value = parseFloat(props.value ?? this.getAttribute('value') ?? 0);
        const max = parseFloat(props.max ?? this.getAttribute('max') ?? 100);
        const label = props.label ?? this.getAttribute('label') ?? '';
        const color = props.color ?? this.getAttribute('color');
        const orientation = props.orientation ?? this.getAttribute('orientation') ?? 'horizontal';
        const showPercent = (props.showPercent ?? props.show_percent ?? this.getAttribute('show-percent')) !== 'false';

        const percentage = Math.min(100, Math.max(0, (value / max) * 100));
        const fillColor = color || getUsageColor(percentage);

        if (orientation === 'vertical') {
            this.innerHTML = `
                <div class="lcd-usage-bar-vertical">
                    ${label ? `<span class="lcd-bar-label">${label}</span>` : ''}
                    <div class="lcd-bar-track-v">
                        <div class="lcd-bar-fill-v" style="height:${percentage}%;background:${fillColor};"></div>
                    </div>
                    ${showPercent ? `<span class="lcd-bar-value">${Math.round(percentage)}%</span>` : ''}
                </div>
            `;
        } else {
            this.innerHTML = `
                <div class="lcd-usage-bar-horizontal">
                    ${label ? `<div class="lcd-bar-header">
                        <span class="lcd-bar-label">${label}</span>
                        ${showPercent ? `<span class="lcd-bar-value">${Math.round(percentage)}%</span>` : ''}
                    </div>` : ''}
                    <div class="lcd-bar-track-h">
                        <div class="lcd-bar-fill-h" style="width:${percentage}%;background:${fillColor};"></div>
                    </div>
                    ${!label && showPercent ? `<span class="lcd-bar-value-center">${Math.round(percentage)}%</span>` : ''}
                </div>
            `;
        }
    }
}

/**
 * <lcd-stat-card> - Value display with icon and label
 *
 * Attributes:
 *   title - Card title
 *   value - Main value to display
 *   unit - Unit suffix (e.g., "%", "°C", "GB")
 *   subtitle - Secondary text below value
 *   icon - Icon name (for future use)
 *   status - "success", "warning", "critical" for color coding
 *   size - "small", "medium" (default), "large"
 */
class LcdStatCard extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['title', 'value', 'unit', 'subtitle', 'icon', 'status', 'size', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        const title = props.title ?? this.getAttribute('title') ?? '';
        const value = props.value ?? this.getAttribute('value') ?? '';
        const unit = props.unit ?? this.getAttribute('unit') ?? '';
        const subtitle = props.subtitle ?? this.getAttribute('subtitle') ?? '';
        const status = props.status ?? this.getAttribute('status') ?? '';
        const size = props.size ?? this.getAttribute('size') ?? 'medium';

        let valueColor = 'var(--color-text-primary)';
        if (status === 'success') valueColor = 'var(--color-success)';
        if (status === 'warning') valueColor = 'var(--color-warning)';
        if (status === 'critical') valueColor = 'var(--color-critical)';

        const sizeClass = `lcd-stat-card-${size}`;

        this.innerHTML = `
            <div class="lcd-stat-card ${sizeClass}">
                ${title ? `<div class="lcd-stat-title">${title}</div>` : ''}
                <div class="lcd-stat-value-row">
                    <span class="lcd-stat-value" style="color:${valueColor}">${value}</span>
                    ${unit ? `<span class="lcd-stat-unit">${unit}</span>` : ''}
                </div>
                ${subtitle ? `<div class="lcd-stat-subtitle">${subtitle}</div>` : ''}
            </div>
        `;
    }
}

/**
 * <lcd-temp-gauge> - Temperature gauge with color thresholds
 *
 * Attributes:
 *   value - Temperature in Celsius
 *   max - Maximum temperature (default: 100)
 *   label - Label text
 */
class LcdTempGauge extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        const value = parseFloat(props.value ?? this.getAttribute('value') ?? 0);
        const max = parseFloat(props.max ?? this.getAttribute('max') ?? 100);
        const label = props.label ?? this.getAttribute('label') ?? '';

        const percentage = Math.min(100, Math.max(0, (value / max) * 100));
        const color = getTempColor(value);

        // SVG donut chart - size is responsive via CSS
        const radius = 45;
        const circumference = 2 * Math.PI * radius;
        const strokeDashoffset = circumference - (percentage / 100) * circumference;

        this.innerHTML = `
            <div class="lcd-temp-gauge">
                ${label ? `<div class="lcd-gauge-label">${label}</div>` : ''}
                <div class="lcd-gauge-container">
                    <svg viewBox="0 0 100 100" class="lcd-gauge-svg">
                        <circle cx="50" cy="50" r="${radius}" fill="none" stroke="var(--color-bar-background)" stroke-width="8"/>
                        <circle cx="50" cy="50" r="${radius}" fill="none" stroke="${color}" stroke-width="8"
                            stroke-dasharray="${circumference}" stroke-dashoffset="${strokeDashoffset}"
                            stroke-linecap="round" class="lcd-gauge-fill"/>
                    </svg>
                    <div class="lcd-gauge-value">
                        <span style="color:${color}">${Math.round(value)}°</span>
                    </div>
                </div>
            </div>
        `;
    }
}

/**
 * <lcd-info-list> - Label/value pairs list
 *
 * Attributes:
 *   title - Optional title for the list
 *   items - JSON array of {label, value, color?} objects
 *   size - "small", "medium" (default), "large" for text scaling
 */
class LcdInfoList extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['title', 'items', 'size', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        const title = props.title ?? this.getAttribute('title') ?? '';
        const size = props.size ?? this.getAttribute('size') ?? 'medium';

        let items = props.items ?? [];
        if (this.hasAttribute('items') && !props.items) {
            try {
                items = JSON.parse(this.getAttribute('items'));
            } catch (e) {}
        }

        if (!Array.isArray(items)) items = [];

        const sizeClass = `lcd-info-list-${size}`;

        const itemsHtml = items.map(item => `
            <div class="lcd-info-item">
                <span class="lcd-info-label">${item.label || ''}</span>
                <span class="lcd-info-value" style="color:${item.color || 'var(--color-text-primary)'}">${item.value || ''}</span>
            </div>
        `).join('');

        this.innerHTML = `
            <div class="lcd-info-list ${sizeClass}">
                ${title ? `<div class="lcd-info-title">${title}</div>` : ''}
                <div class="lcd-info-items">
                    ${itemsHtml}
                </div>
            </div>
        `;
    }
}

/**
 * <lcd-sparkline> - Mini time-series chart
 *
 * Attributes:
 *   values - JSON array of numbers
 *   color - Line color (default: accent)
 *   label - Optional label
 *   fill - Whether to fill under the line (default: false)
 *   style - "line" (default), "area", or "bar"
 */
class LcdSparkline extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['values', 'color', 'label', 'fill', 'style', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        let values = props.values ?? [];
        if (this.hasAttribute('values') && !props.values) {
            try {
                values = JSON.parse(this.getAttribute('values'));
            } catch (e) {}
        }

        if (!Array.isArray(values)) values = [];

        const color = props.color ?? this.getAttribute('color') ?? (getCssVar('--color-accent') || '#00d4ff');
        const label = props.label ?? this.getAttribute('label') ?? '';
        const fill = props.fill ?? this.getAttribute('fill') === 'true';
        const style = props.style ?? this.getAttribute('style') ?? 'line';

        if (values.length === 0) {
            this.innerHTML = `<div class="lcd-sparkline-empty">No data</div>`;
            return;
        }

        const min = Math.min(...values);
        const max = Math.max(...values);
        const range = max - min || 1;

        // Padding to prevent stroke overflow at edges
        const strokeWidth = 2;
        const padding = strokeWidth + 1; // Extra pixel for anti-aliasing
        const viewWidth = 100;
        const viewHeight = 50;
        const drawWidth = viewWidth - (padding * 2);
        const drawHeight = viewHeight - (padding * 2);

        const currentValue = values[values.length - 1];

        if (style === 'bar') {
            // Bar chart style
            const barWidth = drawWidth / values.length * 0.8;
            const barGap = drawWidth / values.length * 0.2;
            const bars = values.map((v, i) => {
                const x = padding + (i / values.length) * drawWidth + barGap/2;
                const barHeight = ((v - min) / range) * drawHeight;
                const y = padding + drawHeight - barHeight;
                return `<rect x="${x}" y="${y}" width="${barWidth}" height="${barHeight}" fill="${color}" opacity="0.8"/>`;
            }).join('');

            this.innerHTML = `
                <div class="lcd-sparkline">
                    ${label ? `<div class="lcd-sparkline-header">
                        <span class="lcd-sparkline-label">${label}</span>
                        <span class="lcd-sparkline-value">${Math.round(currentValue)}</span>
                    </div>` : ''}
                    <div class="lcd-sparkline-chart">
                        <svg viewBox="0 0 ${viewWidth} ${viewHeight}" preserveAspectRatio="none" class="lcd-sparkline-svg">
                            ${bars}
                        </svg>
                    </div>
                </div>
            `;
            return;
        }

        // Line/Area style - calculate points with padding
        const points = values.map((v, i) => {
            const x = padding + (i / (values.length - 1)) * drawWidth;
            const y = padding + drawHeight - ((v - min) / range) * drawHeight;
            return `${x},${y}`;
        }).join(' ');

        // For area fill, create a closed polygon
        const areaPath = fill || style === 'area' ? (() => {
            const firstX = padding;
            const lastX = padding + drawWidth;
            const bottomY = padding + drawHeight;
            return `${firstX},${bottomY} ${points} ${lastX},${bottomY}`;
        })() : '';

        this.innerHTML = `
            <div class="lcd-sparkline">
                ${label ? `<div class="lcd-sparkline-header">
                    <span class="lcd-sparkline-label">${label}</span>
                    <span class="lcd-sparkline-value">${Math.round(currentValue)}</span>
                </div>` : ''}
                <div class="lcd-sparkline-chart">
                    <svg viewBox="0 0 ${viewWidth} ${viewHeight}" preserveAspectRatio="none" class="lcd-sparkline-svg">
                        ${(fill || style === 'area') ? `<polygon points="${areaPath}" fill="${color}" opacity="0.2"/>` : ''}
                        <polyline points="${points}" fill="none" stroke="${color}" stroke-width="${strokeWidth}" stroke-linejoin="round" stroke-linecap="round" vector-effect="non-scaling-stroke"/>
                    </svg>
                </div>
            </div>
        `;
    }
}

/**
 * <lcd-status-dot> - Colored status indicator
 *
 * Attributes:
 *   status - "success", "warning", "critical", "info"
 *   label - Optional label text
 */
class LcdStatusDot extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['status', 'label', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        const status = props.status ?? this.getAttribute('status') ?? 'info';
        const label = props.label ?? this.getAttribute('label') ?? '';

        const colors = {
            success: 'var(--color-success)',
            warning: 'var(--color-warning)',
            critical: 'var(--color-critical)',
            info: 'var(--color-info)'
        };

        const color = colors[status] || colors.info;

        this.innerHTML = `
            <div class="lcd-status-dot">
                <span class="lcd-dot" style="background:${color};"></span>
                ${label ? `<span class="lcd-dot-label">${label}</span>` : ''}
            </div>
        `;
    }
}

/**
 * <lcd-donut> - Circular percentage display
 *
 * Attributes:
 *   value - Current value
 *   max - Maximum value (default: 100)
 *   label - Center label
 *   color - Fill color (auto-detected based on percentage if not provided)
 */
class LcdDonut extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'color', 'props'];
    }

    attributeChangedCallback() {
        this.render();
    }

    render() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }

        const value = parseFloat(props.value ?? this.getAttribute('value') ?? 0);
        const max = parseFloat(props.max ?? this.getAttribute('max') ?? 100);
        const label = props.label ?? this.getAttribute('label') ?? '';

        const percentage = Math.min(100, Math.max(0, (value / max) * 100));
        const color = props.color ?? this.getAttribute('color') ?? getUsageColor(percentage);

        const radius = 40;
        const circumference = 2 * Math.PI * radius;
        const strokeDashoffset = circumference - (percentage / 100) * circumference;

        this.innerHTML = `
            <div class="lcd-donut">
                <div class="lcd-donut-container">
                    <svg viewBox="0 0 100 100" class="lcd-donut-svg">
                        <circle cx="50" cy="50" r="${radius}" fill="none" stroke="var(--color-bar-background)" stroke-width="10"/>
                        <circle cx="50" cy="50" r="${radius}" fill="none" stroke="${color}" stroke-width="10"
                            stroke-dasharray="${circumference}" stroke-dashoffset="${strokeDashoffset}"
                            stroke-linecap="round" class="lcd-donut-fill"/>
                    </svg>
                    <div class="lcd-donut-center">
                        <span class="lcd-donut-value" style="color:${color}">${Math.round(percentage)}%</span>
                        ${label ? `<span class="lcd-donut-label">${label}</span>` : ''}
                    </div>
                </div>
            </div>
        `;
    }
}

// Register all custom elements
customElements.define('lcd-usage-bar', LcdUsageBar);
customElements.define('lcd-stat-card', LcdStatCard);
customElements.define('lcd-temp-gauge', LcdTempGauge);
customElements.define('lcd-info-list', LcdInfoList);
customElements.define('lcd-sparkline', LcdSparkline);
customElements.define('lcd-status-dot', LcdStatusDot);
customElements.define('lcd-donut', LcdDonut);

console.log('LCDPossible components loaded');
