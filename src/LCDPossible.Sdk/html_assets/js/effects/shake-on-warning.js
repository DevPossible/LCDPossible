/**
 * Shake on Warning Effect
 * Containers shake when values hit warning/critical thresholds.
 */
window.LCDEffect = {
    _warningThreshold: 70,
    _criticalThreshold: 90,
    _shakeIntensity: 3,
    _shakeDuration: 500,

    onInit: function(options) {
        this._warningThreshold = options.warningThreshold || 70;
        this._criticalThreshold = options.criticalThreshold || 90;
        this._shakeIntensity = options.shakeIntensity || 3;
        this._shakeDuration = options.shakeDuration || 500;

        const style = document.createElement('style');
        style.id = 'effect-shake-on-warning';
        style.textContent = `
            .lcd-shake-warning {
                animation: shakeWarning ${this._shakeDuration}ms ease-in-out;
            }
            .lcd-shake-critical {
                animation: shakeCritical ${this._shakeDuration}ms ease-in-out;
            }
            @keyframes shakeWarning {
                0%, 100% { transform: translateX(0); }
                10%, 30%, 50%, 70%, 90% { transform: translateX(-${this._shakeIntensity}px); }
                20%, 40%, 60%, 80% { transform: translateX(${this._shakeIntensity}px); }
            }
            @keyframes shakeCritical {
                0%, 100% { transform: translateX(0) rotate(0); }
                10%, 30%, 50%, 70%, 90% { transform: translateX(-${this._shakeIntensity * 2}px) rotate(-1deg); }
                20%, 40%, 60%, 80% { transform: translateX(${this._shakeIntensity * 2}px) rotate(1deg); }
            }
        `;
        document.head.appendChild(style);
    },

    _checkValue: function(el) {
        const text = el.textContent || '';
        const match = text.match(/(\d+(?:\.\d+)?)/);
        if (match) {
            return parseFloat(match[1]);
        }
        return null;
    },

    onBeforeRender: function(deltaTime) {
        document.querySelectorAll('.lcd-widget').forEach(widget => {
            const valueEl = widget.querySelector('.lcd-stat-value, .lcd-donut-value');
            if (!valueEl) return;

            const value = this._checkValue(valueEl);
            if (value === null) return;

            const isWarning = value >= this._warningThreshold && value < this._criticalThreshold;
            const isCritical = value >= this._criticalThreshold;

            // Only shake on state change
            if (isCritical && !widget.classList.contains('lcd-shake-critical')) {
                widget.classList.remove('lcd-shake-warning');
                widget.classList.add('lcd-shake-critical');
                setTimeout(() => widget.classList.remove('lcd-shake-critical'), this._shakeDuration);
            } else if (isWarning && !widget.classList.contains('lcd-shake-warning')) {
                widget.classList.remove('lcd-shake-critical');
                widget.classList.add('lcd-shake-warning');
                setTimeout(() => widget.classList.remove('lcd-shake-warning'), this._shakeDuration);
            }
        });
    },

    onWarning: function(element, level) {
        const shakeClass = level === 'critical' ? 'lcd-shake-critical' : 'lcd-shake-warning';
        element.classList.add(shakeClass);
        setTimeout(() => element.classList.remove(shakeClass), this._shakeDuration);
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onDestroy: function() {
        document.querySelectorAll('.lcd-shake-warning, .lcd-shake-critical').forEach(el => {
            el.classList.remove('lcd-shake-warning', 'lcd-shake-critical');
        });
        const style = document.getElementById('effect-shake-on-warning');
        if (style) style.remove();
    }
};
