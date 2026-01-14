/**
 * Rain Effect
 * Rain drops falling with splash effects.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _drops: [],
    _splashes: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] rain.onInit called', options);

        var dropCount = options.dropCount || 150;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-rain-canvas';
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

        for (var i = 0; i < dropCount; i++) {
            this._drops.push(this._createDrop());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createDrop: function() {
        return {
            x: Math.random() * (this._canvas.width || window.innerWidth),
            y: Math.random() * (this._canvas.height || window.innerHeight),
            length: 10 + Math.random() * 20,
            speed: 400 + Math.random() * 300,
            opacity: 0.2 + Math.random() * 0.3
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
        var h = this._canvas.height;
        var w = this._canvas.width;

        for (var i = 0; i < this._drops.length; i++) {
            var d = this._drops[i];
            d.y += d.speed * dt;

            if (d.y > h) {
                // Create splash
                this._splashes.push({
                    x: d.x,
                    y: h - 5,
                    radius: 0,
                    maxRadius: 5 + Math.random() * 5,
                    opacity: 0.5
                });
                d.y = -d.length;
                d.x = Math.random() * w;
            }
        }

        this._splashes = this._splashes.filter(function(s) {
            s.radius += 30 * dt;
            s.opacity -= 2 * dt;
            return s.opacity > 0;
        });
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        // Draw drops
        ctx.strokeStyle = 'rgba(150, 200, 255, 0.4)';
        ctx.lineWidth = 1;
        for (var i = 0; i < this._drops.length; i++) {
            var d = this._drops[i];
            ctx.beginPath();
            ctx.moveTo(d.x, d.y);
            ctx.lineTo(d.x, d.y + d.length);
            ctx.globalAlpha = d.opacity;
            ctx.stroke();
        }
        ctx.globalAlpha = 1;

        // Draw splashes
        ctx.strokeStyle = 'rgba(150, 200, 255, 0.5)';
        for (var i = 0; i < this._splashes.length; i++) {
            var s = this._splashes[i];
            ctx.beginPath();
            ctx.arc(s.x, s.y, s.radius, Math.PI, Math.PI * 2);
            ctx.globalAlpha = s.opacity;
            ctx.stroke();
        }
        ctx.globalAlpha = 1;
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
