/**
 * Embers Effect
 * Glowing embers floating upward.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _embers: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] embers.onInit called', options);

        var count = options.count || 60;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-embers-canvas';
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
            this._embers.push(this._createEmber());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createEmber: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;
        return {
            x: Math.random() * w,
            y: h + Math.random() * 50,
            size: 1 + Math.random() * 3,
            speedY: -30 - Math.random() * 50,
            speedX: (Math.random() - 0.5) * 30,
            wobble: Math.random() * Math.PI * 2,
            wobbleSpeed: 2 + Math.random() * 3,
            life: 0.5 + Math.random() * 0.5,
            hue: 15 + Math.random() * 30
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
        var self = this;
        var w = this._canvas.width;
        var h = this._canvas.height;

        for (var i = 0; i < this._embers.length; i++) {
            var e = this._embers[i];
            e.wobble += e.wobbleSpeed * dt;
            e.y += e.speedY * dt;
            e.x += e.speedX * dt + Math.sin(e.wobble) * 20 * dt;
            e.life -= 0.15 * dt;
            e.size *= 0.998;

            if (e.y < -20 || e.life <= 0 || e.size < 0.5) {
                this._embers[i] = this._createEmber();
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._embers.length; i++) {
            var e = this._embers[i];
            var alpha = e.life;

            ctx.beginPath();
            ctx.arc(e.x, e.y, e.size, 0, Math.PI * 2);
            ctx.fillStyle = 'hsla(' + e.hue + ', 100%, 60%, ' + alpha + ')';
            ctx.shadowColor = 'hsl(' + e.hue + ', 100%, 50%)';
            ctx.shadowBlur = 10;
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
