/**
 * Clouds Effect
 * Slow-moving clouds drifting across.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _clouds: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] clouds.onInit called', options);

        var cloudCount = options.cloudCount || 6;

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-clouds-canvas';
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

        for (var i = 0; i < cloudCount; i++) {
            this._clouds.push(this._createCloud());
        }

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _createCloud: function() {
        var w = this._canvas.width || window.innerWidth;
        var h = this._canvas.height || window.innerHeight;

        var puffs = [];
        var puffCount = 4 + Math.floor(Math.random() * 4);
        for (var i = 0; i < puffCount; i++) {
            puffs.push({
                offsetX: (Math.random() - 0.5) * 100,
                offsetY: (Math.random() - 0.5) * 30,
                radius: 30 + Math.random() * 50
            });
        }

        return {
            x: Math.random() * (w + 200) - 100,
            y: h * 0.1 + Math.random() * h * 0.4,
            speed: 5 + Math.random() * 15,
            alpha: 0.15 + Math.random() * 0.15,
            puffs: puffs
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

        for (var i = 0; i < this._clouds.length; i++) {
            var c = this._clouds[i];
            c.x += c.speed * dt;

            if (c.x > w + 150) {
                c.x = -150;
                c.y = this._canvas.height * 0.1 + Math.random() * this._canvas.height * 0.4;
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._clouds.length; i++) {
            var c = this._clouds[i];

            for (var j = 0; j < c.puffs.length; j++) {
                var p = c.puffs[j];
                var x = c.x + p.offsetX;
                var y = c.y + p.offsetY;

                var gradient = ctx.createRadialGradient(x, y, 0, x, y, p.radius);
                gradient.addColorStop(0, 'rgba(255, 255, 255, ' + c.alpha + ')');
                gradient.addColorStop(0.5, 'rgba(240, 240, 240, ' + (c.alpha * 0.6) + ')');
                gradient.addColorStop(1, 'rgba(220, 220, 220, 0)');

                ctx.beginPath();
                ctx.arc(x, y, p.radius, 0, Math.PI * 2);
                ctx.fillStyle = gradient;
                ctx.fill();
            }
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
