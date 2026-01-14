/**
 * Robot Assistant Effect
 * Cute robot that moves between widgets and creates sparks while "working" on them.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _robot: null,
    _time: 0,
    _targetWidget: null,
    _sparks: [],
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] robot-assistant.onInit called', options);

        // Create canvas overlay
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-robot-assistant-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 9998',
            'pointer-events: none'
        ].join(';');
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._robot = {
            x: 50,
            y: this._canvas.height - 100,
            targetX: 50,
            targetY: this._canvas.height - 100,
            eyeOffset: 0,
            armAngle: -Math.PI / 4,
            state: 'idle', // idle, moving, working
            workTimer: 0,
            idleTimer: 0
        };

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        // Start animation loop
        this._lastFrameTime = performance.now();
        this._animate();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
        if (this._robot) {
            this._robot.y = Math.min(this._robot.y, this._canvas.height - 50);
        }
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;
        this._updateRobot(deltaTime);
        this._updateSparks(deltaTime);
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _updateRobot: function(dt) {
        var r = this._robot;
        var speed = 150;

        if (r.state === 'idle') {
            r.idleTimer += dt;
            if (r.idleTimer > 2 + Math.random() * 2) {
                r.idleTimer = 0;
                this._findNewTarget();
            }
        } else if (r.state === 'moving') {
            var dx = r.targetX - r.x;
            var dy = r.targetY - r.y;
            var dist = Math.sqrt(dx * dx + dy * dy);

            if (dist < 10) {
                r.x = r.targetX;
                r.y = r.targetY;
                r.state = 'working';
                r.workTimer = 0;
            } else {
                r.x += (dx / dist) * speed * dt;
                r.y += (dy / dist) * speed * dt;
            }
        } else if (r.state === 'working') {
            r.workTimer += dt;

            // Create sparks while working
            if (Math.random() < 0.3) {
                this._createSpark(r.x + 40 + Math.cos(r.armAngle) * 50, r.y - 30 + Math.sin(r.armAngle) * 50);
            }

            // Animate arm while working
            r.armAngle = -Math.PI / 4 + Math.sin(this._time * 15) * 0.3;

            if (r.workTimer > 2 + Math.random()) {
                r.state = 'idle';
                r.idleTimer = 0;
            }
        }
    },

    _findNewTarget: function() {
        var widgets = document.querySelectorAll('.lcd-widget');
        if (widgets.length === 0) return;

        var randomWidget = widgets[Math.floor(Math.random() * widgets.length)];
        var rect = randomWidget.getBoundingClientRect();

        this._targetWidget = randomWidget;

        // Position robot next to the widget
        this._robot.targetX = Math.max(50, Math.min(rect.left - 60, this._canvas.width - 100));
        this._robot.targetY = Math.max(80, Math.min(rect.top + rect.height / 2, this._canvas.height - 80));
        this._robot.state = 'moving';
    },

    _createSpark: function(x, y) {
        var count = 3 + Math.floor(Math.random() * 4);
        for (var i = 0; i < count; i++) {
            var angle = Math.random() * Math.PI * 2;
            var speed = 100 + Math.random() * 150;
            this._sparks.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed - 50, // Slight upward bias
                life: 0.5 + Math.random() * 0.3,
                size: 2 + Math.random() * 3,
                color: Math.random() > 0.5 ? '#ffff00' : '#ff8800'
            });
        }
    },

    _updateSparks: function(dt) {
        var gravity = 300;
        var self = this;

        this._sparks = this._sparks.filter(function(spark) {
            spark.vy += gravity * dt;
            spark.x += spark.vx * dt;
            spark.y += spark.vy * dt;
            spark.life -= dt;
            spark.size *= 0.95;
            return spark.life > 0 && spark.size > 0.5;
        });
    },

    _render: function() {
        var ctx = this._ctx;
        ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);

        // Draw widget highlight if working
        if (this._robot.state === 'working' && this._targetWidget) {
            this._drawHighlight();
        }

        // Draw sparks
        this._drawSparks();

        // Draw robot
        this._drawRobot();
    },

    _drawSparks: function() {
        var ctx = this._ctx;

        for (var i = 0; i < this._sparks.length; i++) {
            var spark = this._sparks[i];
            var alpha = spark.life * 2;

            ctx.beginPath();
            ctx.arc(spark.x, spark.y, spark.size, 0, Math.PI * 2);
            ctx.fillStyle = spark.color.replace(')', ', ' + alpha + ')').replace('rgb', 'rgba').replace('#ffff00', 'rgba(255, 255, 0, ' + alpha + ')').replace('#ff8800', 'rgba(255, 136, 0, ' + alpha + ')');

            // Simple color with alpha
            if (spark.color === '#ffff00') {
                ctx.fillStyle = 'rgba(255, 255, 0, ' + alpha + ')';
            } else {
                ctx.fillStyle = 'rgba(255, 136, 0, ' + alpha + ')';
            }
            ctx.fill();

            // Glow effect
            ctx.shadowColor = spark.color;
            ctx.shadowBlur = 10;
            ctx.fill();
            ctx.shadowBlur = 0;
        }
    },

    _drawRobot: function() {
        var ctx = this._ctx;
        var r = this._robot;

        ctx.save();
        ctx.translate(r.x, r.y);

        // Hover animation
        var hover = Math.sin(this._time * 2) * 3;
        ctx.translate(0, hover);

        // Shadow
        ctx.fillStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.beginPath();
        ctx.ellipse(0, 50 - hover, 30, 8, 0, 0, Math.PI * 2);
        ctx.fill();

        // Body
        ctx.fillStyle = '#4a5568';
        ctx.strokeStyle = '#00d4ff';
        ctx.lineWidth = 2;

        // Main body
        this._roundRect(ctx, -25, -50, 50, 60, 10);
        ctx.fill();
        ctx.stroke();

        // Chest panel
        ctx.fillStyle = '#2d3748';
        ctx.fillRect(-15, -35, 30, 25);
        ctx.strokeStyle = '#00d4ff';
        ctx.strokeRect(-15, -35, 30, 25);

        // Chest lights
        var lightBlink = Math.sin(this._time * 5) > 0;
        ctx.fillStyle = lightBlink ? '#00ff00' : '#004400';
        ctx.fillRect(-10, -30, 6, 6);
        ctx.fillStyle = !lightBlink ? '#00ff00' : '#004400';
        ctx.fillRect(-10, -20, 6, 6);
        ctx.fillStyle = r.state === 'working' ? '#ff0000' : '#440000';
        ctx.fillRect(4, -30, 6, 6);

        // Head
        ctx.fillStyle = '#2d3748';
        this._roundRect(ctx, -30, -95, 60, 50, 8);
        ctx.fill();
        ctx.strokeStyle = '#00d4ff';
        ctx.stroke();

        // Antenna
        ctx.strokeStyle = '#4a5568';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(0, -95);
        ctx.lineTo(0, -110);
        ctx.stroke();

        // Antenna light
        var antennaGlow = (Math.sin(this._time * 8) + 1) / 2;
        ctx.fillStyle = 'rgba(255, 255, 0, ' + antennaGlow + ')';
        ctx.beginPath();
        ctx.arc(0, -113, 5, 0, Math.PI * 2);
        ctx.fill();
        ctx.shadowColor = '#ffff00';
        ctx.shadowBlur = 10 * antennaGlow;
        ctx.fill();
        ctx.shadowBlur = 0;

        // Eyes
        r.eyeOffset = Math.sin(this._time * 0.5) * 3;
        ctx.fillStyle = '#00d4ff';
        ctx.beginPath();
        ctx.arc(-12 + r.eyeOffset, -70, 10, 0, Math.PI * 2);
        ctx.arc(12 + r.eyeOffset, -70, 10, 0, Math.PI * 2);
        ctx.fill();

        // Eye glow
        ctx.shadowColor = '#00d4ff';
        ctx.shadowBlur = 15;
        ctx.fill();
        ctx.shadowBlur = 0;

        // Pupils
        ctx.fillStyle = '#000';
        ctx.beginPath();
        ctx.arc(-12 + r.eyeOffset + 2, -70, 4, 0, Math.PI * 2);
        ctx.arc(12 + r.eyeOffset + 2, -70, 4, 0, Math.PI * 2);
        ctx.fill();

        // Mouth
        if (r.state === 'working') {
            // Excited mouth when working
            ctx.fillStyle = '#00ff00';
            ctx.beginPath();
            ctx.arc(0, -50, 8, 0, Math.PI);
            ctx.fill();
        } else {
            // Normal LED display mouth
            ctx.fillStyle = '#00ff00';
            ctx.fillRect(-12, -55, 24, 4);
        }

        // Arms
        ctx.strokeStyle = '#4a5568';
        ctx.lineWidth = 8;
        ctx.lineCap = 'round';

        // Right arm (working arm)
        ctx.beginPath();
        ctx.moveTo(25, -30);
        var armLen = 50;
        var armX = 25 + Math.cos(r.armAngle) * armLen;
        var armY = -30 + Math.sin(r.armAngle) * armLen;
        ctx.lineTo(armX, armY);
        ctx.stroke();

        // Tool at end of arm
        ctx.fillStyle = '#ff8800';
        ctx.beginPath();
        ctx.arc(armX, armY, 8, 0, Math.PI * 2);
        ctx.fill();

        // Tool glow when working
        if (r.state === 'working') {
            ctx.shadowColor = '#ff8800';
            ctx.shadowBlur = 15 + Math.sin(this._time * 20) * 5;
            ctx.fill();
            ctx.shadowBlur = 0;
        }

        // Left arm (waving)
        ctx.beginPath();
        ctx.moveTo(-25, -30);
        var waveAngle = Math.sin(this._time * 2) * 0.3 - Math.PI / 4;
        ctx.lineTo(-25 + Math.cos(waveAngle) * 40, -30 + Math.sin(waveAngle) * 40);
        ctx.stroke();

        // Hand
        ctx.fillStyle = '#4a5568';
        ctx.beginPath();
        ctx.arc(-25 + Math.cos(waveAngle) * 40, -30 + Math.sin(waveAngle) * 40, 6, 0, Math.PI * 2);
        ctx.fill();

        // Legs/treads
        ctx.fillStyle = '#2d3748';
        ctx.fillRect(-20, 10, 15, 25);
        ctx.fillRect(5, 10, 15, 25);

        // Tread details
        ctx.strokeStyle = '#4a5568';
        ctx.lineWidth = 2;
        for (var i = 0; i < 3; i++) {
            ctx.beginPath();
            ctx.moveTo(-20, 15 + i * 8);
            ctx.lineTo(-5, 15 + i * 8);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(5, 15 + i * 8);
            ctx.lineTo(20, 15 + i * 8);
            ctx.stroke();
        }

        ctx.restore();
    },

    _drawHighlight: function() {
        if (!this._targetWidget) return;

        var rect = this._targetWidget.getBoundingClientRect();
        var ctx = this._ctx;

        // Pulsing highlight
        var pulse = Math.sin(this._time * 4) * 0.3 + 0.7;
        ctx.strokeStyle = 'rgba(255, 136, 0, ' + pulse + ')';
        ctx.lineWidth = 2;
        ctx.setLineDash([5, 5]);
        ctx.strokeRect(rect.left - 3, rect.top - 3, rect.width + 6, rect.height + 6);
        ctx.setLineDash([]);
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

    onBeforeRender: function(deltaTime) {
        // Animation handled internally via requestAnimationFrame
    },

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
