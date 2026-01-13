/**
 * Slide Numbers Effect
 * Digits slide up/down like a slot machine when values change.
 */
window.LCDEffect = {
    _previousValues: new Map(),
    _slideDuration: 400,

    onInit: function(options) {
        this._slideDuration = options.slideDuration || 400;

        const style = document.createElement('style');
        style.id = 'effect-slide-numbers';
        style.textContent = `
            .lcd-slide-digit {
                display: inline-block;
                position: relative;
                overflow: hidden;
                height: 1.2em;
                vertical-align: bottom;
            }
            .lcd-slide-digit-inner {
                display: inline-block;
                transition: transform ${this._slideDuration}ms cubic-bezier(0.4, 0, 0.2, 1);
            }
            .lcd-slide-digit.slide-up .lcd-slide-digit-inner {
                animation: slideUp ${this._slideDuration}ms cubic-bezier(0.4, 0, 0.2, 1);
            }
            .lcd-slide-digit.slide-down .lcd-slide-digit-inner {
                animation: slideDown ${this._slideDuration}ms cubic-bezier(0.4, 0, 0.2, 1);
            }
            @keyframes slideUp {
                0% { transform: translateY(100%); }
                100% { transform: translateY(0); }
            }
            @keyframes slideDown {
                0% { transform: translateY(-100%); }
                100% { transform: translateY(0); }
            }
        `;
        document.head.appendChild(style);
        this._captureValues();
    },

    _captureValues: function() {
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const id = this._getElementId(el);
            this._previousValues.set(id, el.textContent?.trim() || '');
        });
    },

    _getElementId: function(el) {
        return el.id || el.dataset.effectId || (el.dataset.effectId = Math.random().toString(36).substr(2, 9));
    },

    onBeforeRender: function(deltaTime) {
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const id = this._getElementId(el);
            const currentText = el.textContent?.trim() || '';
            const previousText = this._previousValues.get(id) || '';

            if (previousText !== currentText) {
                // Wrap each digit
                const wrapped = currentText.split('').map((char, i) => {
                    if (/\d/.test(char)) {
                        const prevChar = previousText[i];
                        const direction = parseInt(char) > parseInt(prevChar || '0') ? 'up' : 'down';
                        return `<span class="lcd-slide-digit slide-${direction}"><span class="lcd-slide-digit-inner">${char}</span></span>`;
                    }
                    return char;
                }).join('');

                el.innerHTML = wrapped;

                // Remove animation classes after animation completes
                setTimeout(() => {
                    el.querySelectorAll('.lcd-slide-digit').forEach(digit => {
                        digit.classList.remove('slide-up', 'slide-down');
                    });
                }, this._slideDuration);
            }

            this._previousValues.set(id, currentText);
        });
    },

    onValueChange: function(change) {},
    onAfterRender: function(deltaTime) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        const style = document.getElementById('effect-slide-numbers');
        if (style) style.remove();
    }
};
