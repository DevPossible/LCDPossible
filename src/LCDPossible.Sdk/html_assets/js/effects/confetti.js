/**
 * Confetti Effect
 * Colorful confetti falling continuously.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _confetti: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] confetti.onInit called', options);

        var count = options.count || 80;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-confetti-canvas';
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
            this._confetti.push(this._createPiece());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createPiece: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;
        return {
            x: Math.random() * w,
            y: Math.random() * h - h,
            width: 5 + Math.random() * 8,
            height: 8 + Math.random() * 12,
            rotation: Math.random() * Math.PI * 2,
            rotationSpeed: (Math.random() - 0.5) * 5,
            speedY: 50 + Math.random() * 100,
            speedX: (Math.random() - 0.5) * 50,
            wobble: Math.random() * Math.PI * 2,
            wobbleSpeed: 2 + Math.random() * 3,
            hue: Math.random() * 360
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

        for (var i = 0; i < this._confetti.length; i++) {
            var c = this._confetti[i];
            c.wobble += c.wobbleSpeed * dt;
            c.rotation += c.rotationSpeed * dt;
            c.y += c.speedY * dt;
            c.x += c.speedX * dt + Math.sin(c.wobble) * 30 * dt;

            if (c.y > h + 20) {
                c.y = -20;
                c.x = Math.random() * w;
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._confetti.length; i++) {
            var c = this._confetti[i];

            ctx.save();
            ctx.translate(c.x, c.y);
            ctx.rotate(c.rotation);
            ctx.fillStyle = 'hsl(' + c.hue + ', 80%, 60%)';
            ctx.fillRect(-c.width / 2, -c.height / 2, c.width, c.height);
            ctx.restore();
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
