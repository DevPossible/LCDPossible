/**
 * Particle Field Effect
 * Floating particles in the background with connection lines.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _particles: [],
    _particleCount: 50,
    _maxSpeed: 0.5,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] particle-field.onInit called', options);

        this._particleCount = options.particleCount || 50;
        this._maxSpeed = options.maxSpeed || 0.5;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-particle-field-canvas';
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

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        // Start animation loop
        this._lastFrameTime = performance.now();
        this._animate();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;

        // Create particles
        this._particles = [];
        for (var i = 0; i < this._particleCount; i++) {
            this._particles.push({
                x: Math.random() * this._canvas.width,
                y: Math.random() * this._canvas.height,
                vx: (Math.random() - 0.5) * this._maxSpeed * 60,
                vy: (Math.random() - 0.5) * this._maxSpeed * 60,
                radius: 1 + Math.random() * 3,
                alpha: 0.3 + Math.random() * 0.7,
                hue: 180 + Math.random() * 60
            });
        }
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

        for (var i = 0; i < this._particles.length; i++) {
            var p = this._particles[i];

            // Update position
            p.x += p.vx * dt;
            p.y += p.vy * dt;

            // Wrap around edges
            if (p.x < 0) p.x = w;
            if (p.x > w) p.x = 0;
            if (p.y < 0) p.y = h;
            if (p.y > h) p.y = 0;
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        // Fade effect
        ctx.fillStyle = 'rgba(0, 0, 0, 0.1)';
        ctx.fillRect(0, 0, w, h);

        // Draw particles
        for (var i = 0; i < this._particles.length; i++) {
            var p = this._particles[i];

            ctx.beginPath();
            ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
            ctx.fillStyle = 'hsla(' + p.hue + ', 80%, 60%, ' + p.alpha + ')';
            ctx.fill();
        }

        // Draw connections between nearby particles
        ctx.strokeStyle = 'rgba(0, 200, 255, 0.1)';
        ctx.lineWidth = 0.5;

        for (var i = 0; i < this._particles.length; i++) {
            for (var j = i + 1; j < this._particles.length; j++) {
                var dx = this._particles[i].x - this._particles[j].x;
                var dy = this._particles[i].y - this._particles[j].y;
                var dist = Math.sqrt(dx * dx + dy * dy);

                if (dist < 100) {
                    ctx.beginPath();
                    ctx.moveTo(this._particles[i].x, this._particles[i].y);
                    ctx.lineTo(this._particles[j].x, this._particles[j].y);
                    ctx.stroke();
                }
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
