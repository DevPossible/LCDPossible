/**
 * Fireflies Effect
 * Glowing particles drifting randomly.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _fireflies: [],
    _time: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] fireflies.onInit called', options);

        var count = options.count || 40;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-fireflies-canvas';
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

        for (var i = 0; i < count; i++) {
            this._fireflies.push(this._createFirefly());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createFirefly: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;
        return {
            x: Math.random() * w,
            y: Math.random() * h,
            targetX: Math.random() * w,
            targetY: Math.random() * h,
            size: 2 + Math.random() * 3,
            glowPhase: Math.random() * Math.PI * 2,
            glowSpeed: 1 + Math.random() * 2,
            speed: 20 + Math.random() * 30,
            hue: 50 + Math.random() * 30
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

        for (var i = 0; i < this._fireflies.length; i++) {
            var f = this._fireflies[i];
            f.glowPhase += f.glowSpeed * dt;

            var dx = f.targetX - f.x;
            var dy = f.targetY - f.y;
            var dist = Math.sqrt(dx * dx + dy * dy);

            if (dist < 10) {
                f.targetX = Math.random() * w;
                f.targetY = Math.random() * h;
            } else {
                f.x += (dx / dist) * f.speed * dt;
                f.y += (dy / dist) * f.speed * dt;
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._fireflies.length; i++) {
            var f = this._fireflies[i];
            var glow = (Math.sin(f.glowPhase) + 1) / 2;
            var alpha = 0.3 + glow * 0.7;

            ctx.beginPath();
            ctx.arc(f.x, f.y, f.size, 0, Math.PI * 2);
            ctx.fillStyle = 'hsla(' + f.hue + ', 100%, 70%, ' + alpha + ')';
            ctx.shadowColor = 'hsl(' + f.hue + ', 100%, 50%)';
            ctx.shadowBlur = 15 * glow;
            ctx.fill();
        }
        ctx.shadowBlur = 0;
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
