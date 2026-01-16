using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LCDPossible.VirtualLcd.Protocols;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.VirtualLcd.Views;

/// <summary>
/// Custom control for efficiently displaying LCD frames.
/// Uses WriteableBitmap for direct pixel access.
/// </summary>
public class LcdDisplayControl : Control
{
    private WriteableBitmap? _framebuffer;
    private readonly object _framebufferLock = new();
    private int _displayWidth;
    private int _displayHeight;

    /// <summary>
    /// Defines the Width property.
    /// </summary>
    public static readonly StyledProperty<int> DisplayWidthProperty =
        AvaloniaProperty.Register<LcdDisplayControl, int>(nameof(DisplayWidth), 1280);

    /// <summary>
    /// Defines the Height property.
    /// </summary>
    public static readonly StyledProperty<int> DisplayHeightProperty =
        AvaloniaProperty.Register<LcdDisplayControl, int>(nameof(DisplayHeight), 480);

    /// <summary>
    /// Defines the Stretch property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<LcdDisplayControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Display width in pixels.
    /// </summary>
    public int DisplayWidth
    {
        get => GetValue(DisplayWidthProperty);
        set => SetValue(DisplayWidthProperty, value);
    }

    /// <summary>
    /// Display height in pixels.
    /// </summary>
    public int DisplayHeight
    {
        get => GetValue(DisplayHeightProperty);
        set => SetValue(DisplayHeightProperty, value);
    }

    /// <summary>
    /// How to stretch the display to fit the control.
    /// </summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    static LcdDisplayControl()
    {
        AffectsRender<LcdDisplayControl>(DisplayWidthProperty, DisplayHeightProperty, StretchProperty);
        AffectsMeasure<LcdDisplayControl>(DisplayWidthProperty, DisplayHeightProperty);
    }

    public LcdDisplayControl()
    {
        // Set initial size
        _displayWidth = DisplayWidth;
        _displayHeight = DisplayHeight;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DisplayWidthProperty || change.Property == DisplayHeightProperty)
        {
            _displayWidth = DisplayWidth;
            _displayHeight = DisplayHeight;

            lock (_framebufferLock)
            {
                _framebuffer?.Dispose();
                _framebuffer = null;
            }

            InvalidateVisual();
        }
    }

    /// <summary>
    /// Update the display with new frame data.
    /// Thread-safe - can be called from any thread.
    /// </summary>
    /// <param name="imageData">Image data (JPEG or raw pixels).</param>
    /// <param name="format">Format of the image data.</param>
    public void UpdateFrame(byte[] imageData, ImageFormat format)
    {
        try
        {
            // Decode image based on format
            using var image = format switch
            {
                ImageFormat.Jpeg => DecodeJpeg(imageData),
                ImageFormat.Rgb565 => DecodeRgb565(imageData),
                ImageFormat.Rgb888 => DecodeRgb888(imageData),
                _ => DecodeJpeg(imageData)
            };

            // Resize if needed
            if (image.Width != _displayWidth || image.Height != _displayHeight)
            {
                image.Mutate(x => x.Resize(_displayWidth, _displayHeight));
            }

            // Copy to framebuffer
            lock (_framebufferLock)
            {
                EnsureFramebuffer();

                if (_framebuffer != null)
                {
                    using var fb = _framebuffer.Lock();
                    var bufferSize = fb.RowBytes * fb.Size.Height;
                    var pixelData = new byte[bufferSize];
                    image.CopyPixelDataTo(pixelData);
                    System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, fb.Address, bufferSize);
                }
            }

            // Trigger repaint on UI thread
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            // Log error but don't crash - frame will just be skipped
            System.Diagnostics.Debug.WriteLine($"Frame decode error: {ex.Message}");
        }
    }

    private void EnsureFramebuffer()
    {
        if (_framebuffer == null || _framebuffer.PixelSize.Width != _displayWidth || _framebuffer.PixelSize.Height != _displayHeight)
        {
            _framebuffer?.Dispose();
            _framebuffer = new WriteableBitmap(
                new PixelSize(_displayWidth, _displayHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                AlphaFormat.Opaque);
        }
    }

    private Image<Bgra32> DecodeJpeg(byte[] data)
    {
        return SixLabors.ImageSharp.Image.Load<Bgra32>(data);
    }

    private Image<Bgra32> DecodeRgb565(byte[] data)
    {
        // RGB565: 2 bytes per pixel, 5 bits R, 6 bits G, 5 bits B
        var image = new Image<Bgra32>(_displayWidth, _displayHeight);

        var pixelCount = Math.Min(data.Length / 2, _displayWidth * _displayHeight);

        for (var i = 0; i < pixelCount; i++)
        {
            var pixel = (ushort)(data[i * 2] | (data[i * 2 + 1] << 8));

            var r = (byte)(((pixel >> 11) & 0x1F) * 255 / 31);
            var g = (byte)(((pixel >> 5) & 0x3F) * 255 / 63);
            var b = (byte)((pixel & 0x1F) * 255 / 31);

            var x = i % _displayWidth;
            var y = i / _displayWidth;
            image[x, y] = new Bgra32(b, g, r, 255);
        }

        return image;
    }

    private Image<Bgra32> DecodeRgb888(byte[] data)
    {
        // RGB888: 3 bytes per pixel
        var image = new Image<Bgra32>(_displayWidth, _displayHeight);

        var pixelCount = Math.Min(data.Length / 3, _displayWidth * _displayHeight);

        for (var i = 0; i < pixelCount; i++)
        {
            var r = data[i * 3];
            var g = data[i * 3 + 1];
            var b = data[i * 3 + 2];

            var x = i % _displayWidth;
            var y = i / _displayWidth;
            image[x, y] = new Bgra32(b, g, r, 255);
        }

        return image;
    }

    protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize)
    {
        // Desired size is the display dimensions
        return new Avalonia.Size(_displayWidth, _displayHeight);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // Fill background
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

        lock (_framebufferLock)
        {
            if (_framebuffer == null)
            {
                // No frame yet - draw placeholder
                DrawPlaceholder(context);
                return;
            }

            // Calculate destination rectangle based on stretch mode
            var destRect = CalculateDestRect();
            context.DrawImage(_framebuffer, destRect);
        }
    }

    private void DrawPlaceholder(DrawingContext context)
    {
        var text = new FormattedText(
            "Waiting for frames...",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal),
            16,
            Brushes.Gray);

        var textPos = new Avalonia.Point(
            (Bounds.Width - text.Width) / 2,
            (Bounds.Height - text.Height) / 2);

        context.DrawText(text, textPos);
    }

    private Rect CalculateDestRect()
    {
        var sourceSize = new Avalonia.Size(_displayWidth, _displayHeight);
        var destSize = Bounds.Size;

        return Stretch switch
        {
            Stretch.None => new Rect(
                (destSize.Width - sourceSize.Width) / 2,
                (destSize.Height - sourceSize.Height) / 2,
                sourceSize.Width,
                sourceSize.Height),

            Stretch.Fill => new Rect(0, 0, destSize.Width, destSize.Height),

            Stretch.Uniform => CalculateUniformRect(sourceSize, destSize),

            Stretch.UniformToFill => CalculateUniformToFillRect(sourceSize, destSize),

            _ => new Rect(0, 0, destSize.Width, destSize.Height)
        };
    }

    private static Rect CalculateUniformRect(Avalonia.Size source, Avalonia.Size dest)
    {
        var scale = Math.Min(dest.Width / source.Width, dest.Height / source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        var x = (dest.Width - width) / 2;
        var y = (dest.Height - height) / 2;
        return new Rect(x, y, width, height);
    }

    private static Rect CalculateUniformToFillRect(Avalonia.Size source, Avalonia.Size dest)
    {
        var scale = Math.Max(dest.Width / source.Width, dest.Height / source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        var x = (dest.Width - width) / 2;
        var y = (dest.Height - height) / 2;
        return new Rect(x, y, width, height);
    }
}
