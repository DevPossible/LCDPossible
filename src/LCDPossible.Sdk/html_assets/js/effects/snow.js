/**
 * Snow Effect
 * Gentle snowflakes drifting down.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _flakes: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] snow.onInit called', options);

        var flakeCount = options.flakeCount || 100;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-snow-canvas';
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

        for (var i = 0; i < flakeCount; i++) {
            this._flakes.push(this._createFlake());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createFlake: function() {
        return {
            x: Math.random() * (this._canvas.width || window.innerWidth),
            y: Math.random() * (this._canvas.height || window.innerHeight),
            size: 1 + Math.random() * 4,
            speedY: 20 + Math.random() * 40,
            speedX: (Math.random() - 0.5) * 20,
            wobble: Math.random() * Math.PI * 2,
            wobbleSpeed: 1 + Math.random() * 2,
            opacity: 0.3 + Math.random() * 0.7
        };
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

        this._update(deltaTime);
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _update: function(dt) {
        var w = this._canvas.width;
        var h = this._canvas.height;

        for (var i = 0; i < this._flakes.length; i++) {
            var f = this._flakes[i];
            f.wobble += f.wobbleSpeed * dt;
            f.y += f.speedY * dt;
            f.x += f.speedX * dt + Math.sin(f.wobble) * 10 * dt;

            if (f.y > h) {
                f.y = -10;
                f.x = Math.random() * w;
            }
            if (f.x < 0) f.x = w;
            if (f.x > w) f.x = 0;
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._flakes.length; i++) {
            var f = this._flakes[i];
            ctx.beginPath();
            ctx.arc(f.x, f.y, f.size, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(255, 255, 255, ' + f.opacity + ')';
            ctx.fill();
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
