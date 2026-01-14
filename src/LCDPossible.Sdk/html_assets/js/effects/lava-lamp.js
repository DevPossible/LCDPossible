/**
 * Lava Lamp Effect
 * Blobby colored blobs floating.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _blobs: [],
    _time: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] lava-lamp.onInit called', options);

        var blobCount = options.blobCount || 8;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-lava-lamp-canvas';
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

        for (var i = 0; i < blobCount; i++) {
            this._blobs.push(this._createBlob());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createBlob: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;
        return {
            x: Math.random() * w,
            y: Math.random() * h,
            radius: 40 + Math.random() * 80,
            vx: (Math.random() - 0.5) * 20,
            vy: -10 - Math.random() * 20,
            hue: Math.random() * 360,
            wobblePhase: Math.random() * Math.PI * 2
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

        this._time += deltaTime;
        this._update(deltaTime);
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _update: function(dt) {
        var w = this._canvas.width;
        var h = this._canvas.height;

        for (var i = 0; i < this._blobs.length; i++) {
            var b = this._blobs[i];
            b.wobblePhase += dt;
            b.x += b.vx * dt + Math.sin(b.wobblePhase) * 10 * dt;
            b.y += b.vy * dt;

            // Bounce off edges
            if (b.x < b.radius) { b.x = b.radius; b.vx *= -1; }
            if (b.x > w - b.radius) { b.x = w - b.radius; b.vx *= -1; }

            // Wrap vertically with heat simulation
            if (b.y < -b.radius) {
                b.y = h + b.radius;
                b.vy = -10 - Math.random() * 20;
            }
            if (b.y > h + b.radius) {
                b.vy = -10 - Math.random() * 20;
            }

            // Slow down when at top
            if (b.y < h * 0.3) {
                b.vy += 15 * dt;
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.fillStyle = 'rgba(0, 0, 0, 0.1)';
        ctx.fillRect(0, 0, w, h);

        for (var i = 0; i < this._blobs.length; i++) {
            var b = this._blobs[i];
            var wobble = Math.sin(b.wobblePhase * 2) * 0.2;

            var gradient = ctx.createRadialGradient(b.x, b.y, 0, b.x, b.y, b.radius);
            gradient.addColorStop(0, 'hsla(' + b.hue + ', 80%, 60%, 0.8)');
            gradient.addColorStop(0.5, 'hsla(' + b.hue + ', 70%, 50%, 0.4)');
            gradient.addColorStop(1, 'hsla(' + b.hue + ', 60%, 40%, 0)');

            ctx.beginPath();
            ctx.ellipse(b.x, b.y, b.radius * (1 + wobble), b.radius * (1 - wobble), 0, 0, Math.PI * 2);
            ctx.fillStyle = gradient;
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
