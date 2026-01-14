/**
 * Lightning Effect
 * Occasional lightning flashes across background.
 */
window.LCDEffect = {
    _canvas: null,
    _ctx: null,
    _bolts: [],
    _nextFlash: 0,
    _time: 0,
    _flashAlpha: 0,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] lightning.onInit called', options);

        this._canvas = document.createElement('canvas');
        this._canvas.id = 'effect-lightning-canvas';
        this._canvas.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 1',
            'pointer-events: none'
        ].join(';');
        document.body.insertBefore(this._canvas, document.body.firstChild);

        this._ctx = this._canvas.getContext('2d');
        this._resize();
        this._nextFlash = 2 + Math.random() * 4;

        this._resizeHandler = function() { this._resize(); }.bind(this);
        window.addEventListener('resize', this._resizeHandler);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _resize: function() {
        this._canvas.width = window.innerWidth;
        this._canvas.height = window.innerHeight;
    },

    _createBolt: function(startX, startY, endX, endY) {
        var points = [{ x: startX, y: startY }];
        var segments = 8 + Math.floor(Math.random() * 8);
        var dx = (endX - startX) / segments;
        var dy = (endY - startY) / segments;

        for (var i = 1; i < segments; i++) {
            var x = startX + dx * i + (Math.random() - 0.5) * 100;
            var y = startY + dy * i + (Math.random() - 0.5) * 30;
            points.push({ x: x, y: y });

            // Branch
            if (Math.random() < 0.3) {
                var branchEnd = {
                    x: x + (Math.random() - 0.5) * 100,
                    y: y + 50 + Math.random() * 100
                };
                this._bolts.push({ points: [{ x: x, y: y }, branchEnd], alpha: 1, width: 1 });
            }
        }

        points.push({ x: endX, y: endY });
        return { points: points, alpha: 1, width: 3 };
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;
        this._update(deltaTime);
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _update: function(dt) {
        var w = this._canvas.width;
        var h = this._canvas.height;

        // Trigger flash
        if (this._time >= this._nextFlash) {
            var startX = Math.random() * w;
            this._bolts.push(this._createBolt(startX, 0, startX + (Math.random() - 0.5) * 200, h * 0.7));
            this._flashAlpha = 0.3;
            this._nextFlash = this._time + 3 + Math.random() * 6;
        }

        // Fade bolts
        this._bolts = this._bolts.filter(function(b) {
            b.alpha -= 3 * dt;
            return b.alpha > 0;
        });

        // Fade flash
        if (this._flashAlpha > 0) {
            this._flashAlpha -= 2 * dt;
        }
    },

    _render: function() {
        var ctx = this._ctx;
        var w = this._canvas.width;
        var h = this._canvas.height;

        ctx.clearRect(0, 0, w, h);

        // Draw flash
        if (this._flashAlpha > 0) {
            ctx.fillStyle = 'rgba(255, 255, 255, ' + this._flashAlpha + ')';
            ctx.fillRect(0, 0, w, h);
        }

        // Draw bolts
        for (var i = 0; i < this._bolts.length; i++) {
            var b = this._bolts[i];
            if (b.points.length < 2) continue;

            ctx.beginPath();
            ctx.moveTo(b.points[0].x, b.points[0].y);
            for (var j = 1; j < b.points.length; j++) {
                ctx.lineTo(b.points[j].x, b.points[j].y);
            }

            ctx.strokeStyle = 'rgba(200, 220, 255, ' + b.alpha + ')';
            ctx.lineWidth = b.width;
            ctx.shadowColor = 'rgba(150, 180, 255, ' + b.alpha + ')';
            ctx.shadowBlur = 20;
            ctx.stroke();

            // Inner bright line
            ctx.strokeStyle = 'rgba(255, 255, 255, ' + b.alpha + ')';
            ctx.lineWidth = b.width * 0.3;
            ctx.stroke();
        }
        ctx.shadowBlur = 0;
    },

    onBeforeRender: function(deltaTime) {},
    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},

    onDestroy: function() {
        if (this._animationId) {
            cancelAnimationFrame(this._animationId);
            this._animationId = null;
        }
        window.removeEventListener('resize', this._resizeHandler);
        if (this._canvas) this._canvas.remove();
    }
};
