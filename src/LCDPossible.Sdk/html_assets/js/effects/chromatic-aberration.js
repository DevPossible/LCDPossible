/**
 * Chromatic Aberration Effect
 * RGB split/shift effect applied to the entire page.
 */
window.LCDEffect = {
    _time: 0,
    _container: null,
    _redLayer: null,
    _cyanLayer: null,
    _animationId: null,
    _lastFrameTime: 0,

    onInit: function(options) {
        console.log('[EFFECT] chromatic-aberration.onInit called', options);

        // Create overlay container
        this._container = document.createElement('div');
        this._container.id = 'effect-chromatic-aberration';
        this._container.style.cssText = [
            'position: fixed',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'z-index: 9990',
            'pointer-events: none',
            'overflow: hidden'
        ].join(';');

        // Red channel shift (left edge glow)
        this._redLayer = document.createElement('div');
        this._redLayer.style.cssText = [
            'position: absolute',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'background: linear-gradient(90deg, rgba(255,0,0,0.15) 0%, transparent 5%, transparent 95%, rgba(255,0,0,0.15) 100%)',
            'pointer-events: none'
        ].join(';');

        // Cyan channel shift (right edge glow)
        this._cyanLayer = document.createElement('div');
        this._cyanLayer.style.cssText = [
            'position: absolute',
            'top: 0',
            'left: 0',
            'width: 100%',
            'height: 100%',
            'background: linear-gradient(90deg, rgba(0,255,255,0.15) 0%, transparent 5%, transparent 95%, rgba(0,255,255,0.15) 100%)',
            'pointer-events: none'
        ].join(';');

        this._container.appendChild(this._redLayer);
        this._container.appendChild(this._cyanLayer);
        document.body.appendChild(this._container);

        this._lastFrameTime = performance.now();
        this._animate();
    },

    _animate: function() {
        var self = this;
        var now = performance.now();
        var deltaTime = (now - this._lastFrameTime) / 1000;
        this._lastFrameTime = now;

        this._time += deltaTime;
        this._update();

        this._animationId = requestAnimationFrame(function() { self._animate(); });
    },

    _update: function() {
        // Oscillating shift amount
        var shift = Math.sin(this._time * 0.8) * 3;

        // Move the color layers in opposite directions
        this._redLayer.style.transform = 'translateX(' + (-shift) + 'px)';
        this._cyanLayer.style.transform = 'translateX(' + shift + 'px)';

        // Vary opacity slightly for pulsing effect
        var opacity = 0.12 + Math.sin(this._time * 1.5) * 0.05;
        this._redLayer.style.opacity = opacity;
        this._cyanLayer.style.opacity = opacity;
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
        if (this._container) this._container.remove();
    }
};
