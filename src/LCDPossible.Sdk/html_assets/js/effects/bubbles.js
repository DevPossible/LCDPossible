/**
 * Bubbles Effect
 * Translucent bubbles floating upward.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _bubbles: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] bubbles.onInit called', options);

        var bubbleCount = options.bubbleCount || 30;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-bubbles-canvas';
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

        for (var i = 0; i < bubbleCount; i++) {
            this._bubbles.push(this._createBubble());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createBubble: function() {
        var h = this._canvas.height || window.innerHeight;
        var w = this._canvas.width || window.innerWidth;
        return {
            x: Math.random() * w,
            y: h + Math.random() * 100,
            radius: 10 + Math.random() * 30,
            speed: 30 + Math.random() * 50,
            wobble: Math.random() * Math.PI * 2,
            wobbleSpeed: 1 + Math.random() * 2,
            hue: 180 + Math.random() * 40
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

        for (var i = 0; i < this._bubbles.length; i++) {
            var b = this._bubbles[i];
            b.wobble += b.wobbleSpeed * dt;
            b.y -= b.speed * dt;
            b.x += Math.sin(b.wobble) * 20 * dt;

            if (b.y < -b.radius * 2) {
                b.y = h + b.radius;
                b.x = Math.random() * w;
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._bubbles.length; i++) {
            var b = this._bubbles[i];

            // Bubble body
            ctx.beginPath();
            ctx.arc(b.x, b.y, b.radius, 0, Math.PI * 2);
            ctx.strokeStyle = 'hsla(' + b.hue + ', 60%, 70%, 0.4)';
            ctx.lineWidth = 2;
            ctx.stroke();

            // Highlight
            ctx.beginPath();
            ctx.arc(b.x - b.radius * 0.3, b.y - b.radius * 0.3, b.radius * 0.2, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
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
