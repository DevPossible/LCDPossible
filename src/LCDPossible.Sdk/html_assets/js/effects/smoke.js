/**
 * Smoke Effect
 * Wispy smoke tendrils rising.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _particles: [],
    _emitters: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] smoke.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-smoke-canvas';
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

        // Create emitters at bottom
        var w = this._canvas.width;
        var h = this._canvas.height;
        for (var i = 0; i < 4; i++) {
            this._emitters.push({
                x: w * 0.2 + Math.random() * w * 0.6,
                y: h,
                rate: 0.05 + Math.random() * 0.05,
                timer: 0
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

        this._update(deltaTime);
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _update: function(dt) {
        var self = this;
        var h = this._canvas.height;

        // Emit new particles
        for (var i = 0; i < this._emitters.length; i++) {
            var e = this._emitters[i];
            e.timer += dt;
            if (e.timer >= e.rate) {
                e.timer = 0;
                this._particles.push({
                    x: e.x + (Math.random() - 0.5) * 20,
                    y: e.y,
                    vx: (Math.random() - 0.5) * 30,
                    vy: -40 - Math.random() * 30,
                    size: 20 + Math.random() * 30,
                    life: 1,
                    rotation: Math.random() * Math.PI * 2,
                    rotationSpeed: (Math.random() - 0.5) * 0.5
                });
            }
        }

        // Update particles
        this._particles = this._particles.filter(function(p) {
            p.x += p.vx * dt;
            p.y += p.vy * dt;
            p.vx += (Math.random() - 0.5) * 50 * dt;
            p.size += 20 * dt;
            p.life -= 0.3 * dt;
            p.rotation += p.rotationSpeed * dt;
            return p.life > 0 && p.y > -p.size;
        });
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        for (var i = 0; i < this._particles.length; i++) {
            var p = this._particles[i];
            var alpha = p.life * 0.3;

            var gradient = ctx.createRadialGradient(p.x, p.y, 0, p.x, p.y, p.size);
            gradient.addColorStop(0, 'rgba(150, 150, 150, ' + alpha + ')');
            gradient.addColorStop(0.5, 'rgba(100, 100, 100, ' + (alpha * 0.5) + ')');
            gradient.addColorStop(1, 'rgba(80, 80, 80, 0)');

            ctx.beginPath();
            ctx.arc(p.x, p.y, p.size, 0, Math.PI * 2);
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
