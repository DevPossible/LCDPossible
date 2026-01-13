/**
 * Scanlines Effect
 * CRT/retro scanline overlay.
 */
window.LCDEffect = {
    _lineSpacing: 4,
    _opacity: 0.5,
    _moveSpeed: 0,
    _offset: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] scanlines.onInit called', options);

        this._lineSpacing = options.lineSpacing || 4;
        this._opacity = options.opacity || 0.5;
        this._moveSpeed = options.moveSpeed || 0; // 0 = static, >0 = scrolling

        const overlay = document.createElement('div');
        overlay.id = 'effect-scanlines-overlay';
        // Scanlines thick enough to survive JPEG compression (2px dark, 2px transparent)
        const halfSpacing = Math.max(2, Math.floor(this._lineSpacing / 2));
        overlay.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            pointer-events: none;
            z-index: 9999;
            background: repeating-linear-gradient(
                0deg,
                rgba(0, 0, 0, ${this._opacity}) 0px,
                rgba(0, 0, 0, ${this._opacity}) ${halfSpacing}px,
                transparent ${halfSpacing}px,
                transparent ${this._lineSpacing}px
            );
        `;
        document.body.appendChild(overlay);
        console.log('[EFFECT] scanlines overlay created: spacing=' + this._lineSpacing + 'px, opacity=' + this._opacity);

        // Add subtle CRT flicker
        const style = document.createElement('style');
        style.id = 'effect-scanlines-style';
        style.textContent = `
            @keyframes crtFlicker {
                0% { opacity: ${this._opacity}; }
                5% { opacity: ${this._opacity * 1.2}; }
                10% { opacity: ${this._opacity}; }
                15% { opacity: ${this._opacity * 0.8}; }
                20% { opacity: ${this._opacity}; }
                100% { opacity: ${this._opacity}; }
            }
            #effect-scanlines-overlay {
                animation: crtFlicker 0.15s infinite;
            }
        `;
        document.head.appendChild(style);

        // Start internal animation loop if scrolling enabled
        if (this._moveSpeed > 0) {
            this._lastFrameTime = performance.now();
            this._animate();
        }
    },

    _animate: function() {
        const now = performance.now();
        const deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._offset += deltaTime * this._moveSpeed * 10;
        const overlay = document.getElementById('effect-scanlines-overlay');
        if (overlay) {
            overlay.style.backgroundPositionY = `${this._offset}px`;
        }

        // Continue animation loop
        this._animationId = requestAnimationFrame(() => this._animate());
    },

    onBeforeRender: function(deltaTime) {
        // Animation handled internally via requestAnimationFrame - no action needed
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        // Cancel animation loop
        if (this._animationId) {
            cancelAnimationFrame(this._animationId);
            this._animationId = null;
        }
        const overlay = document.getElementById('effect-scanlines-overlay');
        if (overlay) overlay.remove();
        const style = document.getElementById('effect-scanlines-style');
        if (style) style.remove();
    }
};
