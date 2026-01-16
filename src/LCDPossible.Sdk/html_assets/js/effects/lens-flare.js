/**
 * Lens Flare Effect
 * Moving lens flare across screen.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _flareX: 0,
    _flareY: 0,
    _targetX: 0,
    _targetY: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] lens-flare.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-lens-flare-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 9990',
            'pointer-events: none',
            'mix-blend-mode: screen'
        ].join(';');
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        var w = this._canvas.width;
        var h = this._canvas.height;
        this._flareX = w * 0.7;
        this._flareY = h * 0.3;
        this._setNewTarget();

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _setNewTarget: function() {
        var w = this._canvas.width;
        var h = this._canvas.height;
        this._targetX = w * 0.2 + Math.random() * w * 0.6;
        this._targetY = h * 0.1 + Math.random() * h * 0.4;
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
        // Move towards target
        var dx = this._targetX - this._flareX;
        var dy = this._targetY - this._flareY;
        var dist = Math.sqrt(dx * dx + dy * dy);

        if (dist < 20) {
            this._setNewTarget();
        } else {
            this._flareX += dx * 0.5 * dt;
            this._flareY += dy * 0.5 * dt;
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        var centerX = w / 2;
        var centerY = h / 2;

        // Main flare
        this._drawFlare(this._flareX, this._flareY, 80, 'rgba(255, 200, 100, 0.3)');
        this._drawFlare(this._flareX, this._flareY, 40, 'rgba(255, 255, 200, 0.5)');

        // Artifacts along line to center
        var artifacts = 5;
        for (var i = 1; i <= artifacts; i++) {
            var t = i / (artifacts + 1);
            var x = this._flareX + (centerX - this._flareX) * t * 2;
            var y = this._flareY + (centerY - this._flareY) * t * 2;
            var size = 10 + Math.random() * 30;
            var hue = 40 + Math.random() * 20;
            this._drawFlare(x, y, size, 'hsla(' + hue + ', 100%, 70%, 0.2)');
        }

        // Anamorphic streak
        ctx.beginPath();
        var gradient = ctx.createLinearGradient(this._flareX - 200, this._flareY, this._flareX + 200, this._flareY);
        gradient.addColorStop(0, 'rgba(255, 200, 150, 0)');
        gradient.addColorStop(0.5, 'rgba(255, 200, 150, 0.3)');
        gradient.addColorStop(1, 'rgba(255, 200, 150, 0)');
        ctx.fillStyle = gradient;
        ctx.fillRect(this._flareX - 200, this._flareY - 3, 400, 6);
    },

    _drawFlare: function(x, y, radius, color) {
        var ctx = this._ctx;
        var gradient = ctx.createRadialGradient(x, y, 0, x, y, radius);
        gradient.addColorStop(0, color);
        gradient.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fillStyle = gradient;
        ctx.fill();
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
