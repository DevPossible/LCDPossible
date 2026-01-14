/**
 * Film Grain Effect
 * Old film grain texture overlay with scratches.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] film-grain.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-film-grain-canvas';
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

        // Sparse grain dots - not full pixel noise, just scattered specks
        var grainCount = Math.floor((w * h) / 800);
        for (var i = 0; i < grainCount; i++) {
            var x = Math.random() * w;
            var y = Math.random() * h;
            var brightness = Math.random() > 0.5 ? 200 : 50;
            var alpha = 0.1 + Math.random() * 0.2;
            ctx.fillStyle = 'rgba(' + brightness + ',' + brightness + ',' + brightness + ',' + alpha + ')';
            ctx.fillRect(x, y, 1, 1);
        }

        // Occasional vertical scratches (film damage)
        if (Math.random() < 0.3) {
            var scratchX = Math.random() * w;
            var scratchLength = 50 + Math.random() * 200;
            var scratchY = Math.random() * (h - scratchLength);
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(scratchX, scratchY);
            ctx.lineTo(scratchX + (Math.random() - 0.5) * 3, scratchY + scratchLength);
            ctx.stroke();
        }

        // Occasional dust spots
        for (var d = 0; d < 3; d++) {
            if (Math.random() < 0.1) {
                var dustX = Math.random() * w;
                var dustY = Math.random() * h;
                var dustSize = 1 + Math.random() * 3;
                ctx.fillStyle = 'rgba(0, 0, 0, 0.2)';
                ctx.beginPath();
                ctx.arc(dustX, dustY, dustSize, 0, Math.PI * 2);
                ctx.fill();
            }
        }

        // Subtle vignette darkening at edges
        var gradient = ctx.createRadialGradient(w/2, h/2, 0, w/2, h/2, Math.max(w, h) * 0.7);
        gradient.addColorStop(0, 'transparent');
        gradient.addColorStop(0.7, 'transparent');
        gradient.addColorStop(1, 'rgba(0, 0, 0, 0.15)');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, w, h);

        // Frame flicker - occasional brightness shift
        if (Math.random() < 0.02) {
            ctx.fillStyle = 'rgba(255, 255, 255, 0.03)';
            ctx.fillRect(0, 0, w, h);
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
