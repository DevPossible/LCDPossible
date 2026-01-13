/**
 * Pixel Mascot Effect
 * Retro pixel character reacts to values.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _mascot: null,
    _time: 0,
    _pixelSize: 4,
    _animationId: null,
    _lastFrameTime: 0,

    // 16x16 pixel art frames (1 = body, 2 = eyes, 3 = highlight)
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
        ]
    },

    onInit: function(options) {
        console.log('[EFFECT] pixel-mascot.onInit called', options);
        this._pixelSize = options.pixelSize || 4;

        // Create canvas in corner
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-pixel-mascot-canvas';
        this._canvas.width = 100;
        this._canvas.height = 100;
        this._canvas.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            z-index: 9998;
            pointer-events: none;
            image-rendering: pixelated;
        `;
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._ctx.imageSmoothingEnabled = false;

        this._mascot = {
            sprite: 'idle',
            bounce: 0
        };

        // Start internal animation loop (browser handles timing)
        this._lastFrameTime = performance.now();
        this._animate();
        console.log('[EFFECT] pixel-mascot animation loop started');
    },

    _animate: function() {
        const now = performance.now();
        const deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;

        // Update mascot expression based on system state
        this._mascot.sprite = this._checkSystemState();
        this._drawSprite(this._mascot.sprite);

        // Continue animation loop
        this._animationId = requestAnimationFrame(() => this._animate());
    },

    _drawSprite: function(spriteName) {
        const sprite = this._sprites[spriteName] || this._sprites.idle;
        const ctx = this._ctx;
        const size = this._pixelSize;

        ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);

        const bounce = Math.sin(this._time * 3) * 2;
        const offsetX = (this._canvas.width - 16 * size) / 2;
        const offsetY = (this._canvas.height - 16 * size) / 2 + bounce;

        sprite.forEach((row, y) => {
            row.split('').forEach((pixel, x) => {
                if (pixel === '0') return;

                let color;
                switch (pixel) {
                    case '1': color = '#00d4ff'; break; // Body (cyan)
                    case '2': color = '#ffffff'; break; // Eyes (white)
                    case '3': color = '#000000'; break; // Pupils/details
                    default: color = '#00d4ff';
                }

                ctx.fillStyle = color;
                ctx.fillRect(
                    offsetX + x * size,
                    offsetY + y * size,
                    size,
                    size
                );
            });
        });
    },

    _checkSystemState: function() {
        // Check for warning/critical values
        let hasWarning = false;
        let hasCritical = false;

        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const text = el.textContent || '';
            const match = text.match(/(\d+(?:\.\d+)?)/);
            if (match) {
                const value = parseFloat(match[1]);
                if (value >= 90) hasCritical = true;
                else if (value >= 70) hasWarning = true;
            }
        });

        if (hasCritical) return 'worried';
        if (hasWarning) return 'worried';
        return 'happy';
    },

    onBeforeRender: function(deltaTime) {
        // Animation handled internally via requestAnimationFrame - no action needed
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {
        this._mascot.sprite = level === 'critical' ? 'worried' : 'worried';
    },
    onDestroy: function() {
        // Cancel animation loop
        if (this._animationId) {
            cancelAnimationFrame(this._animationId);
            this._animationId = null;
        }
        if (this._canvas) this._canvas.remove();
    }
};
