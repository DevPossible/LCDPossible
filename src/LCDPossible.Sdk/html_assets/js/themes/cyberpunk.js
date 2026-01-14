/**
 * Cyberpunk Theme JavaScript
 *
 * This file is loaded with every HTML panel when the cyberpunk theme is active.
 * It provides lifecycle hooks for applying special effects and animations.
 */

window.LCDTheme = {
    // Internal state
    _initialized: false,
    _frameCount: 0,
    _glowElements: [],

    /**
     * Called after DOM is ready, before first frame capture.
     * Use for initial setup, DOM modifications, or one-time effects.
     */
    onDomReady: function() {
        if (this._initialized) return;
        this._initialized = true;

        // Find elements that should have glow effects
        this._glowElements = document.querySelectorAll('.lcd-stat-card, .lcd-donut, .lcd-temp-gauge, lcd-echarts-gauge, lcd-echarts-donut');

        // Add cyberpunk-specific CSS class to body
        document.body.classList.add('cyberpunk-enhanced');

        // Add scanline overlay if not present
        if (!document.querySelector('.scanlines-overlay')) {
            const scanlines = document.createElement('div');
            scanlines.className = 'scanlines-overlay';
            scanlines.style.cssText = `
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                pointer-events: none;
                z-index: 9999;
                background: repeating-linear-gradient(
                    0deg,
                    transparent,
                    transparent 2px,
                    rgba(0, 255, 255, 0.03) 2px,
                    rgba(0, 255, 255, 0.03) 4px
                );
                opacity: var(--scanlines-opacity, 0.03);
            `;
            document.body.appendChild(scanlines);
        }

        // Apply subtle glow pulse to accent elements
        this._applyGlowEffects();
    },

    /**
     * Called after transition animation completes.
     * Use for post-transition effects or state reset.
     */
    onTransitionEnd: function() {
        // Reset any transition-specific state
        // Could trigger "panel loaded" animation here
    },

    /**
     * Called before each frame render.
     * Use for continuous animations (keep lightweight!).
     */
    onBeforeRender: function() {
        this._frameCount++;

        // Subtle glow intensity pulse (every ~60 frames)
        if (this._frameCount % 60 === 0) {
            this._pulseGlow();
        }
    },

    /**
     * Apply initial glow effects to elements.
     * @private
     */
    _applyGlowEffects: function() {
        // Add glow class to primary value elements
        document.querySelectorAll('.lcd-stat-value, .text-primary').forEach(el => {
            if (!el.classList.contains('cyberpunk-glow')) {
                el.classList.add('cyberpunk-glow');
            }
        });

        // Add glow style if not present
        if (!document.querySelector('#cyberpunk-glow-styles')) {
            const style = document.createElement('style');
            style.id = 'cyberpunk-glow-styles';
            style.textContent = `
                .cyberpunk-glow {
                    text-shadow: 0 0 10px currentColor, 0 0 20px currentColor;
                    transition: text-shadow 0.3s ease;
                }
                .cyberpunk-glow-pulse {
                    text-shadow: 0 0 15px currentColor, 0 0 30px currentColor, 0 0 45px currentColor;
                }
                .cyberpunk-enhanced {
                    /* Base enhancements */
                }
            `;
            document.head.appendChild(style);
        }
    },

    /**
     * Pulse glow effect on accent elements.
     * @private
     */
    _pulseGlow: function() {
        // Brief glow pulse effect
        document.querySelectorAll('.cyberpunk-glow').forEach(el => {
            el.classList.add('cyberpunk-glow-pulse');
            setTimeout(() => {
                el.classList.remove('cyberpunk-glow-pulse');
            }, 200);
        });
    }
};
