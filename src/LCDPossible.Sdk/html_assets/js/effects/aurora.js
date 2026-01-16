/**
 * Aurora Effect
 * Northern lights with flowing color ribbons.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _ribbons: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] aurora.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-aurora-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 1',
            'opacity: 0.5'
        ].join(';');
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        // Create ribbons
        for (var i = 0; i < 5; i++) {
            this._ribbons.push({
                baseY: 0.1 + Math.random() * 0.3,
                amplitude: 0.05 + Math.random() * 0.1,
                frequency: 0.5 + Math.random() * 1,
                phase: Math.random() * Math.PI * 2,
                hue: 120 + Math.random() * 60,
                speed: 0.2 + Math.random() * 0.3
            });
        }

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

        ctx.fillStyle = 'rgba(0, 0, 0, 0.05)';
        ctx.fillRect(0, 0, w, h);

        for (var r = 0; r < this._ribbons.length; r++) {
            var ribbon = this._ribbons[r];
            var baseY = ribbon.baseY * h;

            ctx.beginPath();
            ctx.moveTo(0, baseY);

            for (var x = 0; x <= w; x += 10) {
                var wave1 = Math.sin((x / w) * Math.PI * ribbon.frequency + this._time * ribbon.speed + ribbon.phase);
                var wave2 = Math.sin((x / w) * Math.PI * ribbon.frequency * 2 + this._time * ribbon.speed * 0.7);
                var y = baseY + (wave1 * 0.7 + wave2 * 0.3) * ribbon.amplitude * h;
                ctx.lineTo(x, y);
            }

            ctx.lineTo(w, h);
            ctx.lineTo(0, h);
            ctx.closePath();

            var gradient = ctx.createLinearGradient(0, baseY - ribbon.amplitude * h, 0, h);
            gradient.addColorStop(0, 'hsla(' + ribbon.hue + ', 80%, 50%, 0.3)');
            gradient.addColorStop(0.5, 'hsla(' + (ribbon.hue + 30) + ', 70%, 40%, 0.1)');
            gradient.addColorStop(1, 'transparent');
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
