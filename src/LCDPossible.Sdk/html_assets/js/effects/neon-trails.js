/**
 * Neon Trails Effect
 * Neon light trails follow value changes.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _trails: [],
    _time: 0,

    onInit: function(options) {
        this._trailLength = options.trailLength || 20;
        this._trailColor = options.trailColor || '#00d4ff';

        // Create canvas overlay
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-neon-trails-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: 9995;
            pointer-events: none;
        `;
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
    },

    _createTrail: function(x, y, color) {
        const angle = Math.random() * Math.PI * 2;
        const speed = 100 + Math.random() * 150;

        this._trails.push({
            points: [{ x, y }],
            vx: Math.cos(angle) * speed,
            vy: Math.sin(angle) * speed,
            color: color || this._trailColor,
            life: 1,
            maxPoints: this._trailLength
        });
    },

    _updateTrails: function(deltaTime) {
        const dt = deltaTime || 0.016;

        this._trails = this._trails.filter(trail => {
            // Update head position
            const head = trail.points[0];
            const newX = head.x + trail.vx * dt;
            const newY = head.y + trail.vy * dt;

            // Add new point at head
            trail.points.unshift({ x: newX, y: newY });

            // Trim tail
            if (trail.points.length > trail.maxPoints) {
                trail.points.pop();
            }

            // Apply friction and gravity
            trail.vx *= 0.98;
            trail.vy *= 0.98;
            trail.vy += 50 * dt; // Slight gravity

            // Fade out
            trail.life -= dt * 0.5;

            // Bounce off edges
            if (newX < 0 || newX > this._canvas.width) trail.vx *= -0.8;
            if (newY < 0 || newY > this._canvas.height) trail.vy *= -0.8;

            return trail.life > 0;
        });
    },

    _drawTrails: function() {
        const ctx = this._ctx;
        ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);

        this._trails.forEach(trail => {
            if (trail.points.length < 2) return;

            ctx.beginPath();
            ctx.moveTo(trail.points[0].x, trail.points[0].y);

            for (let i = 1; i < trail.points.length; i++) {
                ctx.lineTo(trail.points[i].x, trail.points[i].y);
            }

            // Create gradient along trail
            const gradient = ctx.createLinearGradient(
                trail.points[0].x, trail.points[0].y,
                trail.points[trail.points.length - 1].x,
                trail.points[trail.points.length - 1].y
            );
            gradient.addColorStop(0, trail.color);
            gradient.addColorStop(1, 'transparent');

            ctx.strokeStyle = gradient;
            ctx.lineWidth = 3 * trail.life;
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';

            // Glow effect
            ctx.shadowColor = trail.color;
            ctx.shadowBlur = 15 * trail.life;

            ctx.stroke();

            // Draw bright head
            ctx.beginPath();
            ctx.arc(trail.points[0].x, trail.points[0].y, 5 * trail.life, 0, Math.PI * 2);
            ctx.fillStyle = '#ffffff';
            ctx.fill();
        });

        ctx.shadowBlur = 0;
    },

    onValueChange: function(change) {
        const el = change.element;
        if (!el) return;

        const rect = el.getBoundingClientRect();
        const x = rect.left + rect.width / 2;
        const y = rect.top + rect.height / 2;

        // Get color based on value
        let color = this._trailColor;
        if (change.newValue !== undefined) {
            const val = parseFloat(change.newValue);
            if (val >= 90) color = '#ff0000';
            else if (val >= 70) color = '#ffaa00';
            else if (val >= 50) color = '#00ff00';
        }

        // Create multiple trails
        for (let i = 0; i < 3; i++) {
            this._createTrail(x, y, color);
        }
    },

    onBeforeRender: function(deltaTime) {
        this._time += deltaTime || 0.016;
        this._updateTrails(deltaTime);
        this._drawTrails();
    },

    onAfterRender: function(deltaTime) {},
    onWarning: function(element, level) {
        if (!element) return;

        const rect = element.getBoundingClientRect();
        const x = rect.left + rect.width / 2;
        const y = rect.top + rect.height / 2;

        const color = level === 'critical' ? '#ff0000' : '#ffaa00';

        // Burst of trails
        for (let i = 0; i < 5; i++) {
            this._createTrail(x, y, color);
        }
    },
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
