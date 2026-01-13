/**
 * Particle Field Effect
 * Floating particles in the background.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _particles: [],
    _particleCount: 50,
    _maxSpeed: 0.5,

    onInit: function(options) {
        this._particleCount = options.particleCount || 50;
        this._maxSpeed = options.maxSpeed || 0.5;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-particle-field-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: -1;
            opacity: 0.4;
        `;
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;

        // Create particles
        this._particles = [];
        for (let i = 0; i < this._particleCount; i++) {
            this._particles.push({
                x: Math.random() * this._canvas.width,
                y: Math.random() * this._canvas.height,
                vx: (Math.random() - 0.5) * this._maxSpeed,
                vy: (Math.random() - 0.5) * this._maxSpeed,
                radius: 1 + Math.random() * 3,
                alpha: 0.3 + Math.random() * 0.7,
                hue: 180 + Math.random() * 60 // Cyan to blue range
            });
        }
    },

    onBeforeRender: function(deltaTime) {
        if (!this._ctx) return;

        const dt = (deltaTime || 0.016) * 60; // Normalize to 60fps

        // Clear canvas
        this._ctx.fillStyle = 'rgba(0, 0, 0, 0.1)';
        this._ctx.fillRect(0, 0, this._canvas.width, this._canvas.height);

        // Update and draw particles
        this._particles.forEach(p => {
            // Update position
            p.x += p.vx * dt;
            p.y += p.vy * dt;

            // Wrap around edges
            if (p.x < 0) p.x = this._canvas.width;
            if (p.x > this._canvas.width) p.x = 0;
            if (p.y < 0) p.y = this._canvas.height;
            if (p.y > this._canvas.height) p.y = 0;

            // Draw particle
            this._ctx.beginPath();
            this._ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
            this._ctx.fillStyle = `hsla(${p.hue}, 80%, 60%, ${p.alpha})`;
            this._ctx.fill();
        });

        // Draw connections between nearby particles
        this._ctx.strokeStyle = 'rgba(0, 200, 255, 0.1)';
        this._ctx.lineWidth = 0.5;
        for (let i = 0; i < this._particles.length; i++) {
            for (let j = i + 1; j < this._particles.length; j++) {
                const dx = this._particles[i].x - this._particles[j].x;
                const dy = this._particles[i].y - this._particles[j].y;
                const dist = Math.sqrt(dx * dx + dy * dy);

                if (dist < 100) {
                    this._ctx.beginPath();
                    this._ctx.moveTo(this._particles[i].x, this._particles[i].y);
                    this._ctx.lineTo(this._particles[j].x, this._particles[j].y);
                    this._ctx.stroke();
                }
            }
        }
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
