/**
 * Grid Pulse Effect
 * Grid lines pulse outward from center.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _gridSize: 50,
    _pulseSpeed: 1,

    onInit: function(options) {
        this._gridSize = options.gridSize || 50;
        this._pulseSpeed = options.pulseSpeed || 1;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-grid-pulse-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: -1;
            opacity: 0.3;
        `;
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
    },

    onBeforeRender: function(deltaTime) {
        if (!this._ctx) return;

        this._time += (deltaTime || 0.016) * this._pulseSpeed;

        const ctx = this._ctx;
        const w = this._canvas.width;
        const h = this._canvas.height;
        const centerX = w / 2;
        const centerY = h / 2;

        // Clear canvas
        ctx.fillStyle = 'rgba(0, 0, 0, 0.2)';
        ctx.fillRect(0, 0, w, h);

        // Calculate pulse wave position
        const maxDist = Math.sqrt(centerX * centerX + centerY * centerY);
        const pulsePos = (this._time * 100) % (maxDist + 200);

        // Draw grid lines
        ctx.strokeStyle = 'rgba(0, 200, 255, 0.3)';
        ctx.lineWidth = 1;

        // Vertical lines
        for (let x = this._gridSize; x < w; x += this._gridSize) {
            const dist = Math.abs(x - centerX);
            const pulseFactor = Math.max(0, 1 - Math.abs(dist - pulsePos) / 50);

            ctx.strokeStyle = `rgba(0, 200, 255, ${0.1 + pulseFactor * 0.5})`;
            ctx.lineWidth = 1 + pulseFactor * 2;

            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, h);
            ctx.stroke();
        }

        // Horizontal lines
        for (let y = this._gridSize; y < h; y += this._gridSize) {
            const dist = Math.abs(y - centerY);
            const pulseFactor = Math.max(0, 1 - Math.abs(dist - pulsePos) / 50);

            ctx.strokeStyle = `rgba(0, 200, 255, ${0.1 + pulseFactor * 0.5})`;
            ctx.lineWidth = 1 + pulseFactor * 2;

            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
            ctx.stroke();
        }

        // Draw pulse ring from center
        ctx.strokeStyle = `rgba(0, 255, 255, ${0.5 - (pulsePos / maxDist) * 0.4})`;
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(centerX, centerY, pulsePos, 0, Math.PI * 2);
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
