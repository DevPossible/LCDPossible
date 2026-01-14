/**
 * Pixel Mascot Effect
 * Retro pixel character that walks around, reacts to values, and celebrates.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _mascot: null,
    _time: 0,
    _pixelSize: 4,
    _animationId: null,
    _lastFrameTime: 0,
    _walkFrame: 0,
    _jumpVelocity: 0,
    _groundY: 0,

    // 16x16 pixel art frames (1 = body, 2 = eyes, 3 = highlight/pupils, 4 = accent)
    _sprites: {
        idle: [
            '0000001111000000',
            '0000111111110000',
            '0001112211221000',
            '0011112211221100',
            '0011111111111100',
            '0011333333331100',
            '0001111111111000',
            '0000111111110000',
            '0001111111111000',
            '0011111111111100',
            '0111111111111110',
            '1111110000111111',
            '1111100000011111',
            '0111000000001110',
            '0011000000001100',
            '0011100000011100'
        ],
        happy: [
            '0000001111000000',
            '0000111111110000',
            '0001112211221000',
            '0011113311331100',
            '0011111111111100',
            '0011133333311100',
            '0001113333111000',
            '0000111111110000',
            '0001111111111000',
            '0011111111111100',
            '0111111111111110',
            '1111110000111111',
            '1111100000011111',
            '0111000000001110',
            '0011000000001100',
            '0011100000011100'
        ],
        excited: [
            '0000441111440000',
            '0004111111114000',
            '0001112211221000',
            '0011113311331100',
            '0011111111111100',
            '0011133333311100',
            '0001113333111000',
            '0000111111110000',
            '0001111111111000',
            '0011111111111100',
            '0111111111111110',
            '1111110000111111',
            '1111100000011111',
            '0111000000001110',
            '0111100000011110',
            '0111110000111110'
        ],
        worried: [
            '0000001111000000',
            '0000111111110000',
            '0001112211221000',
            '0011221122112100',
            '0011111111111100',
            '0011111111111100',
            '0001133333311000',
            '0000113333110000',
            '0001111111111000',
            '0011111111111100',
            '0111111111111110',
            '1111110000111111',
            '1111100000011111',
            '0111000000001110',
            '0011000000001100',
            '0011100000011100'
        ],
        walkLeft: [
            '0000001111000000',
            '0000111111110000',
            '0001112211221000',
            '0011112211221100',
            '0011111111111100',
            '0011333333331100',
            '0001111111111000',
            '0000111111110000',
            '0001111111111000',
            '0011111111111100',
            '0111111111111110',
            '1111111000111111',
            '1111100000011111',
            '0111100000001110',
            '0001100000001100',
            '0001100000111100'
        ],
        walkRight: [
            '0000001111000000',
            '0000111111110000',
            '0001112211221000',
            '0011112211221100',
            '0011111111111100',
            '0011333333331100',
            '0001111111111000',
            '0000111111110000',
            '0001111111111000',
            '0011111111111100',
            '0111111111111110',
            '1111110001111111',
            '1111100000011111',
            '0111000000011110',
            '0011000000011000',
            '0011110000011000'
        ]
    },

    onInit: function(options) {
        console.log('[EFFECT] pixel-mascot.onInit called', options);
        this._pixelSize = options.pixelSize || 4;

        // Create canvas in corner
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-pixel-mascot-canvas';
        this._canvas.width = 200;
        this._canvas.height = 120;
        this._canvas.style.cssText = [
            'position: fixed',
            'bottom: 10px',
            'right: 10px',
            'z-index: 9998',
            'pointer-events: none',
            'image-rendering: pixelated'
        ].join(';');
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._ctx.imageSmoothingEnabled = false;

        this._groundY = this._canvas.height - 20;

        this._mascot = {
            sprite: 'idle',
            x: this._canvas.width / 2 - 32,
            y: this._groundY,
            targetX: this._canvas.width / 2 - 32,
            direction: 1, // 1 = right, -1 = left
            state: 'idle', // idle, walking, jumping, celebrating
            celebrateTimer: 0,
            idleTimer: 0
        };

        // Start internal animation loop
        this._lastFrameTime = performance.now();
        this._animate();
        console.log('[EFFECT] pixel-mascot animation loop started');
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;
        this._walkFrame += deltaTime * 8; // Walk animation speed

        this._updateMascot(deltaTime);
        this._render();

        // Continue animation loop
        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _updateMascot: function(dt) {
        var m = this._mascot;
        var speed = 80; // pixels per second

        // Handle jumping physics
        if (m.state === 'jumping') {
            this._jumpVelocity += 400 * dt; // gravity
            m.y += this._jumpVelocity * dt;

            if (m.y >= this._groundY) {
                m.y = this._groundY;
                this._jumpVelocity = 0;
                m.state = 'idle';
            }
        }

        // Handle celebration
        if (m.state === 'celebrating') {
            m.celebrateTimer -= dt;
            if (m.celebrateTimer <= 0) {
                m.state = 'idle';
            }
        }

        // Idle behavior - occasionally pick new target
        if (m.state === 'idle') {
            m.idleTimer += dt;
            if (m.idleTimer > 2 + Math.random() * 3) {
                m.idleTimer = 0;
                // Pick random target position
                m.targetX = 20 + Math.random() * (this._canvas.width - 100);
                m.state = 'walking';
            }
        }

        // Walking
        if (m.state === 'walking') {
            var dx = m.targetX - m.x;
            m.direction = dx > 0 ? 1 : -1;

            if (Math.abs(dx) < 5) {
                m.state = 'idle';
                m.x = m.targetX;
            } else {
                m.x += m.direction * speed * dt;
            }
        }

        // Update sprite based on state
        var systemState = this._checkSystemState();

        if (m.state === 'celebrating') {
            m.sprite = 'excited';
        } else if (m.state === 'jumping') {
            m.sprite = 'excited';
        } else if (m.state === 'walking') {
            // Alternate walk frames
            m.sprite = Math.floor(this._walkFrame) % 2 === 0 ? 'walkLeft' : 'walkRight';
        } else if (systemState === 'critical') {
            m.sprite = 'worried';
        } else if (systemState === 'warning') {
            m.sprite = 'worried';
        } else {
            m.sprite = 'happy';
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var m = this._mascot;

        ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);

        // Draw shadow
        ctx.fillStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.beginPath();
        ctx.ellipse(m.x + 32, this._groundY + 12, 25, 6, 0, 0, Math.PI * 2);
        ctx.fill();

        // Draw celebration particles
        if (m.state === 'celebrating') {
            this._drawParticles(m.x + 32, m.y - 32);
        }

        // Draw sprite
        this._drawSprite(m.sprite, m.x, m.y - 64, m.direction);

        // Draw jump motion lines
        if (m.state === 'jumping' && this._jumpVelocity < 0) {
            ctx.strokeStyle = 'rgba(0, 212, 255, 0.5)';
            ctx.lineWidth = 2;
            for (var i = 0; i < 3; i++) {
                ctx.beginPath();
                ctx.moveTo(m.x + 20 + i * 12, m.y + 10);
                ctx.lineTo(m.x + 20 + i * 12, m.y + 30);
                ctx.stroke();
            }
        }
    },

    _drawSprite: function(spriteName, x, y, direction) {
        var sprite = this._sprites[spriteName] || this._sprites.idle;
        var ctx = this._ctx;
        var size = this._pixelSize;

        // Apply breathing/bounce animation
        var bounce = Math.sin(this._time * 3) * 2;

        ctx.save();

        // Handle direction flip
        if (direction === -1) {
            ctx.translate(x + 32, y + bounce);
            ctx.scale(-1, 1);
            ctx.translate(-32, 0);
        } else {
            ctx.translate(x, y + bounce);
        }

        for (var row = 0; row < sprite.length; row++) {
            var chars = sprite[row].split('');
            for (var col = 0; col < chars.length; col++) {
                var pixel = chars[col];
                if (pixel === '0') continue;

                var color;
                switch (pixel) {
                    case '1': color = '#00d4ff'; break; // Body (cyan)
                    case '2': color = '#ffffff'; break; // Eyes (white)
                    case '3': color = '#000000'; break; // Pupils/details
                    case '4': color = '#ffff00'; break; // Accent (yellow)
                    default: color = '#00d4ff';
                }

                ctx.fillStyle = color;
                ctx.fillRect(col * size, row * size, size, size);
            }
        }

        ctx.restore();
    },

    _drawParticles: function(cx, cy) {
        var ctx = this._ctx;
        var particleCount = 8;

        for (var i = 0; i < particleCount; i++) {
            var angle = (i / particleCount) * Math.PI * 2 + this._time * 3;
            var radius = 30 + Math.sin(this._time * 5 + i) * 10;
            var x = cx + Math.cos(angle) * radius;
            var y = cy + Math.sin(angle) * radius;

            var colors = ['#ffff00', '#00ff00', '#ff00ff', '#00ffff'];
            ctx.fillStyle = colors[i % colors.length];
            ctx.fillRect(x - 3, y - 3, 6, 6);
        }
    },

    _checkSystemState: function() {
        // Check for warning/critical values
        var hasCritical = false;
        var hasWarning = false;

        var elements = document.querySelectorAll('.lcd-stat-value');
        for (var i = 0; i < elements.length; i++) {
            var el = elements[i];
            var text = el.textContent || '';
            var match = text.match(/(\d+(?:\.\d+)?)/);
            if (match) {
                var value = parseFloat(match[1]);
                if (value >= 90) hasCritical = true;
                else if (value >= 70) hasWarning = true;
            }
        }

        if (hasCritical) return 'critical';
        if (hasWarning) return 'warning';
        return 'normal';
    },

    _jump: function() {
        if (this._mascot.state !== 'jumping') {
            this._mascot.state = 'jumping';
            this._jumpVelocity = -200; // Initial upward velocity
        }
    },

    _celebrate: function() {
        this._mascot.state = 'celebrating';
        this._mascot.celebrateTimer = 2;
        this._jump();
    },

    onBeforeRender: function(deltaTime) {
        // Animation handled internally via requestAnimationFrame
    },

    onAfterRender: function(deltaTime) {},

    onValueChange: function(change) {
        // Jump when values change significantly
        if (change && change.difference && Math.abs(change.difference) > 10) {
            this._jump();
        }
    },

    onWarning: function(element, level) {
        if (level === 'critical') {
            this._mascot.sprite = 'worried';
            this._jump();
        }
    },

    onDestroy: function() {
        if (this._animationId) {
            cancelAnimationFrame(this._animationId);
            this._animationId = null;
        }
        if (this._canvas) this._canvas.remove();
    }
};
