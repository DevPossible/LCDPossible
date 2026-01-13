/**
 * Typewriter Effect
 * Text types out character by character on change.
 */
window.LCDEffect = {
    _previousValues: new Map(),
    _typeSpeed: 50,
    _activeAnimations: new Map(),

    onInit: function(options) {
        this._typeSpeed = options.typeSpeed || 50;

        const style = document.createElement('style');
        style.id = 'effect-typewriter';
        style.textContent = `
            .lcd-typewriter-cursor {
                display: inline-block;
                width: 2px;
                height: 1em;
                background: currentColor;
                margin-left: 2px;
                animation: blink 0.7s infinite;
            }
            @keyframes blink {
                0%, 50% { opacity: 1; }
                51%, 100% { opacity: 0; }
            }
        `;
        document.head.appendChild(style);
        this._captureValues();
    },

    _captureValues: function() {
        document.querySelectorAll('.lcd-stat-value, .lcd-stat-title').forEach(el => {
            const id = this._getElementId(el);
            this._previousValues.set(id, el.textContent?.trim() || '');
        });
    },

    _getElementId: function(el) {
        return el.id || el.dataset.effectId || (el.dataset.effectId = Math.random().toString(36).substr(2, 9));
    },

    _typeText: function(el, text) {
        const id = this._getElementId(el);

        // Cancel any existing animation
        if (this._activeAnimations.has(id)) {
            clearInterval(this._activeAnimations.get(id));
        }

        let index = 0;
        el.innerHTML = '<span class="lcd-typewriter-cursor"></span>';

        const interval = setInterval(() => {
            if (index < text.length) {
                el.innerHTML = text.substring(0, index + 1) + '<span class="lcd-typewriter-cursor"></span>';
                index++;
            } else {
                clearInterval(interval);
                this._activeAnimations.delete(id);
                // Remove cursor after typing is done
                setTimeout(() => {
                    el.textContent = text;
                }, 500);
            }
        }, this._typeSpeed);

        this._activeAnimations.set(id, interval);
    },

    onBeforeRender: function(deltaTime) {
        document.querySelectorAll('.lcd-stat-value').forEach(el => {
            const id = this._getElementId(el);
            const currentText = el.textContent?.trim().replace(/\u00A0/g, ' ') || '';
            const previousText = this._previousValues.get(id) || '';

            // Only animate if text actually changed (not just from our animation)
            if (previousText !== currentText && !this._activeAnimations.has(id)) {
                this._typeText(el, currentText);
            }

            this._previousValues.set(id, currentText);
        });
    },

    onValueChange: function(change) {},
    onAfterRender: function(deltaTime) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        this._activeAnimations.forEach(interval => clearInterval(interval));
        this._activeAnimations.clear();
        const style = document.getElementById('effect-typewriter');
        if (style) style.remove();
    }
};
