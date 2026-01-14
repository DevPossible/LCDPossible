/**
 * Clean Theme JavaScript
 *
 * This file is loaded with every HTML panel when the clean theme is active.
 * It provides lifecycle hooks for applying special effects and animations.
 */

window.LCDTheme = {
    /**
     * Called after DOM is ready, before first frame capture.
     * Use for initial setup, DOM modifications, or one-time effects.
     */
    onDomReady: function() {
        // Clean theme setup
    },

    /**
     * Called after transition animation completes.
     * Use for post-transition effects or state reset.
     */
    onTransitionEnd: function() {
        // Post-transition handling
    },

    /**
     * Called before each frame render.
     * Use for continuous animations (keep lightweight!).
     */
    onBeforeRender: function() {
        // Per-frame updates
    }
};
