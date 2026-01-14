/**
 * Scanlines Effect
 * CRT/retro scanline overlay with visible scan lines and subtle flicker.
 */
window.LCDEffect = {
    _lineSpacing: 3,
    _opacity: 0.15,
    _moveSpeed: 0,
    _offset: 0,
    _animationId: null,
    _lastFrameTime: 0,
    _time: 0,

    onInit: function(options) {
        console.log('[EFFECT] scanlines.onInit called', options);

        this._lineSpacing = options.lineSpacing || 3;
        this._opacity = options.opacity || 0.15;
        this._moveSpeed = options.moveSpeed || 20; // Default to slow scroll for visible effect

        // Create main scanlines overlay
        var overlay = document.createElement('div');
        overlay.id = 'effect-scanlines-overlay';
        overlay.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'pointer-events: none',
            'z-index: 9999'
        ].join(';');
        document.body.appendChild(overlay);

        // Create canvas for scanlines (better control than CSS gradients)
        var canvas = document.createElement('canvas');
        canvas.id = 'effect-scanlines-canvas';
        canvas.style.cssText = [
            'position: absolute',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%'
        ].join(';');
        overlay.appendChild(canvas);
        this._canvas = canvas;
        this._ctx = canvas.getContext('2d');

        // Create phosphor glow overlay
        var glow = document.createElement('div');
        glow.id = 'effect-scanlines-glow';
        glow.style.cssText = [
            'position: absolute',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'background: radial-gradient(ellipse at center, transparent 0%, rgba(0, 0, 0, 0.3) 100%)',
            'pointer-events: none'
        ].join(';');
        overlay.appendChild(glow);

        // Add CRT effects style
        var style = document.createElement('style');
        style.id = 'effect-scanlines-style';
        style.textContent = [
            '@keyframes crtFlicker {',
            '    0% { opacity: 1; }',
            '    3% { opacity: 0.95; }',
            '    6% { opacity: 1; }',
            '    9% { opacity: 0.97; }',
            '    12% { opacity: 1; }',
            '    100% { opacity: 1; }',
            '}',
            '#effect-scanlines-overlay {',
            '    animation: crtFlicker 0.1s infinite;',
            '}'
        ].join('\n');
        document.head.appendChild(style);

        this._resize();
        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        // Start animation loop
        this._lastFrameTime = performance.now();
        this._animate();

        console.log('[EFFECT] scanlines overlay created: spacing=' + this._lineSpacing + 'px, opacity=' + this._opacity);
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
        this._drawScanlines();
    },

    _drawScanlines: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        // Draw horizontal scanlines
        ctx.fillStyle = 'rgba(0, 0, 0, ' + this._opacity + ')';

        var y = this._offset % this._lineSpacing;
        while (y < h) {
            // Each scanline is 1px with spacing between
            ctx.fillRect(0, y, w, 1);
            y += this._lineSpacing;
        }

        // Add occasional bright scan line for CRT effect
        var brightLineY = (this._time * 50) % (h + 100) - 50;
        if (brightLineY >= 0 && brightLineY < h) {
            ctx.fillStyle = 'rgba(255, 255, 255, 0.03)';
            ctx.fillRect(0, brightLineY, w, 2);
        }

        // Add subtle color fringing at edges (chromatic aberration hint)
        var gradient = ctx.createLinearGradient(0, 0, 10, 0);
        gradient.addColorStop(0, 'rgba(255, 0, 0, 0.02)');
        gradient.addColorStop(1, 'transparent');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, 10, h);

        gradient = ctx.createLinearGradient(w - 10, 0, w, 0);
        gradient.addColorStop(0, 'transparent');
        gradient.addColorStop(1, 'rgba(0, 255, 255, 0.02)');
        ctx.fillStyle = gradient;
        ctx.fillRect(w - 10, 0, 10, h);
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;
        this._offset += deltaTime * this._moveSpeed;

        this._drawScanlines();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
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
        var overlay = document.getElementById('effect-scanlines-overlay');
        if (overlay) overlay.remove();
        var style = document.getElementById('effect-scanlines-style');
        if (style) style.remove();
    }
};
