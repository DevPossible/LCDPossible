/**
 * VHS Static Effect
 * VHS tape noise/tracking lines overlay.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _trackingOffset: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] vhs-static.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-vhs-static-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 9990',
            'pointer-events: none'
        ].join(';');
        document.body.appendChild(this._canvas);

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

        // Scanlines - horizontal lines across the screen
        ctx.fillStyle = 'rgba(0, 0, 0, 0.15)';
        for (var y = 0; y < h; y += 3) {
            ctx.fillRect(0, y, w, 1);
        }

        // Sparse random static noise dots
        for (var i = 0; i < 200; i++) {
            var x = Math.random() * w;
            var y = Math.random() * h;
            var brightness = Math.random() > 0.5 ? 255 : 0;
            ctx.fillStyle = 'rgba(' + brightness + ',' + brightness + ',' + brightness + ', 0.3)';
            ctx.fillRect(x, y, 2, 2);
        }

        // Tracking distortion - occasional horizontal tear/shift
        if (Math.random() < 0.03) {
            this._trackingOffset = (Math.random() - 0.5) * 30;
        }
        this._trackingOffset *= 0.85;

        if (Math.abs(this._trackingOffset) > 2) {
            var tearY = Math.random() * h;
            var tearHeight = 2 + Math.random() * 8;
            ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
            ctx.fillRect(0, tearY, w, tearHeight);

            // Offset glitch bar
            ctx.fillStyle = 'rgba(0, 255, 255, 0.1)';
            ctx.fillRect(this._trackingOffset, tearY + tearHeight, w, 2);
        }

        // Occasional bright horizontal tear
        if (Math.random() < 0.01) {
            var brightY = Math.random() * h;
            var brightHeight = 3 + Math.random() * 10;
            ctx.fillStyle = 'rgba(255, 255, 255, 0.25)';
            ctx.fillRect(0, brightY, w, brightHeight);
        }

        // Rolling bar (like VHS tracking issue)
        var rollY = ((this._time * 50) % (h + 100)) - 50;
        ctx.fillStyle = 'rgba(255, 255, 255, 0.08)';
        ctx.fillRect(0, rollY, w, 30);
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
