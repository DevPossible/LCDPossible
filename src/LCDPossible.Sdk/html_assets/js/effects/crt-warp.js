/**
 * CRT Warp Effect
 * CRT screen edge warping/curvature.
 */
window.LCDEffect = {
    _style: null,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] crt-warp.onInit called', options);

        this._style = document.createElement('style');
        this._style.id = 'effect-crt-warp';
        this._style.textContent = [
            'body::before {',
            '    content: "";',
            '    position: fixed;',
            '    top: 0;',
            '    left: 0;',
            '    right: 0;',
            '    bottom: 0;',
            '    pointer-events: none;',
            '    z-index: 9999;',
            '    background: radial-gradient(',
            '        ellipse at center,',
            '        transparent 0%,',
            '        transparent 70%,',
            '        rgba(0, 0, 0, 0.3) 100%',
            '    );',
            '}',
            'body::after {',
            '    content: "";',
            '    position: fixed;',
            '    top: -5%;',
            '    left: -5%;',
            '    right: -5%;',
            '    bottom: -5%;',
            '    pointer-events: none;',
            '    z-index: 9998;',
            '    box-shadow: inset 0 0 100px rgba(0, 0, 0, 0.5);',
            '    border-radius: 20px;',
            '}'
        ].join('\n');
        document.head.appendChild(this._style);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _animate: function() {
        var self = this;
        this._animationId = requestAnimationFrame(function() { self._animate(); });
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
        if (this._style) this._style.remove();
    }
};
