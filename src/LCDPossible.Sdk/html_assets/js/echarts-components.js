/**
 * LCDPossible ECharts-Based Components
 * High-quality graphical components using Apache ECharts.
 * All components are responsive and fill their containers properly.
 */

// Wait for ECharts to be loaded
if (typeof echarts === 'undefined') {
    console.error('ECharts not loaded! Include echarts.min.js before this file.');
}

// Helper to get CSS variable value
function getEchartsCssVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

// Get theme colors
function getThemeColors() {
    return {
        accent: getEchartsCssVar('--color-accent') || '#00ffff',
        accentSecondary: getEchartsCssVar('--color-accent-secondary') || '#ff00aa',
        success: getEchartsCssVar('--color-success') || '#00ff88',
        warning: getEchartsCssVar('--color-warning') || '#ffaa00',
        critical: getEchartsCssVar('--color-critical') || '#ff2244',
        info: getEchartsCssVar('--color-info') || '#00aaff',
        textPrimary: getEchartsCssVar('--color-text-primary') || '#ffffff',
        textSecondary: getEchartsCssVar('--color-text-secondary') || '#88aacc',
        textMuted: getEchartsCssVar('--color-text-muted') || '#446688',
        background: getEchartsCssVar('--color-background') || '#050508',
        barBackground: getEchartsCssVar('--color-bar-background') || 'rgba(20, 30, 50, 0.6)'
    };
}

// Get color based on percentage value (for usage indicators)
function getUsageColorEcharts(percentage) {
    if (percentage >= 90) return getEchartsCssVar('--color-usage-critical') || '#ff2244';
    if (percentage >= 70) return getEchartsCssVar('--color-usage-high') || '#ff8800';
    if (percentage >= 50) return getEchartsCssVar('--color-usage-medium') || '#ffff00';
    return getEchartsCssVar('--color-usage-low') || '#00ff88';
}

// Get color based on temperature
function getTempColorEcharts(celsius) {
    if (celsius >= 85) return getEchartsCssVar('--color-temp-hot') || '#ff2244';
    if (celsius >= 70) return getEchartsCssVar('--color-temp-warm') || '#ffaa00';
    return getEchartsCssVar('--color-temp-cool') || '#00ffff';
}

/**
 * <lcd-echarts-gauge> - Professional gauge using ECharts
 *
 * Attributes:
 *   value - Current value (0-100 by default)
 *   max - Maximum value (default: 100)
 *   min - Minimum value (default: 0)
 *   label - Label text shown in center
 *   unit - Unit suffix (e.g., "°C", "%")
 *   type - "usage" (green->red) or "temp" (blue->red) for auto-coloring
 *   color - Override color (hex)
 *   style - "arc" (default), "speedometer", "ring"
 */
class LcdEchartsGauge extends HTMLElement {
    constructor() {
        super();
        this._chart = null;
        this._resizeObserver = null;
    }

    connectedCallback() {
        this.render();
        this._setupResizeObserver();
    }

    disconnectedCallback() {
        if (this._chart) {
            this._chart.dispose();
            this._chart = null;
        }
        if (this._resizeObserver) {
            this._resizeObserver.disconnect();
        }
    }

    static get observedAttributes() {
        return ['value', 'max', 'min', 'label', 'unit', 'type', 'color', 'style', 'props'];
    }

    attributeChangedCallback() {
        if (this._chart) {
            this._updateChart();
        }
    }

    _setupResizeObserver() {
        this._resizeObserver = new ResizeObserver(() => {
            if (this._chart) {
                this._chart.resize();
            }
        });
        this._resizeObserver.observe(this);
    }

    _getProps() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }
        return {
            value: parseFloat(props.value ?? this.getAttribute('value') ?? 0),
            max: parseFloat(props.max ?? this.getAttribute('max') ?? 100),
            min: parseFloat(props.min ?? this.getAttribute('min') ?? 0),
            label: props.label ?? this.getAttribute('label') ?? '',
            unit: props.unit ?? this.getAttribute('unit') ?? '',
            type: props.type ?? this.getAttribute('type') ?? 'usage',
            color: props.color ?? this.getAttribute('color'),
            style: props.style ?? this.getAttribute('style') ?? 'arc'
        };
    }

    render() {
        // Create container div
        this.innerHTML = '<div class="echarts-gauge-container"></div>';
        const container = this.querySelector('.echarts-gauge-container');
        container.style.cssText = 'width: 100%; height: 100%; min-height: 80px;';

        // Initialize ECharts
        this._chart = echarts.init(container, null, { renderer: 'canvas' });
        this._updateChart();
    }

    _updateChart() {
        const props = this._getProps();
        const colors = getThemeColors();
        const percentage = ((props.value - props.min) / (props.max - props.min)) * 100;

        // Determine color
        let gaugeColor;
        if (props.color) {
            gaugeColor = props.color;
        } else if (props.type === 'temp') {
            gaugeColor = getTempColorEcharts(props.value);
        } else {
            gaugeColor = getUsageColorEcharts(percentage);
        }

        let option;

        if (props.style === 'speedometer') {
            // Speedometer style with tick marks
            option = {
                series: [{
                    type: 'gauge',
                    startAngle: 200,
                    endAngle: -20,
                    min: props.min,
                    max: props.max,
                    splitNumber: 5,
                    itemStyle: {
                        color: gaugeColor,
                        shadowColor: gaugeColor,
                        shadowBlur: 15
                    },
                    progress: {
                        show: true,
                        width: 12,
                        itemStyle: {
                            color: gaugeColor,
                            shadowColor: gaugeColor,
                            shadowBlur: 10
                        }
                    },
                    pointer: {
                        show: true,
                        length: '60%',
                        width: 6,
                        itemStyle: {
                            color: colors.textPrimary
                        }
                    },
                    axisLine: {
                        lineStyle: {
                            width: 12,
                            color: [[1, colors.barBackground]]
                        }
                    },
                    axisTick: {
                        distance: -18,
                        length: 6,
                        lineStyle: {
                            color: colors.textMuted,
                            width: 1
                        }
                    },
                    splitLine: {
                        distance: -20,
                        length: 10,
                        lineStyle: {
                            color: colors.textSecondary,
                            width: 2
                        }
                    },
                    axisLabel: {
                        distance: 8,
                        color: colors.textMuted,
                        fontSize: 10
                    },
                    anchor: {
                        show: true,
                        size: 12,
                        itemStyle: {
                            color: colors.textPrimary,
                            borderWidth: 2,
                            borderColor: gaugeColor
                        }
                    },
                    title: {
                        show: !!props.label,
                        offsetCenter: [0, '70%'],
                        color: colors.accent,
                        fontSize: 14,
                        fontWeight: 'bold'
                    },
                    detail: {
                        valueAnimation: true,
                        fontSize: 24,
                        fontWeight: 'bold',
                        offsetCenter: [0, '40%'],
                        color: gaugeColor,
                        formatter: (value) => `${Math.round(value)}${props.unit}`
                    },
                    data: [{
                        value: props.value,
                        name: props.label.toUpperCase()
                    }]
                }]
            };
        } else if (props.style === 'ring') {
            // Simple ring/donut style
            option = {
                series: [{
                    type: 'gauge',
                    startAngle: 90,
                    endAngle: -270,
                    min: props.min,
                    max: props.max,
                    pointer: { show: false },
                    progress: {
                        show: true,
                        overlap: false,
                        roundCap: true,
                        clip: false,
                        width: 16,
                        itemStyle: {
                            color: gaugeColor,
                            shadowColor: gaugeColor,
                            shadowBlur: 15
                        }
                    },
                    axisLine: {
                        lineStyle: {
                            width: 16,
                            color: [[1, colors.barBackground]]
                        }
                    },
                    splitLine: { show: false },
                    axisTick: { show: false },
                    axisLabel: { show: false },
                    title: {
                        show: !!props.label,
                        offsetCenter: [0, '30%'],
                        color: colors.textSecondary,
                        fontSize: 12,
                        fontWeight: 'bold'
                    },
                    detail: {
                        valueAnimation: true,
                        fontSize: 28,
                        fontWeight: 'bold',
                        offsetCenter: [0, 0],
                        color: gaugeColor,
                        formatter: (value) => `${Math.round(value)}${props.unit}`
                    },
                    data: [{
                        value: props.value,
                        name: props.label.toUpperCase()
                    }]
                }]
            };
        } else {
            // Default arc style
            option = {
                series: [{
                    type: 'gauge',
                    startAngle: 180,
                    endAngle: 0,
                    min: props.min,
                    max: props.max,
                    pointer: { show: false },
                    progress: {
                        show: true,
                        overlap: false,
                        roundCap: true,
                        clip: false,
                        width: 14,
                        itemStyle: {
                            color: {
                                type: 'linear',
                                x: 0, y: 0, x2: 1, y2: 0,
                                colorStops: [
                                    { offset: 0, color: gaugeColor },
                                    { offset: 1, color: gaugeColor }
                                ]
                            },
                            shadowColor: gaugeColor,
                            shadowBlur: 15
                        }
                    },
                    axisLine: {
                        lineStyle: {
                            width: 14,
                            color: [[1, colors.barBackground]]
                        }
                    },
                    splitLine: { show: false },
                    axisTick: { show: false },
                    axisLabel: { show: false },
                    title: {
                        show: !!props.label,
                        offsetCenter: [0, '40%'],
                        color: colors.accent,
                        fontSize: 14,
                        fontWeight: 'bold',
                        fontFamily: 'system-ui, sans-serif'
                    },
                    detail: {
                        valueAnimation: true,
                        fontSize: 32,
                        fontWeight: 'bold',
                        offsetCenter: [0, '-10%'],
                        color: gaugeColor,
                        formatter: (value) => `${Math.round(value)}${props.unit}`,
                        fontFamily: 'ui-monospace, monospace'
                    },
                    data: [{
                        value: props.value,
                        name: props.label.toUpperCase()
                    }]
                }]
            };
        }

        this._chart.setOption(option, true);
    }
}

/**
 * <lcd-echarts-donut> - Circular progress/percentage using ECharts
 *
 * Attributes:
 *   value - Current value
 *   max - Maximum value (default: 100)
 *   label - Center label
 *   color - Fill color (auto if not provided)
 *   type - "usage" for auto-coloring based on percentage
 */
class LcdEchartsDonut extends HTMLElement {
    constructor() {
        super();
        this._chart = null;
        this._resizeObserver = null;
    }

    connectedCallback() {
        this.render();
        this._setupResizeObserver();
    }

    disconnectedCallback() {
        if (this._chart) {
            this._chart.dispose();
            this._chart = null;
        }
        if (this._resizeObserver) {
            this._resizeObserver.disconnect();
        }
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'color', 'type', 'props'];
    }

    attributeChangedCallback() {
        if (this._chart) {
            this._updateChart();
        }
    }

    _setupResizeObserver() {
        this._resizeObserver = new ResizeObserver(() => {
            if (this._chart) {
                this._chart.resize();
            }
        });
        this._resizeObserver.observe(this);
    }

    _getProps() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }
        return {
            value: parseFloat(props.value ?? this.getAttribute('value') ?? 0),
            max: parseFloat(props.max ?? this.getAttribute('max') ?? 100),
            label: props.label ?? this.getAttribute('label') ?? '',
            color: props.color ?? this.getAttribute('color'),
            type: props.type ?? this.getAttribute('type') ?? 'usage'
        };
    }

    render() {
        this.innerHTML = '<div class="echarts-donut-container"></div>';
        const container = this.querySelector('.echarts-donut-container');
        container.style.cssText = 'width: 100%; height: 100%; min-height: 80px;';

        this._chart = echarts.init(container, null, { renderer: 'canvas' });
        this._updateChart();
    }

    _updateChart() {
        const props = this._getProps();
        const colors = getThemeColors();
        const percentage = Math.min(100, Math.max(0, (props.value / props.max) * 100));

        const fillColor = props.color || getUsageColorEcharts(percentage);

        const option = {
            series: [{
                type: 'pie',
                radius: ['65%', '85%'],
                avoidLabelOverlap: false,
                itemStyle: {
                    borderRadius: 8
                },
                label: {
                    show: true,
                    position: 'center',
                    formatter: () => `${Math.round(percentage)}%`,
                    fontSize: 28,
                    fontWeight: 'bold',
                    color: fillColor,
                    fontFamily: 'ui-monospace, monospace'
                },
                emphasis: {
                    scale: false
                },
                labelLine: {
                    show: false
                },
                data: [
                    {
                        value: props.value,
                        name: props.label,
                        itemStyle: {
                            color: fillColor,
                            shadowColor: fillColor,
                            shadowBlur: 15
                        }
                    },
                    {
                        value: props.max - props.value,
                        name: 'remaining',
                        itemStyle: {
                            color: colors.barBackground
                        },
                        label: { show: false }
                    }
                ]
            }],
            graphic: props.label ? [{
                type: 'text',
                left: 'center',
                bottom: '15%',
                style: {
                    text: props.label.toUpperCase(),
                    fill: colors.textSecondary,
                    fontSize: 12,
                    fontWeight: 'bold',
                    fontFamily: 'system-ui, sans-serif'
                }
            }] : []
        };

        this._chart.setOption(option, true);
    }
}

/**
 * <lcd-echarts-sparkline> - Line/area/bar chart using ECharts
 *
 * Attributes:
 *   values - JSON array of numbers
 *   color - Line color
 *   label - Chart label
 *   style - "line" (default), "area", "bar"
 *   show-value - Show current value (default: true)
 */
class LcdEchartsSparkline extends HTMLElement {
    constructor() {
        super();
        this._chart = null;
        this._resizeObserver = null;
    }

    connectedCallback() {
        this.render();
        this._setupResizeObserver();
    }

    disconnectedCallback() {
        if (this._chart) {
            this._chart.dispose();
            this._chart = null;
        }
        if (this._resizeObserver) {
            this._resizeObserver.disconnect();
        }
    }

    static get observedAttributes() {
        return ['values', 'color', 'label', 'style', 'show-value', 'props'];
    }

    attributeChangedCallback() {
        if (this._chart) {
            this._updateChart();
        }
    }

    _setupResizeObserver() {
        this._resizeObserver = new ResizeObserver(() => {
            if (this._chart) {
                this._chart.resize();
            }
        });
        this._resizeObserver.observe(this);
    }

    _getProps() {
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

        return {
            values: Array.isArray(values) ? values : [],
            color: props.color ?? this.getAttribute('color'),
            label: props.label ?? this.getAttribute('label') ?? '',
            style: props.style ?? this.getAttribute('style') ?? 'line',
            showValue: (props.showValue ?? props.show_value ?? this.getAttribute('show-value')) !== 'false'
        };
    }

    render() {
        this.innerHTML = '<div class="echarts-sparkline-container"></div>';
        const container = this.querySelector('.echarts-sparkline-container');
        container.style.cssText = 'width: 100%; height: 100%; min-height: 60px;';

        this._chart = echarts.init(container, null, { renderer: 'canvas' });
        this._updateChart();
    }

    _updateChart() {
        const props = this._getProps();
        const colors = getThemeColors();

        if (props.values.length === 0) {
            this._chart.clear();
            this._chart.setOption({
                graphic: [{
                    type: 'text',
                    left: 'center',
                    top: 'middle',
                    style: {
                        text: 'No data',
                        fill: colors.textMuted,
                        fontSize: 14
                    }
                }]
            });
            return;
        }

        const lineColor = props.color || colors.accent;
        const currentValue = props.values[props.values.length - 1];
        const xData = props.values.map((_, i) => i);

        let series;
        if (props.style === 'bar') {
            series = {
                type: 'bar',
                data: props.values,
                itemStyle: {
                    color: lineColor,
                    borderRadius: [4, 4, 0, 0]
                },
                barWidth: '60%'
            };
        } else {
            series = {
                type: 'line',
                data: props.values,
                smooth: true,
                symbol: 'none',
                lineStyle: {
                    width: 3,
                    color: lineColor,
                    shadowColor: lineColor,
                    shadowBlur: 10
                },
                areaStyle: props.style === 'area' ? {
                    color: {
                        type: 'linear',
                        x: 0, y: 0, x2: 0, y2: 1,
                        colorStops: [
                            { offset: 0, color: lineColor + '40' },
                            { offset: 1, color: lineColor + '05' }
                        ]
                    }
                } : undefined
            };
        }

        const option = {
            grid: {
                left: 5,
                right: 5,
                top: props.label && props.showValue ? 35 : 10,
                bottom: 5,
                containLabel: false
            },
            xAxis: {
                type: 'category',
                data: xData,
                show: false,
                boundaryGap: props.style === 'bar'
            },
            yAxis: {
                type: 'value',
                show: false,
                min: (value) => Math.floor(value.min * 0.9),
                max: (value) => Math.ceil(value.max * 1.1)
            },
            series: [series],
            graphic: (props.label || props.showValue) ? [{
                type: 'group',
                left: 10,
                top: 5,
                children: [
                    props.label ? {
                        type: 'text',
                        style: {
                            text: props.label.toUpperCase(),
                            fill: colors.accent,
                            fontSize: 12,
                            fontWeight: 'bold'
                        }
                    } : null,
                    props.showValue ? {
                        type: 'text',
                        left: props.label ? 'auto' : 0,
                        style: {
                            text: Math.round(currentValue).toString(),
                            fill: colors.textPrimary,
                            fontSize: 18,
                            fontWeight: 'bold',
                            x: props.label ? 10 : 0
                        }
                    } : null
                ].filter(Boolean)
            }] : []
        };

        // Position value on right if label exists
        if (props.label && props.showValue) {
            option.graphic = [{
                type: 'group',
                left: 10,
                top: 5,
                children: [{
                    type: 'text',
                    style: {
                        text: props.label.toUpperCase(),
                        fill: colors.accent,
                        fontSize: 12,
                        fontWeight: 'bold'
                    }
                }]
            }, {
                type: 'text',
                right: 10,
                top: 5,
                style: {
                    text: Math.round(currentValue).toString(),
                    fill: colors.textPrimary,
                    fontSize: 18,
                    fontWeight: 'bold'
                }
            }];
        }

        this._chart.setOption(option, true);
    }
}

/**
 * <lcd-echarts-progress> - Horizontal/vertical progress bar using ECharts
 *
 * Attributes:
 *   value - Current value
 *   max - Maximum value (default: 100)
 *   label - Label text
 *   orientation - "horizontal" (default) or "vertical"
 *   show-percent - Show percentage (default: true)
 *   color - Override color
 */
class LcdEchartsProgress extends HTMLElement {
    constructor() {
        super();
        this._chart = null;
        this._resizeObserver = null;
    }

    connectedCallback() {
        this.render();
        this._setupResizeObserver();
    }

    disconnectedCallback() {
        if (this._chart) {
            this._chart.dispose();
            this._chart = null;
        }
        if (this._resizeObserver) {
            this._resizeObserver.disconnect();
        }
    }

    static get observedAttributes() {
        return ['value', 'max', 'label', 'orientation', 'show-percent', 'color', 'props'];
    }

    attributeChangedCallback() {
        if (this._chart) {
            this._updateChart();
        }
    }

    _setupResizeObserver() {
        this._resizeObserver = new ResizeObserver(() => {
            if (this._chart) {
                this._chart.resize();
            }
        });
        this._resizeObserver.observe(this);
    }

    _getProps() {
        let props = {};
        if (this.hasAttribute('props')) {
            try {
                props = JSON.parse(this.getAttribute('props'));
            } catch (e) {}
        }
        return {
            value: parseFloat(props.value ?? this.getAttribute('value') ?? 0),
            max: parseFloat(props.max ?? this.getAttribute('max') ?? 100),
            label: props.label ?? this.getAttribute('label') ?? '',
            orientation: props.orientation ?? this.getAttribute('orientation') ?? 'horizontal',
            showPercent: (props.showPercent ?? props.show_percent ?? this.getAttribute('show-percent')) !== 'false',
            color: props.color ?? this.getAttribute('color')
        };
    }

    render() {
        this.innerHTML = '<div class="echarts-progress-container"></div>';
        const container = this.querySelector('.echarts-progress-container');
        container.style.cssText = 'width: 100%; height: 100%; min-height: 40px;';

        this._chart = echarts.init(container, null, { renderer: 'canvas' });
        this._updateChart();
    }

    _updateChart() {
        const props = this._getProps();
        const colors = getThemeColors();
        const percentage = Math.min(100, Math.max(0, (props.value / props.max) * 100));
        const fillColor = props.color || getUsageColorEcharts(percentage);
        const isVertical = props.orientation === 'vertical';

        const option = {
            grid: {
                left: props.label && !isVertical ? 80 : 10,
                right: props.showPercent && !isVertical ? 60 : 10,
                top: isVertical && props.showPercent ? 40 : 10,
                bottom: isVertical && props.label ? 30 : 10,
                containLabel: false
            },
            xAxis: {
                type: isVertical ? 'value' : 'category',
                data: isVertical ? undefined : [''],
                show: false,
                max: isVertical ? props.max : undefined
            },
            yAxis: {
                type: isVertical ? 'category' : 'value',
                data: isVertical ? [''] : undefined,
                show: false,
                max: isVertical ? undefined : props.max
            },
            series: [{
                type: 'bar',
                data: [props.value],
                barWidth: isVertical ? '60%' : '80%',
                itemStyle: {
                    color: {
                        type: 'linear',
                        x: 0, y: 0,
                        x2: isVertical ? 1 : 0,
                        y2: isVertical ? 0 : 1,
                        colorStops: [
                            { offset: 0, color: fillColor },
                            { offset: 1, color: fillColor + 'cc' }
                        ]
                    },
                    borderRadius: 4,
                    shadowColor: fillColor,
                    shadowBlur: 10
                },
                backgroundStyle: {
                    color: colors.barBackground,
                    borderRadius: 4
                },
                showBackground: true
            }],
            graphic: [
                props.label ? {
                    type: 'text',
                    left: isVertical ? 'center' : 10,
                    bottom: isVertical ? 5 : undefined,
                    top: isVertical ? undefined : 'middle',
                    style: {
                        text: props.label.toUpperCase(),
                        fill: colors.accent,
                        fontSize: 14,
                        fontWeight: 'bold'
                    }
                } : null,
                props.showPercent ? {
                    type: 'text',
                    right: isVertical ? undefined : 10,
                    left: isVertical ? 'center' : undefined,
                    top: isVertical ? 5 : 'middle',
                    style: {
                        text: `${Math.round(percentage)}%`,
                        fill: colors.textPrimary,
                        fontSize: 20,
                        fontWeight: 'bold'
                    }
                } : null
            ].filter(Boolean)
        };

        this._chart.setOption(option, true);
    }
}

// Register all ECharts components
customElements.define('lcd-echarts-gauge', LcdEchartsGauge);
customElements.define('lcd-echarts-donut', LcdEchartsDonut);
customElements.define('lcd-echarts-sparkline', LcdEchartsSparkline);
customElements.define('lcd-echarts-progress', LcdEchartsProgress);

console.log('LCDPossible ECharts components loaded');
