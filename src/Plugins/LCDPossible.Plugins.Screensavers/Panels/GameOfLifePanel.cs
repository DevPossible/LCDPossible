using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Conway's Game of Life cellular automaton.
/// </summary>
public sealed class GameOfLifePanel : BaseLivePanel
{
    private const int CellSize = 4;
    private const float ResetInterval = 60f; // Reset after 60 seconds of stagnation

    private readonly Random _random;
    private bool[]? _grid;
    private bool[]? _nextGrid;
    private int _gridWidth;
    private int _gridHeight;
    private int _generation;
    private int _lastPopulation;
    private int _stagnantFrames;
    private DateTime _lastUpdate;

    public override string PanelId => "game-of-life";
    public override string DisplayName => "Game of Life";
    public override bool IsAnimated => true;

    public GameOfLifePanel()
    {
        _random = new Random();
        _lastUpdate = DateTime.UtcNow;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var newGridWidth = width / CellSize;
        var newGridHeight = height / CellSize;

        // Initialize or reset grid if dimensions changed
        if (_grid == null || _gridWidth != newGridWidth || _gridHeight != newGridHeight)
        {
            _gridWidth = newGridWidth;
            _gridHeight = newGridHeight;
            RandomizeGrid();
        }

        // Throttle updates for visual effect
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;

        if (deltaTime >= 0.1f) // Update ~10 times per second
        {
            _lastUpdate = now;
            UpdateGrid();
            _generation++;

            // Check for stagnation and reset if needed
            var population = CountPopulation();
            if (population == _lastPopulation)
            {
                _stagnantFrames++;
                if (_stagnantFrames > 100) // Reset after ~10 seconds of no change
                {
                    RandomizeGrid();
                }
            }
            else
            {
                _stagnantFrames = 0;
            }
            _lastPopulation = population;
        }

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.ProcessPixelRows(accessor =>
        {
            for (var gy = 0; gy < _gridHeight; gy++)
            {
                for (var gx = 0; gx < _gridWidth; gx++)
                {
                    if (_grid![gy * _gridWidth + gx])
                    {
                        // Draw cell with color based on position
                        var hue = (gx + gy) * 3f % 360f;
                        var color = HslToRgba(hue, 0.8f, 0.5f);

                        for (var py = gy * CellSize; py < (gy + 1) * CellSize && py < height; py++)
                        {
                            var row = accessor.GetRowSpan(py);
                            for (var px = gx * CellSize; px < (gx + 1) * CellSize && px < width; px++)
                            {
                                row[px] = color;
                            }
                        }
                    }
                }
            }
        });

        return Task.FromResult(image);
    }

    private void RandomizeGrid()
    {
        _grid = new bool[_gridWidth * _gridHeight];
        _nextGrid = new bool[_gridWidth * _gridHeight];

        // Random initial state with ~30% density
        for (var i = 0; i < _grid.Length; i++)
        {
            _grid[i] = _random.NextDouble() < 0.3;
        }

        _generation = 0;
        _stagnantFrames = 0;
    }

    private void UpdateGrid()
    {
        for (var y = 0; y < _gridHeight; y++)
        {
            for (var x = 0; x < _gridWidth; x++)
            {
                var neighbors = CountNeighbors(x, y);
                var index = y * _gridWidth + x;
                var alive = _grid![index];

                // Conway's rules
                if (alive)
                {
                    _nextGrid![index] = neighbors == 2 || neighbors == 3;
                }
                else
                {
                    _nextGrid![index] = neighbors == 3;
                }
            }
        }

        // Swap buffers
        (_grid, _nextGrid) = (_nextGrid, _grid);
    }

    private int CountNeighbors(int x, int y)
    {
        var count = 0;

        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                var nx = (x + dx + _gridWidth) % _gridWidth;
                var ny = (y + dy + _gridHeight) % _gridHeight;

                if (_grid![ny * _gridWidth + nx])
                {
                    count++;
                }
            }
        }

        return count;
    }

    private int CountPopulation()
    {
        var count = 0;
        for (var i = 0; i < _grid!.Length; i++)
        {
            if (_grid[i]) count++;
        }
        return count;
    }

    private static Rgba32 HslToRgba(float h, float s, float l)
    {
        h = ((h % 360f) + 360f) % 360f;

        var c = (1f - MathF.Abs(2f * l - 1f)) * s;
        var x = c * (1f - MathF.Abs((h / 60f) % 2f - 1f));
        var m = l - c / 2f;

        float r, g, b;

        if (h < 60f) { r = c; g = x; b = 0f; }
        else if (h < 120f) { r = x; g = c; b = 0f; }
        else if (h < 180f) { r = 0f; g = c; b = x; }
        else if (h < 240f) { r = 0f; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0f; b = c; }
        else { r = c; g = 0f; b = x; }

        return new Rgba32(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }
}
