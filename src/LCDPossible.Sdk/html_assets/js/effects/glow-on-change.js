/**
 * Glow on Change Effect
 * Values that changed since last frame emit a brief glow/pulse.
 */
window.LCDEffect = {
    _previousValues: new Map(),
    _glowDuration: 500,

    onInit: function(options) {
        this._glowDuration = options.glowDuration || 500;

        // Add glow styles
        const style = document.createElement('style');
        style.id = 'effect-glow-on-change';
        style.textContent = `
            .lcd-value-glow {
                animation: valueGlow ${this._glowDuration}ms ease-out;
            }
            @keyframes valueGlow {
                0% {
                    text-shadow: 0 0 20px currentColor, 0 0 40px currentColor, 0 0 60px currentColor;
                    filter: brightness(1.5);
                }
                100% {
                    text-shadow: none;
                    filter: brightness(1);
                }
            }
        `;
        document.head.appendChild(style);

        // Store initial values
        this._captureValues();
    },

    _captureValues: function() {
        document.querySelectorAll('.lcd-stat-value, .lcd-donut-value, [class*="gauge"] .value').forEach(el => {
            const id = el.id || el.closest('[id]')?.id || Math.random().toString(36);
            this._previousValues.set(id, el.textContent?.trim());
        });
    },

    onValueChange: function(change) {
        // This is called by external code when values change
        if (change && change.element) {
            change.element.classList.add('lcd-value-glow');
            setTimeout(() => {
                change.element.classList.remove('lcd-value-glow');
            }, this._glowDuration);
        }
    },

    onBeforeRender: function(deltaTime) {
        // Check for changed values
        document.querySelectorAll('.lcd-stat-value, .lcd-donut-value, [class*="gauge"] .value').forEach(el => {
            const id = el.id || el.closest('[id]')?.id || Math.random().toString(36);
            const currentValue = el.textContent?.trim();
            const previousValue = this._previousValues.get(id);

            if (previousValue !== undefined && previousValue !== currentValue) {
                el.classList.add('lcd-value-glow');
                setTimeout(() => {
                    el.classList.remove('lcd-value-glow');
                }, this._glowDuration);
            }

            this._previousValues.set(id, currentValue);
        });
    },

    onAfterRender: function(deltaTime) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        const style = document.getElementById('effect-glow-on-change');
        if (style) style.remove();
    }
};
