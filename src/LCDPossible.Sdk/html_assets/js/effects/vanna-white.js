/**
 * Vanna White Effect
 * Character walks up to tiles and gestures at them.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _character: null,
    _targetWidget: null,
    _state: 'idle', // idle, walking, presenting
    _time: 0,
    _presentDuration: 3,

    onInit: function(options) {
        this._presentDuration = options.presentDuration || 3;

        // Create canvas overlay
        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-vanna-white-canvas';
        this._canvas.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: 9998;
            pointer-events: none;
        `;
        document.body.appendChild(this._canvas);

        this._ctx = this._canvas.getContext('2d');
        this._resize();

        this._character = {
            x: -50,
            y: this._canvas.height - 120,
            targetX: 100,
            speed: 150,
            frame: 0,
            facing: 1 // 1 = right, -1 = left
        };

        this._resizeHandler = () => this._resize();
        window.addEventListener('resize', this._resizeHandler);

        this._selectNewTarget();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
        if (this._character) {
            this._character.y = this._canvas.height - 120;
        }
    },

    _selectNewTarget: function() {
        const widgets = document.querySelectorAll('.lcd-widget');
        if (widgets.length === 0) return;

        const randomWidget = widgets[Math.floor(Math.random() * widgets.length)];
        const rect = randomWidget.getBoundingClientRect();

        this._targetWidget = randomWidget;
        this._character.targetX = rect.left + rect.width / 2 - 30;
        this._character.facing = this._character.targetX > this._character.x ? 1 : -1;
        this._state = 'walking';
    },

    _drawCharacter: function() {
        const ctx = this._ctx;
        const c = this._character;

        ctx.save();
        ctx.translate(c.x + 30, c.y);
        ctx.scale(c.facing, 1);

        // Simple stick figure with dress
        const walkCycle = Math.sin(c.frame * 0.3);

        // Head
        ctx.fillStyle = '#ffdbac';
        ctx.beginPath();
        ctx.arc(0, -80, 15, 0, Math.PI * 2);
        ctx.fill();

        // Hair
        ctx.fillStyle = '#4a3728';
        ctx.beginPath();
        ctx.arc(0, -85, 15, Math.PI, Math.PI * 2);
        ctx.fill();
        ctx.fillRect(-15, -85, 30, 10);

        // Body/Dress
        ctx.fillStyle = '#ff69b4';
        ctx.beginPath();
        ctx.moveTo(0, -65);
        ctx.lineTo(-20, 0);
        ctx.lineTo(20, 0);
        ctx.closePath();
        ctx.fill();

        // Arms
        ctx.strokeStyle = '#ffdbac';
        ctx.lineWidth = 6;
        ctx.lineCap = 'round';

        if (this._state === 'presenting') {
            // Gesture pose - arm pointing up
            ctx.beginPath();
            ctx.moveTo(10, -50);
            ctx.lineTo(30, -70);
            ctx.lineTo(40, -90);
            ctx.stroke();

            // Sparkle near hand
            const sparkle = Math.sin(this._time * 10) * 0.5 + 0.5;
            ctx.fillStyle = `rgba(255, 255, 0, ${sparkle})`;
            ctx.beginPath();
            ctx.arc(45, -95, 5, 0, Math.PI * 2);
            ctx.fill();
        } else {
            // Walking arms
            ctx.beginPath();
            ctx.moveTo(-10, -50);
            ctx.lineTo(-20 - walkCycle * 10, -30);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(10, -50);
            ctx.lineTo(20 + walkCycle * 10, -30);
            ctx.stroke();
        }

        // Legs
        ctx.strokeStyle = '#ffdbac';
        if (this._state === 'walking') {
            ctx.beginPath();
            ctx.moveTo(-8, 0);
            ctx.lineTo(-8 - walkCycle * 15, 40);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(8, 0);
            ctx.lineTo(8 + walkCycle * 15, 40);
            ctx.stroke();
        } else {
            ctx.beginPath();
            ctx.moveTo(-8, 0);
            ctx.lineTo(-12, 40);
            ctx.stroke();
            ctx.beginPath();
            ctx.moveTo(8, 0);
            ctx.lineTo(12, 40);
            ctx.stroke();
        }

        // Heels
        ctx.fillStyle = '#ff69b4';
        ctx.fillRect(-18 - (this._state === 'walking' ? walkCycle * 15 : 4), 35, 15, 8);
        ctx.fillRect(2 + (this._state === 'walking' ? walkCycle * 15 : 4), 35, 15, 8);

        ctx.restore();
    },

    _highlightWidget: function() {
        if (!this._targetWidget) return;

        const rect = this._targetWidget.getBoundingClientRect();
        const ctx = this._ctx;

        // Draw highlight around widget
        const pulse = Math.sin(this._time * 5) * 0.3 + 0.7;
        ctx.strokeStyle = `rgba(255, 215, 0, ${pulse})`;
        ctx.lineWidth = 3;
        ctx.setLineDash([5, 5]);
        ctx.strokeRect(rect.left - 5, rect.top - 5, rect.width + 10, rect.height + 10);
        ctx.setLineDash([]);
    },

    onBeforeRender: function(deltaTime) {
        if (!this._ctx) return;

        const dt = deltaTime || 0.016;
        this._time += dt;
        this._character.frame += dt * 60;

        // Clear canvas
        this._ctx.clearRect(0, 0, this._canvas.width, this._canvas.height);

        // State machine
        if (this._state === 'walking') {
            const dx = this._character.targetX - this._character.x;
            if (Math.abs(dx) < 5) {
                this._state = 'presenting';
                this._presentStartTime = this._time;
            } else {
                this._character.x += Math.sign(dx) * this._character.speed * dt;
            }
        } else if (this._state === 'presenting') {
            this._highlightWidget();
            if (this._time - this._presentStartTime > this._presentDuration) {
                this._selectNewTarget();
            }
        }

        this._drawCharacter();
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},
    onDestroy: function() {
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
