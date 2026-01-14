/**
 * Wave Effect
 * Widgets wave in a sine pattern across the grid.
 */
window.LCDEffect = {
    _waveAmplitude: 8,
    _waveSpeed: 2,
    _time: 0,

    onInit: function(options) {
        this._waveAmplitude = options.waveAmplitude || 8;
        this._waveSpeed = options.waveSpeed || 2;

        const style = document.createElement('style');
        style.id = 'effect-wave';
        style.textContent = `
            .lcd-widget {
                transition: transform 0.1s ease-out;
            }
        `;
        document.head.appendChild(style);
    },

    onBeforeRender: function(deltaTime) {
        this._time += (deltaTime || 0.016) * this._waveSpeed;

        const widgets = document.querySelectorAll('.lcd-widget');
        const gridCols = 12; // Standard 12-column grid

        widgets.forEach((widget, index) => {
            // Calculate grid position (approximate based on widget position)
            const rect = widget.getBoundingClientRect();
            const gridX = Math.floor((rect.left / window.innerWidth) * gridCols);
            const gridY = Math.floor((rect.top / window.innerHeight) * 4); // 4-row grid

            // Create wave offset based on position
            const waveOffset = (gridX + gridY) * 0.5;
            const translateY = Math.sin(this._time + waveOffset) * this._waveAmplitude;

            widget.style.transform = `translateY(${translateY}px)`;
        });
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        document.querySelectorAll('.lcd-widget').forEach(widget => {
            widget.style.transform = '';
        });
        const style = document.getElementById('effect-wave');
        if (style) style.remove();
    }
};
