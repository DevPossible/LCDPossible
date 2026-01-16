/**
 * Breathing Glow Effect
 * Pulsing ambient glow around the screen.
 */
window.LCDEffect = {
    _time: 0,
    _canvas: null,
    _ctx: null,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] breathing-glow.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-breathing-glow-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 1',
            'pointer-events: none'
        ].join(';');
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

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

        this._time += deltaTime;
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        var pulse = (Math.sin(this._time * 0.5) + 1) / 2;
        var alpha = 0.1 + pulse * 0.15;

        // Edge glow
        var edgeSize = 100 + pulse * 50;

        // Top edge
        var topGrad = ctx.createLinearGradient(0, 0, 0, edgeSize);
        topGrad.addColorStop(0, 'rgba(0, 200, 255, ' + alpha + ')');
        topGrad.addColorStop(1, 'transparent');
        ctx.fillStyle = topGrad;
        ctx.fillRect(0, 0, w, edgeSize);

        // Bottom edge
        var botGrad = ctx.createLinearGradient(0, h, 0, h - edgeSize);
        botGrad.addColorStop(0, 'rgba(0, 200, 255, ' + alpha + ')');
        botGrad.addColorStop(1, 'transparent');
        ctx.fillStyle = botGrad;
        ctx.fillRect(0, h - edgeSize, w, edgeSize);

        // Left edge
        var leftGrad = ctx.createLinearGradient(0, 0, edgeSize, 0);
        leftGrad.addColorStop(0, 'rgba(0, 200, 255, ' + alpha + ')');
        leftGrad.addColorStop(1, 'transparent');
        ctx.fillStyle = leftGrad;
        ctx.fillRect(0, 0, edgeSize, h);

        // Right edge
        var rightGrad = ctx.createLinearGradient(w, 0, w - edgeSize, 0);
        rightGrad.addColorStop(0, 'rgba(0, 200, 255, ' + alpha + ')');
        rightGrad.addColorStop(1, 'transparent');
        ctx.fillStyle = rightGrad;
        ctx.fillRect(w - edgeSize, 0, edgeSize, h);
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
