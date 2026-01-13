/**
 * Warning Flash Effect
 * Panel border flashes when any value is critical.
 */
window.LCDEffect = {
    _warningThreshold: 70,
    _criticalThreshold: 90,
    _flashSpeed: 2,
    _time: 0,
    _currentLevel: 'normal',

    onInit: function(options) {
        this._warningThreshold = options.warningThreshold || 70;
        this._criticalThreshold = options.criticalThreshold || 90;
        this._flashSpeed = options.flashSpeed || 2;

        const style = document.createElement('style');
        style.id = 'effect-warning-flash-style';
        style.textContent = `
            .lcd-warning-overlay {
                position: fixed;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                pointer-events: none;
                z-index: 9997;
                border: 4px solid transparent;
                box-sizing: border-box;
            }
            .lcd-warning-overlay.warning {
                animation: warningFlash 0.5s ease-in-out infinite;
            }
            .lcd-warning-overlay.critical {
                animation: criticalFlash 0.25s ease-in-out infinite;
            }
            @keyframes warningFlash {
                0%, 100% { border-color: transparent; background: transparent; }
                50% { border-color: rgba(255, 165, 0, 0.8); background: rgba(255, 165, 0, 0.05); }
            }
            @keyframes criticalFlash {
                0%, 100% { border-color: transparent; background: transparent; }
                50% { border-color: rgba(255, 0, 0, 0.9); background: rgba(255, 0, 0, 0.1); }
            }
            .lcd-widget.warning-highlight {
                box-shadow: 0 0 20px rgba(255, 165, 0, 0.5);
            }
            .lcd-widget.critical-highlight {
                box-shadow: 0 0 20px rgba(255, 0, 0, 0.7);
            }
        `;
        document.head.appendChild(style);

        // Create overlay
        const overlay = document.createElement('div');
        overlay.id = 'effect-warning-flash-overlay';
        overlay.className = 'lcd-warning-overlay';
        document.body.appendChild(overlay);
    },

    _checkSystemState: function() {
        let maxLevel = 'normal';
        const warningWidgets = [];
        const criticalWidgets = [];

        document.querySelectorAll('.lcd-widget').forEach(widget => {
            widget.classList.remove('warning-highlight', 'critical-highlight');

            const valueEl = widget.querySelector('.lcd-stat-value');
            if (!valueEl) return;

            const text = valueEl.textContent || '';
            const match = text.match(/(\d+(?:\.\d+)?)/);
            if (!match) return;

            const value = parseFloat(match[1]);

            if (value >= this._criticalThreshold) {
                maxLevel = 'critical';
                widget.classList.add('critical-highlight');
                criticalWidgets.push(widget);
            } else if (value >= this._warningThreshold) {
                if (maxLevel !== 'critical') maxLevel = 'warning';
                widget.classList.add('warning-highlight');
                warningWidgets.push(widget);
            }
        });

        return maxLevel;
    },

    onBeforeRender: function(deltaTime) {
        this._time += deltaTime || 0.016;

        const level = this._checkSystemState();
        const overlay = document.getElementById('effect-warning-flash-overlay');

        if (overlay) {
            overlay.classList.remove('warning', 'critical');
            if (level !== 'normal') {
                overlay.classList.add(level);
            }
        }

        this._currentLevel = level;
    },

    onWarning: function(element, level) {
        if (level === 'critical') {
            element.classList.add('critical-highlight');
        } else {
            element.classList.add('warning-highlight');
        }
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onDestroy: function() {
        document.querySelectorAll('.warning-highlight, .critical-highlight').forEach(el => {
            el.classList.remove('warning-highlight', 'critical-highlight');
        });
        const overlay = document.getElementById('effect-warning-flash-overlay');
        if (overlay) overlay.remove();
        const style = document.getElementById('effect-warning-flash-style');
        if (style) style.remove();
    }
};
