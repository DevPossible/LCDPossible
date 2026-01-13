/**
 * Flip Digits Effect
 * Numbers flip like an airport departure board when changing.
 */
window.LCDEffect = {
    _previousValues: new Map(),
    _flipDuration: 300,

    onInit: function(options) {
        this._flipDuration = options.flipDuration || 300;

        const style = document.createElement('style');
        style.id = 'effect-flip-digits';
        style.textContent = `
            .lcd-flip-container {
                perspective: 200px;
                display: inline-block;
            }
            .lcd-flip-digit {
                display: inline-block;
                position: relative;
            }
            .lcd-flip-digit.flipping {
                animation: flipDigit ${this._flipDuration}ms ease-in-out;
            }
            @keyframes flipDigit {
                0% { transform: rotateX(0deg); }
                50% { transform: rotateX(-90deg); }
                100% { transform: rotateX(0deg); }
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

    _wrapDigits: function(el) {
        const text = el.textContent || '';
        const wrapped = text.split('').map((char, i) => {
            if (/\d/.test(char)) {
                return `<span class="lcd-flip-digit" data-index="${i}">${char}</span>`;
            }
            return char;
        }).join('');
        el.innerHTML = wrapped;
    },

    onBeforeRender: function(deltaTime) {
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const id = this._getElementId(el);
            const currentValue = el.textContent?.trim() || '';
            const previousValue = this._previousValues.get(id) || '';

            if (previousValue !== currentValue) {
                // Wrap digits if not already wrapped
                if (!el.querySelector('.lcd-flip-digit')) {
                    this._wrapDigits(el);
                }

                // Find changed digits and animate them
                el.querySelectorAll('.lcd-flip-digit').forEach((digit, i) => {
                    const prevChar = previousValue[i];
                    const currChar = currentValue[i];
                    if (prevChar !== currChar) {
                        digit.classList.add('flipping');
                        setTimeout(() => digit.classList.remove('flipping'), this._flipDuration);
                    }
                });
            }

            this._previousValues.set(id, currentValue);
        });
    },

    onValueChange: function(change) {},
    onAfterRender: function(deltaTime) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        const style = document.getElementById('effect-flip-digits');
        if (style) style.remove();
    }
};
