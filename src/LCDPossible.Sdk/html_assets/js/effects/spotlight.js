/**
 * Spotlight Effect
 * Roaming spotlight illuminates different widgets.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _spotlight: null,
    _time: 0,
    _targetWidget: null,
    _dwellTime: 3,

    onInit: function(options) {
        this._dwellTime = options.dwellTime || 3;

        // Create canvas overlay with mask
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-spotlight-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: 9996;
            pointer-events: none;
            mix-blend-mode: multiply;
        `;
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._spotlight = {
            x: this._canvas.width / 2,
            y: this._canvas.height / 2,
            targetX: this._canvas.width / 2,
            targetY: this._canvas.height / 2,
            radius: 150,
            lastSwitch: 0
        };

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);

        this._selectNewTarget();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
    },

    _selectNewTarget: function() {
        const widgets = document.querySelectorAll('.lcd-widget');
        if (widgets.length === 0) return;

        const randomWidget = widgets[Math.floor(Math.random() * widgets.length)];
        const rect = randomWidget.getBoundingClientRect();

        this._targetWidget = randomWidget;
        this._spotlight.targetX = rect.left + rect.width / 2;
        this._spotlight.targetY = rect.top + rect.height / 2;
        this._spotlight.radius = Math.max(rect.width, rect.height) * 0.8;
        this._spotlight.lastSwitch = this._time;
    },

    onBeforeRender: function(deltaTime) {
        this._time += deltaTime || 0.016;

        // Switch to new target periodically
        if (this._time - this._spotlight.lastSwitch > this._dwellTime) {
            this._selectNewTarget();
        }

        // Smooth movement
        const s = this._spotlight;
        s.x += (s.targetX - s.x) * 0.05;
        s.y += (s.targetY - s.y) * 0.05;

        const ctx = this._ctx;
        const w = this._canvas.width;
        const h = this._canvas.height;

        // Create dark overlay
        ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.fillRect(0, 0, w, h);

        // Create spotlight hole (using composite operation)
        ctx.globalCompositeOperation = 'destination-out';

        // Main spotlight
        const gradient = ctx.createRadialGradient(s.x, s.y, 0, s.x, s.y, s.radius);
        gradient.addColorStop(0, 'rgba(255, 255, 255, 1)');
        gradient.addColorStop(0.7, 'rgba(255, 255, 255, 0.8)');
        gradient.addColorStop(1, 'rgba(255, 255, 255, 0)');

        ctx.fillStyle = gradient;
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.radius, 0, Math.PI * 2);
        ctx.fill();

        // Reset composite operation
        ctx.globalCompositeOperation = 'source-over';

        // Add spotlight glow edge
        ctx.strokeStyle = 'rgba(255, 255, 200, 0.3)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.radius, 0, Math.PI * 2);
        ctx.stroke();
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
