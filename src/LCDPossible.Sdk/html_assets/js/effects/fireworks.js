/**
 * Fireworks Effect
 * Colorful fireworks exploding in the background.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _rockets: [],
    _particles: [],
    _launchInterval: 1.5,
    _timeSinceLastLaunch: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] fireworks.onInit called', options);

        this._launchInterval = options.launchInterval || 1.5;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-fireworks-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 1',
            'opacity: 0.6'
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

    _launchRocket: function() {
        var w = this._canvas.width;
        var h = this._canvas.height;

        this._rockets.push({
            x: w * 0.1 + Math.random() * w * 0.8,
            y: h,
            vx: (Math.random() - 0.5) * 50,
            vy: -200 - Math.random() * 150,
            hue: Math.random() * 360,
            trail: []
        });
    },

    _explode: function(rocket) {
        var particleCount = 60 + Math.floor(Math.random() * 40);
        var hue = rocket.hue;

        for (var i = 0; i < particleCount; i++) {
            var angle = (i / particleCount) * Math.PI * 2;
            var speed = 50 + Math.random() * 150;
            var spread = Math.random();

            this._particles.push({
                x: rocket.x,
                y: rocket.y,
                vx: Math.cos(angle) * speed * spread,
                vy: Math.sin(angle) * speed * spread,
                hue: hue + (Math.random() - 0.5) * 30,
                life: 1,
                decay: 0.6 + Math.random() * 0.4,
                size: 2 + Math.random() * 2
            });
        }
    },

    _update: function(dt) {
        var self = this;
        var h = this._canvas.height;

        // Launch new rockets
        this._timeSinceLastLaunch += dt;
        if (this._timeSinceLastLaunch >= this._launchInterval) {
            this._launchRocket();
            // Sometimes launch multiple
            if (Math.random() < 0.3) {
                setTimeout(function() { self._launchRocket(); }, 100 + Math.random() * 200);
            }
            this._timeSinceLastLaunch = 0;
        }

        // Update rockets
        this._rockets = this._rockets.filter(function(rocket) {
            rocket.x += rocket.vx * dt;
            rocket.y += rocket.vy * dt;
            rocket.vy += 150 * dt; // gravity

            // Add trail point
            rocket.trail.push({ x: rocket.x, y: rocket.y });
            if (rocket.trail.length > 10) {
                rocket.trail.shift();
            }

            // Explode when velocity slows
            if (rocket.vy > -20) {
                self._explode(rocket);
                return false;
            }

            return rocket.y > 0;
        });

        // Update particles
        this._particles = this._particles.filter(function(p) {
            p.x += p.vx * dt;
            p.y += p.vy * dt;
            p.vy += 80 * dt; // gravity
            p.vx *= 0.98;
            p.vy *= 0.98;
            p.life -= p.decay * dt;
            return p.life > 0;
        });
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        // Fade effect
        ctx.fillStyle = 'rgba(0, 0, 0, 0.15)';
        ctx.fillRect(0, 0, w, h);

        // Draw rocket trails
        for (var i = 0; i < this._rockets.length; i++) {
            var rocket = this._rockets[i];

            if (rocket.trail.length > 1) {
                ctx.beginPath();
                ctx.moveTo(rocket.trail[0].x, rocket.trail[0].y);
                for (var j = 1; j < rocket.trail.length; j++) {
                    ctx.lineTo(rocket.trail[j].x, rocket.trail[j].y);
                }
                ctx.strokeStyle = 'rgba(255, 200, 100, 0.8)';
                ctx.lineWidth = 2;
                ctx.stroke();
            }

            // Draw rocket head
            ctx.beginPath();
            ctx.arc(rocket.x, rocket.y, 3, 0, Math.PI * 2);
            ctx.fillStyle = '#fff';
            ctx.fill();
        }

        // Draw particles
        for (var i = 0; i < this._particles.length; i++) {
            var p = this._particles[i];
            var alpha = p.life;

            ctx.beginPath();
            ctx.arc(p.x, p.y, p.size * p.life, 0, Math.PI * 2);
            ctx.fillStyle = 'hsla(' + p.hue + ', 100%, 60%, ' + alpha + ')';
            ctx.fill();

            // Glow
            ctx.shadowColor = 'hsl(' + p.hue + ', 100%, 60%)';
            ctx.shadowBlur = 10 * p.life;
            ctx.fill();
            ctx.shadowBlur = 0;
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
