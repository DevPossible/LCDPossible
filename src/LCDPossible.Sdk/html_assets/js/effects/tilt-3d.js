/**
 * 3D Tilt Effect
 * Containers have slight 3D tilt/perspective that shifts.
 */
window.LCDEffect = {
    _maxTilt: 5,
    _perspective: 1000,
    _time: 0,

    onInit: function(options) {
        this._maxTilt = options.maxTilt || 5;
        this._perspective = options.perspective || 1000;

        const style = document.createElement('style');
        style.id = 'effect-tilt-3d';
        style.textContent = `
            .lcd-widget-grid {
                perspective: ${this._perspective}px;
                transform-style: preserve-3d;
            }
            .lcd-widget {
                transform-style: preserve-3d;
                transition: transform 0.1s ease-out;
            }
        `;
        document.head.appendChild(style);
    },

    onBeforeRender: function(deltaTime) {
        this._time += deltaTime || 0.016;

        document.querySelectorAll('.lcd-widget').forEach((widget, index) => {
            // Create a wave-like tilt pattern across widgets
            const offset = index * 0.5;
            const rotateX = Math.sin(this._time + offset) * this._maxTilt;
            const rotateY = Math.cos(this._time * 0.7 + offset) * this._maxTilt;

            widget.style.transform = `rotateX(${rotateX}deg) rotateY(${rotateY}deg)`;
        });
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        document.querySelectorAll('.lcd-widget').forEach(widget => {
            widget.style.transform = '';
        });
        const style = document.getElementById('effect-tilt-3d');
        if (style) style.remove();
    }
};
