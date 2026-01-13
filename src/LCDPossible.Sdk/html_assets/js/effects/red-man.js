/**
 * Red Man Effect - Debug/Test overlay
 * Creates a full-panel red overlay at 55% opacity for debugging.
 * If you see this, effects are working!
 */
window.LCDEffect = {
    onInit: function(options) {
        console.log('[EFFECT] red-man.onInit called', options);

        const overlay = document.createElement('div');
        overlay.id = 'effect-red-man-overlay';
        overlay.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background-color: rgba(255, 0, 0, 0.55);
            pointer-events: none;
            z-index: 9999;
        `;
        document.body.appendChild(overlay);
        console.log('[EFFECT] red-man overlay created (55% red)');
    },

    onBeforeRender: function(deltaTime) {},
    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        const overlay = document.getElementById('effect-red-man-overlay');
        if (overlay) overlay.remove();
    }
};
