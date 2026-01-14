/**
 * Bounce Effect
 * Widgets move with random velocities and bounce off walls and each other.
 */
window.LCDEffect = {
    _widgets: [],
    _time: 0,
    _animationId: null,
    _lastFrameTime: 0,
    _bounciness: 0.8,
    _friction: 0.99,
    _speed: 50,

    onInit: function(options) {
        console.log('[EFFECT] bounce.onInit called', options);

        this._bounciness = options.bounciness || 0.8;
        this._friction = options.friction || 0.99;
        this._speed = options.speed || 50;

        // Add style for transformed widgets
        var style = document.createElement('style');
        style.id = 'effect-bounce-style';
        style.textContent = [
            '.lcd-widget.bounce-active {',
            '    position: relative !important;',
            '    transition: none !important;',
            '}'
        ].join('\n');
        document.head.appendChild(style);

        // Initialize widget physics data
        this._initializeWidgets();

        // Start animation loop
        this._lastFrameTime = performance.now();
        this._animate();
    },

    _initializeWidgets: function() {
        var self = this;
        this._widgets = [];

        var elements = document.querySelectorAll('.lcd-widget');
        elements.forEach(function(el) {
            var rect = el.getBoundingClientRect();

            // Random initial velocity
            var angle = Math.random() * Math.PI * 2;
            var speed = self._speed * (0.5 + Math.random() * 0.5);

            self._widgets.push({
                element: el,
                x: 0, // Offset from original position
                y: 0,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                width: rect.width,
                height: rect.height,
                originalLeft: rect.left,
                originalTop: rect.top,
                mass: rect.width * rect.height / 10000 // Mass based on size
            });

            el.classList.add('bounce-active');
        });
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        // Cap delta time to prevent huge jumps
        deltaTime = Math.min(deltaTime, 0.05);

        this._time += deltaTime;
        this._updatePhysics(deltaTime);
        this._render();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _updatePhysics: function(dt) {
        var self = this;
        var screenWidth = window.innerWidth;
        var screenHeight = window.innerHeight;

        // Update positions
        this._widgets.forEach(function(w) {
            // Apply friction
            w.vx *= self._friction;
            w.vy *= self._friction;

            // Add slight random perturbation to keep things moving
            if (Math.abs(w.vx) < 5 && Math.abs(w.vy) < 5) {
                var angle = Math.random() * Math.PI * 2;
                w.vx += Math.cos(angle) * 10;
                w.vy += Math.sin(angle) * 10;
            }

            // Update position
            w.x += w.vx * dt;
            w.y += w.vy * dt;

            // Calculate actual position on screen
            var actualLeft = w.originalLeft + w.x;
            var actualTop = w.originalTop + w.y;
            var actualRight = actualLeft + w.width;
            var actualBottom = actualTop + w.height;

            // Wall collisions
            if (actualLeft < 0) {
                w.x = -w.originalLeft;
                w.vx = Math.abs(w.vx) * self._bounciness;
            }
            if (actualRight > screenWidth) {
                w.x = screenWidth - w.originalLeft - w.width;
                w.vx = -Math.abs(w.vx) * self._bounciness;
            }
            if (actualTop < 0) {
                w.y = -w.originalTop;
                w.vy = Math.abs(w.vy) * self._bounciness;
            }
            if (actualBottom > screenHeight) {
                w.y = screenHeight - w.originalTop - w.height;
                w.vy = -Math.abs(w.vy) * self._bounciness;
            }
        });

        // Widget-to-widget collisions
        for (var i = 0; i < this._widgets.length; i++) {
            for (var j = i + 1; j < this._widgets.length; j++) {
                this._checkCollision(this._widgets[i], this._widgets[j]);
            }
        }
    },

    _checkCollision: function(a, b) {
        // Get actual positions
        var aLeft = a.originalLeft + a.x;
        var aTop = a.originalTop + a.y;
        var aRight = aLeft + a.width;
        var aBottom = aTop + a.height;

        var bLeft = b.originalLeft + b.x;
        var bTop = b.originalTop + b.y;
        var bRight = bLeft + b.width;
        var bBottom = bTop + b.height;

        // Check for overlap
        if (aRight < bLeft || aLeft > bRight || aBottom < bTop || aTop > bBottom) {
            return; // No collision
        }

        // Calculate centers
        var aCenterX = aLeft + a.width / 2;
        var aCenterY = aTop + a.height / 2;
        var bCenterX = bLeft + b.width / 2;
        var bCenterY = bTop + b.height / 2;

        // Calculate overlap
        var overlapX = (a.width + b.width) / 2 - Math.abs(aCenterX - bCenterX);
        var overlapY = (a.height + b.height) / 2 - Math.abs(aCenterY - bCenterY);

        // Resolve collision along axis of least overlap
        if (overlapX < overlapY) {
            // Horizontal collision
            var sign = aCenterX < bCenterX ? -1 : 1;
            a.x += sign * overlapX / 2;
            b.x -= sign * overlapX / 2;

            // Exchange velocities (simplified elastic collision)
            var totalMass = a.mass + b.mass;
            var aNewVx = ((a.mass - b.mass) * a.vx + 2 * b.mass * b.vx) / totalMass;
            var bNewVx = ((b.mass - a.mass) * b.vx + 2 * a.mass * a.vx) / totalMass;
            a.vx = aNewVx * this._bounciness;
            b.vx = bNewVx * this._bounciness;
        } else {
            // Vertical collision
            var sign = aCenterY < bCenterY ? -1 : 1;
            a.y += sign * overlapY / 2;
            b.y -= sign * overlapY / 2;

            // Exchange velocities
            var totalMass = a.mass + b.mass;
            var aNewVy = ((a.mass - b.mass) * a.vy + 2 * b.mass * b.vy) / totalMass;
            var bNewVy = ((b.mass - a.mass) * b.vy + 2 * a.mass * a.vy) / totalMass;
            a.vy = aNewVy * this._bounciness;
            b.vy = bNewVy * this._bounciness;
        }
    },

    _render: function() {
        this._widgets.forEach(function(w) {
            w.element.style.transform = 'translate(' + w.x + 'px, ' + w.y + 'px)';
        });
    },

    onBeforeRender: function(deltaTime) {
        // Animation handled internally via requestAnimationFrame
    },

    onAfterRender: function(deltaTime) {},
    onValueChange: function(change) {},
    onWarning: function(element, level) {},

    onDestroy: function() {
        if (this._animationId) {
            cancelAnimationFrame(this._animationId);
            this._animationId = null;
        }

        // Reset widget positions
        this._widgets.forEach(function(w) {
            w.element.style.transform = '';
            w.element.classList.remove('bounce-active');
        });

        var style = document.getElementById('effect-bounce-style');
        if (style) style.remove();
    }
};
