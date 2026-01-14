/**
 * Bokeh Effect
 * Out-of-focus light circles drifting.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _circles: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] bokeh.onInit called', options);

        var circleCount = options.circleCount || 25;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-bokeh-canvas';
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

        for (var i = 0; i < circleCount; i++) {
            this._circles.push(this._createCircle());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createCircle: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;
        return {
            x: Math.random() * w,
            y: Math.random() * h,
            radius: 20 + Math.random() * 60,
            vx: (Math.random() - 0.5) * 15,
            vy: (Math.random() - 0.5) * 15,
            hue: Math.random() * 360,
            alpha: 0.1 + Math.random() * 0.2
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

        for (var i = 0; i < this._circles.length; i++) {
            var c = this._circles[i];
            c.x += c.vx * dt;
            c.y += c.vy * dt;

            // Wrap around
            if (c.x < -c.radius) c.x = w + c.radius;
            if (c.x > w + c.radius) c.x = -c.radius;
            if (c.y < -c.radius) c.y = h + c.radius;
            if (c.y > h + c.radius) c.y = -c.radius;
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._circles.length; i++) {
            var c = this._circles[i];

            var gradient = ctx.createRadialGradient(c.x, c.y, 0, c.x, c.y, c.radius);
            gradient.addColorStop(0, 'hsla(' + c.hue + ', 70%, 60%, ' + (c.alpha * 0.8) + ')');
            gradient.addColorStop(0.7, 'hsla(' + c.hue + ', 60%, 50%, ' + (c.alpha * 0.3) + ')');
            gradient.addColorStop(1, 'hsla(' + c.hue + ', 50%, 40%, 0)');

            ctx.beginPath();
            ctx.arc(c.x, c.y, c.radius, 0, Math.PI * 2);
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
