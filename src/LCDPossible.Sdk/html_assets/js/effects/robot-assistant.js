/**
 * Robot Assistant Effect
 * Cute robot points at important metrics.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _robot: null,
    _time: 0,
    _targetWidget: null,
    _highlightValue: null,

    onInit: function(options) {
        // Create canvas overlay
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-robot-assistant-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: 9998;
            pointer-events: none;
        `;
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._robot = {
            x: 50,
            y: this._canvas.height - 100,
            targetX: 50,
            eyeOffset: 0,
            armAngle: 0
        };

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);

        this._findImportantMetric();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
        if (this._robot) {
            this._robot.y = this._canvas.height - 100;
        }
    },

    _findImportantMetric: function() {
        // Find the highest value widget (most "important")
        let maxValue = 0;
        let importantWidget = null;

        document.querySelectorAll('.lcd-widget').forEach(widget => {
            const valueEl = widget.querySelector('.lcd-stat-value');
            if (valueEl) {
                const text = valueEl.textContent || '';
                const match = text.match(/(\d+(?:\.\d+)?)/);
                if (match) {
                    const value = parseFloat(match[1]);
                    if (value > maxValue) {
                        maxValue = value;
                        importantWidget = widget;
                        this._highlightValue = value;
                    }
                }
            }
        });

        if (importantWidget) {
            this._targetWidget = importantWidget;
            const rect = importantWidget.getBoundingClientRect();
            this._robot.targetX = Math.min(
                Math.max(rect.left - 80, 50),
                this._canvas.width - 100
            );
        }
    },

    _drawRobot: function() {
        const ctx = this._ctx;
        const r = this._robot;

        // Move towards target
        r.x += (r.targetX - r.x) * 0.05;

        ctx.save();
        ctx.translate(r.x, r.y);

        // Hover animation
        const hover = Math.sin(this._time * 2) * 3;
        ctx.translate(0, hover);

        // Body
        ctx.fillStyle = '#4a5568';
        ctx.strokeStyle = '#00d4ff';
        ctx.lineWidth = 2;

        // Rounded body
        this._roundRect(ctx, -25, -50, 50, 60, 10);
        ctx.fill();
        ctx.stroke();

        // Head
        ctx.fillStyle = '#2d3748';
        this._roundRect(ctx, -30, -90, 60, 45, 8);
        ctx.fill();
        ctx.stroke();

        // Antenna
        ctx.beginPath();
        ctx.moveTo(0, -90);
        ctx.lineTo(0, -105);
        ctx.stroke();
        ctx.fillStyle = '#ff0';
        ctx.beginPath();
        ctx.arc(0, -108, 5, 0, Math.PI * 2);
        ctx.fill();

        // Eyes
        r.eyeOffset = Math.sin(this._time * 0.5) * 3;
        ctx.fillStyle = '#00d4ff';
        ctx.beginPath();
        ctx.arc(-12 + r.eyeOffset, -70, 8, 0, Math.PI * 2);
        ctx.arc(12 + r.eyeOffset, -70, 8, 0, Math.PI * 2);
        ctx.fill();

        // Eye glow
        ctx.shadowColor = '#00d4ff';
        ctx.shadowBlur = 10;
        ctx.fill();
        ctx.shadowBlur = 0;

        // Mouth (LED display)
        ctx.fillStyle = '#00ff00';
        ctx.fillRect(-15, -55, 30, 5);

        // Arms
        ctx.strokeStyle = '#4a5568';
        ctx.lineWidth = 8;
        ctx.lineCap = 'round';

        // Left arm (pointing)
        if (this._targetWidget) {
            const rect = this._targetWidget.getBoundingClientRect();
            const targetY = rect.top + rect.height / 2 - r.y;
            const targetX = rect.left + rect.width / 2 - r.x;
            r.armAngle = Math.atan2(targetY + 60, targetX);
        }

        ctx.beginPath();
        ctx.moveTo(25, -30);
        const armLen = 50;
        const armX = 25 + Math.cos(r.armAngle) * armLen;
        const armY = -30 + Math.sin(r.armAngle) * armLen;
        ctx.lineTo(armX, armY);
        ctx.stroke();

        // Pointing finger
        ctx.fillStyle = '#4a5568';
        ctx.beginPath();
        ctx.arc(armX, armY, 8, 0, Math.PI * 2);
        ctx.fill();

        // Right arm (waving)
        ctx.beginPath();
        ctx.moveTo(-25, -30);
        const waveAngle = Math.sin(this._time * 3) * 0.3 - Math.PI / 4;
        ctx.lineTo(-25 + Math.cos(waveAngle) * 40, -30 + Math.sin(waveAngle) * 40);
        ctx.stroke();

        // Legs/wheels
        ctx.fillStyle = '#2d3748';
        ctx.beginPath();
        ctx.arc(-15, 15, 12, 0, Math.PI * 2);
        ctx.arc(15, 15, 12, 0, Math.PI * 2);
        ctx.fill();

        ctx.restore();
    },

    _roundRect: function(ctx, x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.lineTo(x + w - r, y);
        ctx.quadraticCurveTo(x + w, y, x + w, y + r);
        ctx.lineTo(x + w, y + h - r);
        ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
        ctx.lineTo(x + r, y + h);
        ctx.quadraticCurveTo(x, y + h, x, y + h - r);
        ctx.lineTo(x, y + r);
        ctx.quadraticCurveTo(x, y, x + r, y);
        ctx.closePath();
    },

    _drawHighlight: function() {
        if (!this._targetWidget) return;

        const rect = this._targetWidget.getBoundingClientRect();
        const ctx = this._ctx;

        // Pulsing highlight
        const pulse = Math.sin(this._time * 4) * 0.3 + 0.7;
        ctx.strokeStyle = `rgba(0, 212, 255, ${pulse})`;
        ctx.lineWidth = 2;
        ctx.setLineDash([5, 5]);
        ctx.strokeRect(rect.left - 3, rect.top - 3, rect.width + 6, rect.height + 6);
        ctx.setLineDash([]);

        // Speech bubble with value
        if (this._highlightValue !== null) {
            const bubbleX = this._robot.x + 40;
            const bubbleY = this._robot.y - 120;

            ctx.fillStyle = 'rgba(0, 0, 0, 0.8)';
            this._roundRect(ctx, bubbleX, bubbleY, 60, 30, 5);
            ctx.fill();

            ctx.fillStyle = '#00d4ff';
            ctx.font = 'bold 14px sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(`${Math.round(this._highlightValue)}%`, bubbleX + 30, bubbleY + 20);
        }
    },

    onBeforeRender: function(deltaTime) {
        this._time += deltaTime || 0.016;

        // Periodically find new important metric
        if (Math.floor(this._time) % 5 === 0 && Math.floor(this._time) !== this._lastCheck) {
            this._findImportantMetric();
            this._lastCheck = Math.floor(this._time);
        }

        this._ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);
        this._drawHighlight();
        this._drawRobot();
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
