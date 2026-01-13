/**
 * Matrix Rain Effect
 * Digital rain falling behind widgets.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _columns: [],
    _fontSize: 14,
    _chars: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%^&*',

    onInit: function(options) {
        this._fontSize = options.fontSize || 14;
        if (options.chars) this._chars = options.chars;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-matrix-rain-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: -1;
            opacity: 0.3;
        `;
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        // Handle window resize
        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;

        const columnCount = Math.floor(this._canvas.width / this._fontSize);
        this._columns = [];
        for (let i = 0; i < columnCount; i++) {
            this._columns.push({
                y: Math.random() * this._canvas.height,
                speed: 0.5 + Math.random() * 0.5
            });
        }
    },

    onBeforeRender: function(deltaTime) {
        if (!this._ctx) return;

        const dt = deltaTime || 0.016;

        // Fade effect
        this._ctx.fillStyle = 'rgba(0, 0, 0, 0.05)';
        this._ctx.fillRect(0, 0, this._canvas.width, this._canvas.height);

        // Draw characters
        this._ctx.fillStyle = '#0f0';
        this._ctx.font = `${this._fontSize}px monospace`;

        this._columns.forEach((col, i) => {
            const char = this._chars[Math.floor(Math.random() * this._chars.length)];
            const x = i * this._fontSize;

            // Brighter leading character
            this._ctx.fillStyle = '#fff';
            this._ctx.fillText(char, x, col.y);

            // Trail characters
            this._ctx.fillStyle = 'rgba(0, 255, 70, 0.8)';
            this._ctx.fillText(char, x, col.y - this._fontSize);

            col.y += this._fontSize * col.speed * dt * 60;

            // Reset column when it goes off screen
            if (col.y > this._canvas.height && Math.random() > 0.95) {
                col.y = 0;
            }
        });
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
