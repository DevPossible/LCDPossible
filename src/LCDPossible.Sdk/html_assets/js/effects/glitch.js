/**
 * Glitch Effect
 * Random digital glitch effects on high values.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _glitchIntensity: 0,
    _nextGlitchTime: 0,
    _glitching: false,
    _glitchDuration: 0,
    _slices: [],

    onInit: function(options) {
        this._threshold = options.threshold || 70;
        this._maxIntensity = options.maxIntensity || 1;

        // Create canvas overlay
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-glitch-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: 9999;
            pointer-events: none;
            mix-blend-mode: screen;
        `;
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        // Add CSS for widget glitching
        const style = document.createElement('style');
        style.id = 'effect-glitch-style';
        style.textContent = `
            .lcd-widget.glitching {
                animation: widgetGlitch 0.1s steps(2) infinite;
            }
            @keyframes widgetGlitch {
                0% { transform: translate(0); filter: hue-rotate(0deg); }
                25% { transform: translate(-2px, 1px); filter: hue-rotate(90deg); }
                50% { transform: translate(2px, -1px); filter: hue-rotate(180deg); }
                75% { transform: translate(-1px, 2px); filter: hue-rotate(270deg); }
                100% { transform: translate(0); filter: hue-rotate(360deg); }
            }
            .lcd-widget.critical-glitch {
                animation: criticalGlitch 0.05s steps(3) infinite;
            }
            @keyframes criticalGlitch {
                0% { transform: translate(0) skewX(0deg); clip-path: inset(0 0 0 0); }
                20% { transform: translate(-3px, 2px) skewX(1deg); clip-path: inset(10% 0 30% 0); }
                40% { transform: translate(3px, -2px) skewX(-1deg); clip-path: inset(50% 0 10% 0); }
                60% { transform: translate(-2px, -1px) skewX(0.5deg); clip-path: inset(20% 0 60% 0); }
                80% { transform: translate(2px, 1px) skewX(-0.5deg); clip-path: inset(70% 0 5% 0); }
                100% { transform: translate(0) skewX(0deg); clip-path: inset(0 0 0 0); }
            }
            .glitch-text {
                position: relative;
            }
            .glitch-text::before,
            .glitch-text::after {
                content: attr(data-text);
                position: absolute;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
            }
            .glitch-text::before {
                color: #ff0000;
                animation: textGlitch1 0.3s infinite;
                clip-path: polygon(0 0, 100% 0, 100% 45%, 0 45%);
            }
            .glitch-text::after {
                color: #00ffff;
                animation: textGlitch2 0.3s infinite;
                clip-path: polygon(0 55%, 100% 55%, 100% 100%, 0 100%);
            }
            @keyframes textGlitch1 {
                0%, 100% { transform: translate(0); }
                20% { transform: translate(-2px, 1px); }
                40% { transform: translate(2px, -1px); }
                60% { transform: translate(-1px, -1px); }
                80% { transform: translate(1px, 1px); }
            }
            @keyframes textGlitch2 {
                0%, 100% { transform: translate(0); }
                20% { transform: translate(2px, -1px); }
                40% { transform: translate(-2px, 1px); }
                60% { transform: translate(1px, 1px); }
                80% { transform: translate(-1px, -1px); }
            }
        `;
        document.head.appendChild(style);

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
    },

    _checkSystemState: function() {
        let maxValue = 0;

        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const text = el.textContent || '';
            const match = text.match(/(\d+(?:\.\d+)?)/);
            if (match) {
                const value = parseFloat(match[1]);
                if (value > maxValue) maxValue = value;
            }
        });

        // Set glitch intensity based on max value
        if (maxValue >= 90) {
            this._glitchIntensity = this._maxIntensity;
        } else if (maxValue >= this._threshold) {
            this._glitchIntensity = ((maxValue - this._threshold) / (90 - this._threshold)) * this._maxIntensity * 0.5;
        } else {
            this._glitchIntensity = 0;
        }

        return maxValue;
    },

    _generateSlices: function() {
        this._slices = [];
        const numSlices = Math.floor(5 + Math.random() * 10);
        const h = this._canvas.height;

        for (let i = 0; i < numSlices; i++) {
            this._slices.push({
                y: Math.random() * h,
                height: 2 + Math.random() * 20,
                offset: (Math.random() - 0.5) * 30 * this._glitchIntensity,
                color: Math.random() > 0.5 ? 'rgba(255, 0, 0, 0.3)' : 'rgba(0, 255, 255, 0.3)'
            });
        }
    },

    _drawGlitch: function() {
        const ctx = this._ctx;
        const w = this._canvas.width;
        const h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        if (!this._glitching || this._glitchIntensity === 0) return;

        // Draw horizontal slice artifacts
        this._slices.forEach(slice => {
            ctx.fillStyle = slice.color;
            ctx.fillRect(slice.offset, slice.y, w, slice.height);
        });

        // Random noise blocks
        const noiseCount = Math.floor(this._glitchIntensity * 20);
        for (let i = 0; i < noiseCount; i++) {
            const x = Math.random() * w;
            const y = Math.random() * h;
            const blockW = 5 + Math.random() * 50;
            const blockH = 2 + Math.random() * 10;

            ctx.fillStyle = `rgba(${Math.random() > 0.5 ? '255,0,0' : '0,255,255'}, ${Math.random() * 0.5})`;
            ctx.fillRect(x, y, blockW, blockH);
        }

        // Scanline interference
        if (Math.random() < this._glitchIntensity * 0.3) {
            const lineY = Math.random() * h;
            ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
            ctx.fillRect(0, lineY, w, 2);
        }
    },

    _updateWidgetGlitches: function(maxValue) {
        document.querySelectorAll('.lcd-widget').forEach(widget => {
            widget.classList.remove('glitching', 'critical-glitch');

            // Check this widget's value
            const valueEl = widget.querySelector('.lcd-stat-value');
            if (valueEl) {
                const text = valueEl.textContent || '';
                const match = text.match(/(\d+(?:\.\d+)?)/);
                if (match) {
                    const value = parseFloat(match[1]);
                    if (value >= 90) {
                        widget.classList.add('critical-glitch');
                    } else if (value >= this._threshold && Math.random() < 0.3) {
                        widget.classList.add('glitching');
                    }
                }
            }
        });
    },

    onBeforeRender: function(deltaTime) {
        this._time += deltaTime || 0.016;

        const maxValue = this._checkSystemState();

        // Trigger glitches randomly based on intensity
        if (this._time >= this._nextGlitchTime && this._glitchIntensity > 0) {
            this._glitching = true;
            this._glitchDuration = 0.05 + Math.random() * 0.1;
            this._generateSlices();
            this._nextGlitchTime = this._time + 0.2 + Math.random() * (1 - this._glitchIntensity);
        }

        // End glitch
        if (this._glitching && this._time >= this._nextGlitchTime - (0.2 - this._glitchDuration)) {
            this._glitching = false;
        }

        this._drawGlitch();
        this._updateWidgetGlitches(maxValue);
    },

    onWarning: function(element, level) {
        if (!element) return;

        if (level === 'critical') {
            element.classList.add('critical-glitch');
            // Force a glitch burst
            this._glitching = true;
            this._glitchIntensity = this._maxIntensity;
            this._generateSlices();

            setTimeout(() => {
                element.classList.remove('critical-glitch');
            }, 500);
        } else {
            element.classList.add('glitching');
            setTimeout(() => {
                element.classList.remove('glitching');
            }, 300);
        }
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();

        document.querySelectorAll('.glitching, .critical-glitch').forEach(el => {
            el.classList.remove('glitching', 'critical-glitch');
        });

        const style = document.getElementById('effect-glitch-style');
        if (style) style.remove();
    }
};
