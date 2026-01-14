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
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] matrix-rain.onInit called', options);

        this._fontSize = options.fontSize || 14;
        if (options.chars) this._chars = options.chars;

        // Create canvas behind everything
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-matrix-rain-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 1',
            'opacity: 0.4'
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

        var columnCount = Math.floor(this._canvas.width / this._fontSize);
        this._columns = [];
        for (var i = 0; i < columnCount; i++) {
            this._columns.push({
                y: Math.random() * this._canvas.height,
                speed: 0.5 + Math.random() * 0.5
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
        var h = this._canvas.height;

        for (var i = 0; i < this._columns.length; i++) {
            var col = this._columns[i];
            col.y += this._fontSize * col.speed * dt * 12;

            // Reset column when it goes off screen
            if (col.y > h && Math.random() > 0.95) {
                col.y = 0;
            }
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        // Fade effect
        ctx.fillStyle = 'rgba(0, 0, 0, 0.05)';
        ctx.fillRect(0, 0, w, h);

        ctx.font = this._fontSize + 'px monospace';

        for (var i = 0; i < this._columns.length; i++) {
            var col = this._columns[i];
            var char = this._chars[Math.floor(Math.random() * this._chars.length)];
            var x = i * this._fontSize;

            // Brighter leading character
            ctx.fillStyle = '#fff';
            ctx.fillText(char, x, col.y);

            // Trail characters
            ctx.fillStyle = 'rgba(0, 255, 70, 0.8)';
            ctx.fillText(char, x, col.y - this._fontSize);
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
