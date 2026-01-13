/**
 * LCDPossible DaisyUI-Based Components
 * Uses DaisyUI's built-in radial-progress, progress, and stat components.
 * These are CSS-only and theme-integrated - no additional charting library needed.
 */

// Helper to get CSS variable value
function getDaisyCssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

// Get color class based on percentage (for usage indicators)
function getUsageColorClass(percentage) {
    if (percentage >= 90) return 'text-error';
    if (percentage >= 70) return 'text-warning';
    if (percentage >= 50) return 'text-info';
    return 'text-success';
}

// Get color class based on temperature
function getTempColorClass(celsius) {
    if (celsius >= 85) return 'text-error';
    if (celsius >= 70) return 'text-warning';
    return 'text-primary';
}

/**
 * <lcd-daisy-gauge> - Circular gauge using DaisyUI radial-progress
 *
 * Attributes:
 *   value - Current value (0-100)
 *   max - Maximum value (default: 100)
 *   label - Label text
 *   unit - Unit suffix (°C, %, etc.)
 *   type - "usage" or "temp" for auto-coloring
 *   size - "sm", "md" (default), "lg", "xl"
 *   color - Override color class (text-primary, text-success, etc.)
 */
class LcdDaisyGauge extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'unit', 'type', 'size', 'color', 'props'];
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
        const unit = props.unit ?? this.getAttribute('unit') ?? '';
        const type = props.type ?? this.getAttribute('type') ?? 'usage';
        const size = props.size ?? this.getAttribute('size') ?? 'md';
        const colorOverride = props.color ?? this.getAttribute('color');

        const percentage = Math.min(100, Math.max(0, (value / max) * 100));
        const displayValue = Math.round(value);

        // Determine color class
        let colorClass;
        if (colorOverride) {
            colorClass = colorOverride;
        } else if (type === 'temp') {
            colorClass = getTempColorClass(value);
        } else {
            colorClass = getUsageColorClass(percentage);
        }

        // Size mapping to CSS variables
        const sizeMap = {
            'sm': { size: '5rem', thickness: '0.4rem', fontSize: 'text-lg' },
            'md': { size: '7rem', thickness: '0.5rem', fontSize: 'text-2xl' },
            'lg': { size: '9rem', thickness: '0.6rem', fontSize: 'text-3xl' },
            'xl': { size: '12rem', thickness: '0.75rem', fontSize: 'text-4xl' }
        };
        const sizeConfig = sizeMap[size] || sizeMap['md'];

        this.innerHTML = `
            <div class="lcd-daisy-gauge-wrapper flex flex-col items-center justify-center h-full gap-2">
                <div class="radial-progress ${colorClass} bg-base-300 border-4 border-base-300"
                     style="--value:${percentage}; --size:${sizeConfig.size}; --thickness:${sizeConfig.thickness};"
                     role="progressbar"
                     aria-valuenow="${displayValue}"
                     aria-valuemin="0"
                     aria-valuemax="${max}">
                    <span class="${sizeConfig.fontSize} font-bold font-mono">${displayValue}${unit}</span>
                </div>
                ${label ? `<span class="text-sm font-semibold text-primary uppercase tracking-widest">${label}</span>` : ''}
            </div>
        `;
    }
}

/**
 * <lcd-daisy-progress> - Linear progress bar using DaisyUI progress
 *
 * Attributes:
 *   value - Current value
 *   max - Maximum value (default: 100)
 *   label - Label text
 *   show-percent - Show percentage (default: true)
 *   type - "usage" for auto-coloring
 *   color - Override color class (progress-primary, progress-success, etc.)
 *   size - "xs", "sm", "md" (default), "lg"
 */
class LcdDaisyProgress extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'show-percent', 'type', 'color', 'size', 'props'];
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
        const showPercent = (props.showPercent ?? props.show_percent ?? this.getAttribute('show-percent')) !== 'false';
        const type = props.type ?? this.getAttribute('type') ?? 'usage';
        const colorOverride = props.color ?? this.getAttribute('color');
        const size = props.size ?? this.getAttribute('size') ?? 'md';

        const percentage = Math.min(100, Math.max(0, (value / max) * 100));

        // Determine progress color class
        let colorClass;
        if (colorOverride) {
            colorClass = colorOverride;
        } else if (type === 'usage') {
            if (percentage >= 90) colorClass = 'progress-error';
            else if (percentage >= 70) colorClass = 'progress-warning';
            else if (percentage >= 50) colorClass = 'progress-info';
            else colorClass = 'progress-success';
        } else {
            colorClass = 'progress-primary';
        }

        // Size class
        const sizeClass = size === 'xs' ? 'h-1' : size === 'sm' ? 'h-2' : size === 'lg' ? 'h-6' : 'h-4';
        const labelSize = size === 'lg' ? 'text-xl' : size === 'md' ? 'text-lg' : 'text-base';
        const valueSize = size === 'lg' ? 'text-2xl' : size === 'md' ? 'text-xl' : 'text-lg';

        this.innerHTML = `
            <div class="lcd-daisy-progress-wrapper flex flex-col h-full justify-center gap-2">
                ${label || showPercent ? `
                <div class="flex justify-between items-baseline">
                    ${label ? `<span class="${labelSize} font-semibold text-primary uppercase tracking-wider">${label}</span>` : '<span></span>'}
                    ${showPercent ? `<span class="${valueSize} font-bold font-mono text-base-content">${Math.round(percentage)}%</span>` : ''}
                </div>
                ` : ''}
                <progress class="progress ${colorClass} w-full ${sizeClass}" value="${value}" max="${max}"></progress>
            </div>
        `;
    }
}

/**
 * <lcd-daisy-stat> - Stat card using DaisyUI stat component
 *
 * Attributes:
 *   title - Stat title
 *   value - Main value
 *   unit - Unit suffix
 *   desc - Description text
 *   status - "success", "warning", "error", "info" for value color
 *   size - "sm", "md" (default), "lg"
 *   icon - Optional icon (SVG string or emoji)
 */
class LcdDaisyStat extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['title', 'value', 'unit', 'desc', 'status', 'size', 'icon', 'props'];
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
        const desc = props.desc ?? this.getAttribute('desc') ?? '';
        const status = props.status ?? this.getAttribute('status') ?? '';
        const size = props.size ?? this.getAttribute('size') ?? 'md';
        const icon = props.icon ?? this.getAttribute('icon') ?? '';

        // Status color class for value
        const statusClass = status ? `text-${status}` : 'text-base-content';

        // Size mapping
        const sizeMap = {
            'sm': { title: 'text-xs', value: 'text-2xl', desc: 'text-xs' },
            'md': { title: 'text-sm', value: 'text-4xl', desc: 'text-sm' },
            'lg': { title: 'text-base', value: 'text-5xl', desc: 'text-base' }
        };
        const sizeConfig = sizeMap[size] || sizeMap['md'];

        this.innerHTML = `
            <div class="stat bg-base-200/50 rounded-lg border border-primary/10 h-full flex flex-col justify-center">
                ${icon ? `<div class="stat-figure text-primary">${icon}</div>` : ''}
                ${title ? `<div class="stat-title ${sizeConfig.title} uppercase tracking-wider font-semibold text-primary">${title}</div>` : ''}
                <div class="stat-value ${sizeConfig.value} ${statusClass} font-mono truncate">${value}${unit ? `<span class="text-base-content/60 text-lg ml-1">${unit}</span>` : ''}</div>
                ${desc ? `<div class="stat-desc ${sizeConfig.desc} text-base-content/60">${desc}</div>` : ''}
            </div>
        `;
    }
}

/**
 * <lcd-daisy-donut> - Donut chart using CSS conic-gradient
 *
 * Attributes:
 *   value - Current value
 *   max - Maximum value (default: 100)
 *   label - Center label
 *   type - "usage" for auto-coloring
 *   color - Override color class
 *   size - "sm", "md" (default), "lg"
 */
class LcdDaisyDonut extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'type', 'color', 'size', 'props'];
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
        const type = props.type ?? this.getAttribute('type') ?? 'usage';
        const colorOverride = props.color ?? this.getAttribute('color');
        const size = props.size ?? this.getAttribute('size') ?? 'md';

        const percentage = Math.min(100, Math.max(0, (value / max) * 100));

        // Get actual color value
        let fillColor;
        if (colorOverride) {
            fillColor = `oklch(var(--${colorOverride.replace('text-', '')}))`;
        } else if (type === 'usage') {
            if (percentage >= 90) fillColor = 'oklch(var(--er))';
            else if (percentage >= 70) fillColor = 'oklch(var(--wa))';
            else if (percentage >= 50) fillColor = 'oklch(var(--in))';
            else fillColor = 'oklch(var(--su))';
        } else {
            fillColor = 'oklch(var(--p))';
        }

        const bgColor = 'oklch(var(--b3))';

        // Size mapping
        const sizeMap = {
            'sm': { size: '5rem', fontSize: 'text-xl', labelSize: 'text-xs' },
            'md': { size: '7rem', fontSize: 'text-3xl', labelSize: 'text-sm' },
            'lg': { size: '10rem', fontSize: 'text-4xl', labelSize: 'text-base' }
        };
        const sizeConfig = sizeMap[size] || sizeMap['md'];

        // Calculate the angle for conic gradient (percentage to degrees)
        const angle = (percentage / 100) * 360;

        this.innerHTML = `
            <div class="lcd-daisy-donut-wrapper flex flex-col items-center justify-center h-full gap-2">
                <div class="relative flex items-center justify-center"
                     style="width: ${sizeConfig.size}; height: ${sizeConfig.size};">
                    <!-- Donut using conic-gradient -->
                    <div class="absolute inset-0 rounded-full"
                         style="background: conic-gradient(${fillColor} 0deg ${angle}deg, ${bgColor} ${angle}deg 360deg);
                                box-shadow: 0 0 20px ${fillColor.includes('--er') ? 'rgba(255,0,0,0.3)' : fillColor.includes('--wa') ? 'rgba(255,170,0,0.3)' : 'rgba(0,255,255,0.3)'};">
                    </div>
                    <!-- Inner cutout for donut effect -->
                    <div class="absolute rounded-full bg-base-100 flex items-center justify-center"
                         style="width: 70%; height: 70%;">
                        <span class="${sizeConfig.fontSize} font-bold font-mono"
                              style="color: ${fillColor};">${Math.round(percentage)}%</span>
                    </div>
                </div>
                ${label ? `<span class="${sizeConfig.labelSize} font-semibold text-base-content/70 uppercase tracking-wider">${label}</span>` : ''}
            </div>
        `;
    }
}

/**
 * <lcd-daisy-sparkline> - Simple SVG sparkline with DaisyUI theming
 *
 * Attributes:
 *   values - JSON array of numbers
 *   label - Optional label
 *   color - Override color class
 *   style - "line" (default), "area", "bar"
 */
class LcdDaisySparkline extends HTMLElement {
    connectedCallback() {
        this.render();
    }

    static get observedAttributes() {
        return ['values', 'label', 'color', 'style', 'props'];
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

        const label = props.label ?? this.getAttribute('label') ?? '';
        const colorClass = props.color ?? this.getAttribute('color') ?? 'text-primary';
        const chartStyle = props.style ?? this.getAttribute('style') ?? 'line';

        if (values.length === 0) {
            this.innerHTML = `
                <div class="flex items-center justify-center h-full text-base-content/50 text-sm uppercase tracking-wider">
                    No data
                </div>
            `;
            return;
        }

        const min = Math.min(...values);
        const max = Math.max(...values);
        const range = max - min || 1;
        const currentValue = values[values.length - 1];

        // SVG dimensions with padding for stroke
        const padding = 4;
        const viewWidth = 100;
        const viewHeight = 40;
        const drawWidth = viewWidth - (padding * 2);
        const drawHeight = viewHeight - (padding * 2);

        let svgContent;

        if (chartStyle === 'bar') {
            const barWidth = (drawWidth / values.length) * 0.8;
            const barGap = (drawWidth / values.length) * 0.1;
            svgContent = values.map((v, i) => {
                const x = padding + barGap + (i * (drawWidth / values.length));
                const barHeight = Math.max(1, ((v - min) / range) * drawHeight);
                const y = padding + drawHeight - barHeight;
                return `<rect x="${x}" y="${y}" width="${barWidth}" height="${barHeight}" class="fill-primary opacity-80" rx="1"/>`;
            }).join('');
        } else {
            // Line/area style
            const points = values.map((v, i) => {
                const x = padding + (i / (values.length - 1)) * drawWidth;
                const y = padding + drawHeight - ((v - min) / range) * drawHeight;
                return `${x},${y}`;
            }).join(' ');

            const areaPath = chartStyle === 'area' ? (() => {
                const firstX = padding;
                const lastX = padding + drawWidth;
                const bottomY = padding + drawHeight;
                return `${firstX},${bottomY} ${points} ${lastX},${bottomY}`;
            })() : '';

            svgContent = `
                ${chartStyle === 'area' ? `<polygon points="${areaPath}" class="fill-primary/20"/>` : ''}
                <polyline points="${points}" fill="none" class="stroke-primary" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            `;
        }

        this.innerHTML = `
            <div class="lcd-daisy-sparkline-wrapper flex flex-col h-full bg-base-200/30 rounded-lg p-3 border border-primary/10">
                ${label || true ? `
                <div class="flex justify-between items-baseline mb-2">
                    ${label ? `<span class="text-sm font-semibold text-primary uppercase tracking-wider">${label}</span>` : '<span></span>'}
                    <span class="text-xl font-bold font-mono text-base-content">${Math.round(currentValue)}</span>
                </div>
                ` : ''}
                <div class="flex-1 min-h-0">
                    <svg viewBox="0 0 ${viewWidth} ${viewHeight}" preserveAspectRatio="none" class="w-full h-full ${colorClass}">
                        ${svgContent}
                    </svg>
                </div>
            </div>
        `;
    }
}

/**
 * <lcd-daisy-info-list> - Info list using DaisyUI styling
 *
 * Attributes:
 *   title - Optional title
 *   items - JSON array of {label, value, color?}
 *   size - "sm", "md" (default), "lg"
 */
class LcdDaisyInfoList extends HTMLElement {
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
        const size = props.size ?? this.getAttribute('size') ?? 'md';

        let items = props.items ?? [];
        if (this.hasAttribute('items') && !props.items) {
            try {
                items = JSON.parse(this.getAttribute('items'));
            } catch (e) {}
        }

        if (!Array.isArray(items)) items = [];

        // Size mapping
        const sizeMap = {
            'sm': { title: 'text-xs', label: 'text-xs', value: 'text-sm' },
            'md': { title: 'text-sm', label: 'text-sm', value: 'text-lg' },
            'lg': { title: 'text-base', label: 'text-base', value: 'text-xl' }
        };
        const sizeConfig = sizeMap[size] || sizeMap['md'];

        const itemsHtml = items.map(item => `
            <div class="flex justify-between items-center py-1 border-b border-primary/10 last:border-b-0">
                <span class="${sizeConfig.label} font-medium text-base-content/70 uppercase tracking-wide">${item.label || ''}</span>
                <span class="${sizeConfig.value} font-bold font-mono ${item.color || 'text-base-content'}">${item.value || ''}</span>
            </div>
        `).join('');

        this.innerHTML = `
            <div class="lcd-daisy-info-list-wrapper h-full flex flex-col bg-base-200/30 rounded-lg p-3 border border-primary/10">
                ${title ? `<div class="${sizeConfig.title} font-semibold text-primary uppercase tracking-wider mb-2">${title}</div>` : ''}
                <div class="flex-1 flex flex-col justify-center">
                    ${itemsHtml}
                </div>
            </div>
        `;
    }
}

// Register all DaisyUI-based components
customElements.define('lcd-daisy-gauge', LcdDaisyGauge);
customElements.define('lcd-daisy-progress', LcdDaisyProgress);
customElements.define('lcd-daisy-stat', LcdDaisyStat);
customElements.define('lcd-daisy-donut', LcdDaisyDonut);
customElements.define('lcd-daisy-sparkline', LcdDaisySparkline);
customElements.define('lcd-daisy-info-list', LcdDaisyInfoList);

console.log('LCDPossible DaisyUI components loaded');
