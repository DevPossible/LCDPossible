/**
 * Neon Border Effect
 * Glowing pulse around widget edges.
 */
window.LCDEffect = {
    _time: 0,
    _style: null,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] neon-border.onInit called', options);

        this._style = document.createElement('style');
        this._style.id = 'effect-neon-border';
        this._style.textContent = [
            '.lcd-widget {',
            '    box-shadow: 0 0 10px var(--neon-color, #00d4ff),',
            '                0 0 20px var(--neon-color, #00d4ff),',
            '                inset 0 0 10px rgba(0, 212, 255, 0.1);',
            '    transition: box-shadow 0.3s ease;',
            '}',
            '.lcd-widget.neon-pulse {',
            '    box-shadow: 0 0 15px var(--neon-color, #00d4ff),',
            '                0 0 30px var(--neon-color, #00d4ff),',
            '                0 0 45px var(--neon-color, #00d4ff),',
            '                inset 0 0 15px rgba(0, 212, 255, 0.2);',
            '}'
        ].join('\n');
        document.head.appendChild(this._style);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;
        this._update();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _update: function() {
        var pulse = (Math.sin(this._time * 2) + 1) / 2;
        var widgets = document.querySelectorAll('.lcd-widget');

        widgets.forEach(function(widget, index) {
            var offset = index * 0.5;
            var widgetPulse = (Math.sin(this._time * 2 + offset) + 1) / 2;

            if (widgetPulse > 0.7) {
                widget.classList.add('neon-pulse');
            } else {
                widget.classList.remove('neon-pulse');
            }
        }.bind(this));
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

        var widgets = document.querySelectorAll('.lcd-widget');
        widgets.forEach(function(w) {
            w.classList.remove('neon-pulse');
        });

        if (this._style) this._style.remove();
    }
};
