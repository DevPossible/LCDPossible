/**
 * Hologram Effect
 * Holographic shimmer/interference pattern.
 */
window.LCDEffect = {
    _time: 0,
    _glitchIntensity: 0.3,
    _shimmerSpeed: 2,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] hologram.onInit called', options);

        this._glitchIntensity = options.glitchIntensity || 0.3;
        this._shimmerSpeed = options.shimmerSpeed || 2;

        const style = document.createElement('style');
        style.id = 'effect-hologram-style';
        style.textContent = `
            .lcd-widget-grid {
                position: relative;
            }
            .lcd-widget-grid::before {
                content: '';
                position: absolute;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: linear-gradient(
                    45deg,
                    transparent 20%,
                    rgba(0, 255, 255, 0.15) 50%,
                    transparent 80%
                );
                background-size: 200% 200%;
                animation: hologramShimmer 3s ease-in-out infinite;
                pointer-events: none;
                z-index: 100;
            }
            @keyframes hologramShimmer {
                0%, 100% { background-position: 0% 0%; }
                50% { background-position: 100% 100%; }
            }
            .lcd-widget {
                position: relative;
            }
            .lcd-widget::after {
                content: '';
                position: absolute;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: repeating-linear-gradient(
                    0deg,
                    transparent,
                    transparent 2px,
                    rgba(0, 255, 255, 0.08) 2px,
                    rgba(0, 255, 255, 0.08) 4px
                );
                pointer-events: none;
                mix-blend-mode: overlay;
            }
            .hologram-glitch {
                animation: hologramGlitch 0.1s ease-in-out;
            }
            @keyframes hologramGlitch {
                0% { transform: translate(0); filter: hue-rotate(0deg); }
                25% { transform: translate(-2px, 1px); filter: hue-rotate(90deg); }
                50% { transform: translate(2px, -1px); filter: hue-rotate(-90deg); }
                75% { transform: translate(-1px, 2px); filter: hue-rotate(180deg); }
                100% { transform: translate(0); filter: hue-rotate(0deg); }
            }
            .hologram-rgb-split {
                text-shadow:
                    -1px 0 rgba(255, 0, 0, 0.5),
                    1px 0 rgba(0, 255, 255, 0.5);
            }
        `;
        document.head.appendChild(style);

        // Add RGB split to values
        const valueElements = document.querySelectorAll('.lcd-stat-value');
        valueElements.forEach(el => {
            el.classList.add('hologram-rgb-split');
        });

        console.log('[EFFECT] hologram initialized: shimmer=' + this._shimmerSpeed + ', glitch=' + this._glitchIntensity + ', rgb-split on ' + valueElements.length + ' elements');

        // Start internal animation loop
        this._lastFrameTime = performance.now();
        this._animate();
    },

    _animate: function() {
        const now = performance.now();
        const deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime * this._shimmerSpeed;

        // Random glitch effect
        if (Math.random() < this._glitchIntensity * 0.02) { // Adjusted for 60fps
            const widgets = document.querySelectorAll('.lcd-widget');
            const randomWidget = widgets[Math.floor(Math.random() * widgets.length)];
            if (randomWidget) {
                randomWidget.classList.add('hologram-glitch');
                setTimeout(() => randomWidget.classList.remove('hologram-glitch'), 100);
            }
        }

        // Update shimmer position dynamically
        const shimmerX = 50 + Math.sin(this._time) * 50;
        const shimmerY = 50 + Math.cos(this._time * 0.7) * 50;
        document.documentElement.style.setProperty('--hologram-shimmer-x', shimmerX + '%');
        document.documentElement.style.setProperty('--hologram-shimmer-y', shimmerY + '%');

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
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            el.classList.remove('hologram-rgb-split');
        });
        document.querySelectorAll('.hologram-glitch').forEach(el => {
            el.classList.remove('hologram-glitch');
        });
        const style = document.getElementById('effect-hologram-style');
        if (style) style.remove();
    }
};
