/**
 * Gentle Float Effect
 * Containers float up/down subtly (breathing effect).
 */
window.LCDEffect = {
    _floatAmount: 4,
    _floatDuration: 3,

    onInit: function(options) {
        this._floatAmount = options.floatAmount || 4;
        this._floatDuration = options.floatDuration || 3;

        const style = document.createElement('style');
        style.id = 'effect-gentle-float';
        style.textContent = `
            .lcd-widget {
                animation: gentleFloat ${this._floatDuration}s ease-in-out infinite;
            }
            .lcd-widget:nth-child(2n) {
                animation-delay: -${this._floatDuration / 2}s;
            }
            .lcd-widget:nth-child(3n) {
                animation-delay: -${this._floatDuration / 3}s;
            }
            .lcd-widget:nth-child(4n) {
                animation-delay: -${this._floatDuration / 4}s;
            }
            @keyframes gentleFloat {
                0%, 100% {
                    transform: translateY(0);
                }
                50% {
                    transform: translateY(-${this._floatAmount}px);
                }
            }
        `;
        document.head.appendChild(style);
    },

    onBeforeRender: function(deltaTime) {},
    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        const style = document.getElementById('effect-gentle-float');
        if (style) style.remove();
    }
};
