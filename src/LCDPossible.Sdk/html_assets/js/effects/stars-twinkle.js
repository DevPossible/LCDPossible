/**
 * Stars Twinkle Effect
 * Stationary twinkling starfield.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _stars: [],
    _time: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] stars-twinkle.onInit called', options);

        var starCount = options.starCount || 150;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-stars-twinkle-canvas';
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

        for (var i = 0; i < starCount; i++) {
            this._stars.push(this._createStar());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createStar: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;
        return {
            x: Math.random() * w,
            y: Math.random() * h,
            size: 0.5 + Math.random() * 2,
            twinklePhase: Math.random() * Math.PI * 2,
            twinkleSpeed: 0.5 + Math.random() * 2,
            baseAlpha: 0.3 + Math.random() * 0.5
        };
    },

    _resize: function() {
        var oldW = this._canvas.width;
        var oldH = this._canvas.height;
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;

        // Reposition stars if resized
        if (oldW && oldH) {
            var scaleX = this._canvas.width / oldW;
            var scaleY = this._canvas.height / oldH;
            for (var i = 0; i < this._stars.length; i++) {
                this._stars[i].x *= scaleX;
                this._stars[i].y *= scaleY;
            }
        }
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
        for (var i = 0; i < this._stars.length; i++) {
            var s = this._stars[i];
            s.twinklePhase += s.twinkleSpeed * dt;
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._stars.length; i++) {
            var s = this._stars[i];
            var twinkle = (Math.sin(s.twinklePhase) + 1) / 2;
            var alpha = s.baseAlpha * (0.3 + twinkle * 0.7);

            ctx.beginPath();
            ctx.arc(s.x, s.y, s.size * (0.8 + twinkle * 0.4), 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(255, 255, 255, ' + alpha + ')';
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
