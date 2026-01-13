/**
 * Particle Burst Effect
 * Particles burst from widgets when values change significantly.
 */
window.LCDEffect = {
    _previousValues: new Map(),
    _particleCount: 12,
    _threshold: 5, // Minimum change to trigger burst

    onInit: function(options) {
        this._particleCount = options.particleCount || 12;
        this._threshold = options.threshold || 5;

        const style = document.createElement('style');
        style.id = 'effect-particle-burst';
        style.textContent = `
            .lcd-particle-container {
                position: fixed;
                pointer-events: none;
                z-index: 9999;
            }
            .lcd-particle {
                position: absolute;
                width: 8px;
                height: 8px;
                border-radius: 50%;
                background: var(--color-primary, oklch(0.7 0.15 200));
                animation: particleBurst 0.8s ease-out forwards;
            }
            @keyframes particleBurst {
                0% {
                    transform: translate(0, 0) scale(1);
                    opacity: 1;
                }
                100% {
                    transform: translate(var(--tx), var(--ty)) scale(0);
                    opacity: 0;
                }
            }
        `;
        document.head.appendChild(style);
        this._captureValues();
    },

    _captureValues: function() {
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const id = this._getElementId(el);
            const value = parseFloat(el.textContent?.replace(/[^0-9.-]/g, '') || '0');
            this._previousValues.set(id, value);
        });
    },

    _getElementId: function(el) {
        return el.id || el.dataset.effectId || (el.dataset.effectId = Math.random().toString(36).substr(2, 9));
    },

    _createBurst: function(el) {
        const rect = el.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        const container = document.createElement('div');
        container.className = 'lcd-particle-container';
        container.style.left = '0';
        container.style.top = '0';

        for (let i = 0; i < this._particleCount; i++) {
            const particle = document.createElement('div');
            particle.className = 'lcd-particle';

            const angle = (i / this._particleCount) * Math.PI * 2;
            const distance = 50 + Math.random() * 50;
            const tx = Math.cos(angle) * distance;
            const ty = Math.sin(angle) * distance;

            particle.style.left = centerX + 'px';
            particle.style.top = centerY + 'px';
            particle.style.setProperty('--tx', tx + 'px');
            particle.style.setProperty('--ty', ty + 'px');

            // Vary particle colors
            const hue = 180 + Math.random() * 60; // Cyan to blue range
            particle.style.background = `oklch(0.7 0.15 ${hue})`;

            container.appendChild(particle);
        }

        document.body.appendChild(container);

        // Remove container after animation
        setTimeout(() => container.remove(), 1000);
    },

    onBeforeRender: function(deltaTime) {
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const id = this._getElementId(el);
            const currentValue = parseFloat(el.textContent?.replace(/[^0-9.-]/g, '') || '0');
            const previousValue = this._previousValues.get(id) || 0;

            const change = Math.abs(currentValue - previousValue);
            if (change >= this._threshold) {
                this._createBurst(el);
            }

            this._previousValues.set(id, currentValue);
        });
    },

    onValueChange: function(change) {},
    onAfterRender: function(deltaTime) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        document.querySelectorAll('.lcd-particle-container').forEach(el => el.remove());
        const style = document.getElementById('effect-particle-burst');
        if (style) style.remove();
    }
};
