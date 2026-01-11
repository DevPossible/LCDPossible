using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Classic demoscene fire effect with palette animation.
/// </summary>
public sealed class FirePanel : BaseLivePanel
{
    private const int ScaleFactor = 4; // Render at lower resolution for performance

    private readonly Random _random;
    private byte[]? _fireBuffer;
    private Rgba32[]? _palette;
    private int _scaledWidth;
    private int _scaledHeight;

    public override string PanelId => "fire";
    public override string DisplayName => "Fire";
    public override bool IsAnimated => true;

    public FirePanel()
    {
        _random = new Random();
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Create fire palette: black -> red -> yellow -> white
        _palette = new Rgba32[256];

        for (var i = 0; i < 256; i++)
        {
            byte r, g, b;

            if (i < 64)
            {
                // Black to dark red
                r = (byte)(i * 4);
                g = 0;
                b = 0;
            }
            else if (i < 128)
            {
                // Dark red to orange
                r = 255;
                g = (byte)((i - 64) * 4);
                b = 0;
            }
            else if (i < 192)
            {
                // Orange to yellow
                r = 255;
                g = 255;
                b = (byte)((i - 128) * 4);
            }
            else
            {
                // Yellow to white
                r = 255;
                g = 255;
                b = 255;
            }

            _palette[i] = new Rgba32(r, g, b);
        }

        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        _scaledWidth = width / ScaleFactor;
        _scaledHeight = height / ScaleFactor;

        // Initialize buffer if needed
        if (_fireBuffer == null || _fireBuffer.Length != _scaledWidth * _scaledHeight)
        {
            _fireBuffer = new byte[_scaledWidth * _scaledHeight];
        }

        // Set random fire at bottom row
        var bottomRow = (_scaledHeight - 1) * _scaledWidth;
        for (var x = 0; x < _scaledWidth; x++)
        {
            _fireBuffer[bottomRow + x] = (byte)(_random.Next(256) > 180 ? 255 : _random.Next(200, 256));
        }

        // Propagate fire upward with cooling
        for (var y = 0; y < _scaledHeight - 1; y++)
        {
            for (var x = 0; x < _scaledWidth; x++)
            {
                // Sample from below with slight horizontal variation
                var srcX = x + _random.Next(-1, 2);
                if (srcX < 0) srcX = 0;
                if (srcX >= _scaledWidth) srcX = _scaledWidth - 1;

                var srcY = y + 1;
                if (srcY >= _scaledHeight) srcY = _scaledHeight - 1;

                var srcIndex = srcY * _scaledWidth + srcX;
                var dstIndex = y * _scaledWidth + x;

                // Cool down as fire rises
                var cooling = _random.Next(0, 4);
                var newValue = _fireBuffer[srcIndex] - cooling;
                if (newValue < 0) newValue = 0;

                _fireBuffer[dstIndex] = (byte)newValue;
            }
        }

        // Render to image
        var image = new Image<Rgba32>(width, height);

        image.ProcessPixelRows(accessor =>
        {
            for (var py = 0; py < height; py++)
            {
                var sy = py / ScaleFactor;
                if (sy >= _scaledHeight) sy = _scaledHeight - 1;

                var row = accessor.GetRowSpan(py);

                for (var px = 0; px < width; px++)
                {
                    var sx = px / ScaleFactor;
                    if (sx >= _scaledWidth) sx = _scaledWidth - 1;

                    var intensity = _fireBuffer[sy * _scaledWidth + sx];
                    row[px] = _palette![intensity];
                }
            }
        });

        return Task.FromResult(image);
    }
}
