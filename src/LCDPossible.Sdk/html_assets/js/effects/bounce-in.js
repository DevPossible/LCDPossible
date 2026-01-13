/**
 * Bounce In Effect
 * Widgets bounce in when panel first loads.
 */
window.LCDEffect = {
    _bounceDuration: 600,
    _staggerDelay: 100,
    _initialized: false,

    onInit: function(options) {
        this._bounceDuration = options.bounceDuration || 600;
        this._staggerDelay = options.staggerDelay || 100;

        const style = document.createElement('style');
        style.id = 'effect-bounce-in';
        style.textContent = `
            .lcd-widget.bounce-in-hidden {
                opacity: 0;
                transform: scale(0.3) translateY(50px);
            }
            .lcd-widget.bounce-in-animate {
                animation: bounceIn ${this._bounceDuration}ms cubic-bezier(0.68, -0.55, 0.265, 1.55) forwards;
            }
            @keyframes bounceIn {
                0% {
                    opacity: 0;
                    transform: scale(0.3) translateY(50px);
                }
                50% {
                    opacity: 1;
                    transform: scale(1.05) translateY(-10px);
                }
                70% {
                    transform: scale(0.95) translateY(5px);
                }
                100% {
                    opacity: 1;
                    transform: scale(1) translateY(0);
                }
            }
        `;
        document.head.appendChild(style);

        // Hide widgets initially and trigger bounce
        if (!this._initialized) {
            this._triggerBounce();
            this._initialized = true;
        }
    },

    _triggerBounce: function() {
        const widgets = document.querySelectorAll('.lcd-widget');

        // Hide all widgets first
        widgets.forEach(widget => {
            widget.classList.add('bounce-in-hidden');
        });

        // Stagger the bounce animation
        widgets.forEach((widget, index) => {
            setTimeout(() => {
                widget.classList.remove('bounce-in-hidden');
                widget.classList.add('bounce-in-animate');
            }, index * this._staggerDelay);
        });
    },

    onBeforeRender: function(deltaTime) {},
    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        document.querySelectorAll('.lcd-widget').forEach(widget => {
            widget.classList.remove('bounce-in-hidden', 'bounce-in-animate');
        });
        const style = document.getElementById('effect-bounce-in');
        if (style) style.remove();
    }
};
