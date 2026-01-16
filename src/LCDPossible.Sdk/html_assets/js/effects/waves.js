/**
 * Waves Effect
 * Ocean waves flowing at bottom.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _time: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] waves.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-waves-canvas';
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

        this._time += deltaTime * 0.5;
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        var waveConfigs = [
            { baseY: 0.85, amplitude: 15, frequency: 0.01, speed: 1, alpha: 0.3, hue: 200 },
            { baseY: 0.88, amplitude: 12, frequency: 0.015, speed: 1.3, alpha: 0.25, hue: 210 },
            { baseY: 0.91, amplitude: 10, frequency: 0.02, speed: 0.8, alpha: 0.2, hue: 220 },
            { baseY: 0.94, amplitude: 8, frequency: 0.025, speed: 1.5, alpha: 0.15, hue: 195 }
        ];

        for (var i = 0; i < waveConfigs.length; i++) {
            var wave = waveConfigs[i];
            var baseY = h * wave.baseY;

            ctx.beginPath();
            ctx.moveTo(0, h);

            for (var x = 0; x <= w; x += 5) {
                var y = baseY +
                    Math.sin(x * wave.frequency + this._time * wave.speed) * wave.amplitude +
                    Math.sin(x * wave.frequency * 2 + this._time * wave.speed * 0.7) * wave.amplitude * 0.5;
                ctx.lineTo(x, y);
            }

            ctx.lineTo(w, h);
            ctx.closePath();

            var gradient = ctx.createLinearGradient(0, baseY - wave.amplitude, 0, h);
            gradient.addColorStop(0, 'hsla(' + wave.hue + ', 70%, 50%, ' + wave.alpha + ')');
            gradient.addColorStop(1, 'hsla(' + wave.hue + ', 60%, 30%, ' + (wave.alpha * 0.5) + ')');
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
