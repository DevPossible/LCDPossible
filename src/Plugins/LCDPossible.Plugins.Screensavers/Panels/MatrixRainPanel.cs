using LCDPossible.Sdk;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Digital rain effect inspired by The Matrix.
/// </summary>
public sealed class MatrixRainPanel : CanvasPanel
{
    private const int CharSize = 16;
    private const int TrailLength = 20;
    private const float BaseSpeed = 80f; // Pixels per second

    // Matrix-style characters (katakana-inspired ASCII subset)
    private static readonly char[] MatrixChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@#$%&*+=<>?".ToCharArray();

    private readonly Random _random;
    private Column[]? _columns;
    private DateTime _lastUpdate;
    private int _width;
    private int _height;
    private Font? _matrixFont;

    public override string PanelId => "matrix-rain";
    public override string DisplayName => "Matrix Rain";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public MatrixRainPanel()
    {
        _random = new Random();
        _lastUpdate = DateTime.UtcNow;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Load a monospace font for the matrix effect
        _matrixFont = FontHelper.GetPreferredMonoFont(CharSize, FontStyle.Bold);
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        // Initialize columns if dimensions changed
        if (_width != width || _height != height || _columns == null)
        {
            _width = width;
            _height = height;
            InitializeColumns();
        }

        // Calculate delta time
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update columns
        UpdateColumns(deltaTime);

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            foreach (var column in _columns!)
            {
                DrawColumn(ctx, column);
            }
        });

        return Task.FromResult(image);
    }

    private void InitializeColumns()
    {
        var columnCount = _width / CharSize + 1;
        _columns = new Column[columnCount];

        for (var i = 0; i < columnCount; i++)
        {
            _columns[i] = CreateColumn(i, randomStart: true);
        }
    }

    private Column CreateColumn(int index, bool randomStart)
    {
        var speed = BaseSpeed * (0.5f + _random.NextSingle());
        var startY = randomStart ? -_random.Next(_height * 2) : -TrailLength * CharSize;

        var chars = new char[TrailLength];
        for (var j = 0; j < TrailLength; j++)
        {
            chars[j] = MatrixChars[_random.Next(MatrixChars.Length)];
        }

        return new Column
        {
            X = index * CharSize,
            Y = startY,
            Speed = speed,
            Characters = chars,
            CharChangeTimer = 0
        };
    }

    private void UpdateColumns(float deltaTime)
    {
        for (var i = 0; i < _columns!.Length; i++)
        {
            var column = _columns[i];
            column.Y += column.Speed * deltaTime;

            // Randomly change characters
            column.CharChangeTimer += deltaTime;
            if (column.CharChangeTimer > 0.1f)
            {
                column.CharChangeTimer = 0;
                var charIndex = _random.Next(column.Characters.Length);
                column.Characters[charIndex] = MatrixChars[_random.Next(MatrixChars.Length)];
            }

            // Reset column when it goes off screen
            if (column.Y > _height + TrailLength * CharSize)
            {
                _columns[i] = CreateColumn(i, randomStart: false);
            }
            else
            {
                _columns[i] = column;
            }
        }
    }

    private void DrawColumn(IImageProcessingContext ctx, Column column)
    {
        for (var i = 0; i < column.Characters.Length; i++)
        {
            var charY = column.Y - i * CharSize;

            // Skip if off screen
            if (charY < -CharSize || charY > _height)
            {
                continue;
            }

            // Calculate brightness - head is brightest, fades toward tail
            float brightness;
            Rgba32 color;

            if (i == 0)
            {
                // Leading character is white/bright green
                brightness = 1f;
                color = new Rgba32(200, 255, 200);
            }
            else
            {
                // Fade from bright green to dark green
                brightness = 1f - (float)i / TrailLength;
                var green = (byte)(brightness * 200 + 55);
                var red = (byte)(brightness * 50);
                color = new Rgba32(red, green, 0);
            }

            if (_matrixFont != null)
            {
                var text = column.Characters[i].ToString();
                ctx.DrawText(text, _matrixFont, color, new PointF(column.X, charY));
            }
            else
            {
                // Fallback: draw rectangles if no font
                var rect = new RectangleF(column.X + 2, charY + 2, CharSize - 4, CharSize - 4);
                ctx.Fill(color, rect);
            }
        }
    }

    private struct Column
    {
        public int X;
        public float Y;
        public float Speed;
        public char[] Characters;
        public float CharChangeTimer;
    }
}
