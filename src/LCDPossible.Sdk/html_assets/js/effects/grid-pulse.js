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
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] grid-pulse.onInit called', options);

        this._gridSize = options.gridSize || 50;
        this._pulseSpeed = options.pulseSpeed || 1;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-grid-pulse-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 1',
            'opacity: 0.4'
        ].join(';');
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        // Start animation loop
        this._lastFrameTime = performance.now();
        this._animate();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime * this._pulseSpeed;
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;
        var centerX = w / 2;
        var centerY = h / 2;

        // Clear canvas
        ctx.fillStyle = 'rgba(0, 0, 0, 0.2)';
        ctx.fillRect(0, 0, w, h);

        // Calculate pulse wave position
        var maxDist = Math.sqrt(centerX * centerX + centerY * centerY);
        var pulsePos = (this._time * 20) % (maxDist + 200);

        // Draw grid lines
        ctx.lineWidth = 1;

        // Vertical lines
        for (var x = this._gridSize; x < w; x += this._gridSize) {
            var dist = Math.abs(x - centerX);
            var pulseFactor = Math.max(0, 1 - Math.abs(dist - pulsePos) / 50);

            ctx.strokeStyle = 'rgba(0, 200, 255, ' + (0.1 + pulseFactor * 0.5) + ')';
            ctx.lineWidth = 1 + pulseFactor * 2;

            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.lineTo(x, h);
            ctx.stroke();
        }

        // Horizontal lines
        for (var y = this._gridSize; y < h; y += this._gridSize) {
            var dist = Math.abs(y - centerY);
            var pulseFactor = Math.max(0, 1 - Math.abs(dist - pulsePos) / 50);

            ctx.strokeStyle = 'rgba(0, 200, 255, ' + (0.1 + pulseFactor * 0.5) + ')';
            ctx.lineWidth = 1 + pulseFactor * 2;

            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
            ctx.stroke();
        }

        // Draw pulse ring from center
        var ringAlpha = 0.5 - (pulsePos / maxDist) * 0.4;
        if (ringAlpha > 0) {
            ctx.strokeStyle = 'rgba(0, 255, 255, ' + ringAlpha + ')';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.arc(centerX, centerY, pulsePos, 0, Math.PI * 2);
            ctx.stroke();
        }
    },

    onBeforeRender: function(deltaTime) {},
    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},

    onDestroy: function() {
        if (this._animationId) {
            cancelAnimationFrame(this._animationId);
            this._animationId = null;
        }
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
