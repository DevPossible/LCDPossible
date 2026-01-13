using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// TV static / white noise effect.
/// </summary>
public sealed class NoisePanel : CanvasPanel
{
    private readonly Random _random;

    public override string PanelId => "noise";
    public override string DisplayName => "Static";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public NoisePanel()
    {
        _random = new Random();
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var image = new Image<Rgba32>(width, height);

        // Generate noise with occasional scan lines for authentic CRT feel
        var scanLineOffset = _random.Next(height);

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);

                // Occasional horizontal interference band
                var isScanLine = (y + scanLineOffset) % 50 < 2;
                var scanLineBrightness = isScanLine ? 0.3f : 1f;

                // Vertical sync glitch effect
                var glitchOffset = 0;
                if (_random.NextDouble() < 0.02) // 2% chance of glitch per line
                {
                    glitchOffset = _random.Next(-20, 20);
                }

                for (var x = 0; x < width; x++)
                {
                    var sourceX = (x + glitchOffset + width) % width;

                    // Generate noise value
                    var noise = (byte)(_random.Next(256) * scanLineBrightness);

                    // Occasional color tint
                    if (_random.NextDouble() < 0.01)
                    {
                        // Random color noise
                        row[sourceX] = new Rgba32(
                            (byte)_random.Next(256),
                            (byte)_random.Next(256),
                            (byte)_random.Next(256));
                    }
                    else
                    {
                        // Grayscale noise
                        row[sourceX] = new Rgba32(noise, noise, noise);
                    }
                }
            }
        });

        return Task.FromResult(image);
    }
}
