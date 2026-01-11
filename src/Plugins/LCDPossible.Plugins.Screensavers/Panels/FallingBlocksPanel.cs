using LCDPossible.Sdk;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Tetris-style falling blocks screensaver with AI-controlled gameplay.
/// Supports 1-3 simultaneous players with separate boards.
/// </summary>
public sealed class FallingBlocksPanel : BaseLivePanel
{
    public override string PanelId => "falling-blocks";
    public override string DisplayName => "Falling Blocks";
    public override bool IsLive => true;
    public override bool IsAnimated => true;

    // Grid dimensions
    private const int GridWidth = 10;
    private const int GridHeight = 20;

    // Timing
    private DateTime _lastUpdate = DateTime.UtcNow;
    private const float LineClearDuration = 0.3f;
    private const float MoveInterval = 0.05f;

    // Players
    private readonly List<PlayerState> _players = new();
    private int _playerCount;
    private int? _requestedPlayerCount;
    private readonly Random _random = new();

    /// <summary>
    /// Sets the number of players (1-3). If not set, a random count is used.
    /// </summary>
    public void SetPlayerCount(int count)
    {
        _requestedPlayerCount = Math.Clamp(count, 1, 3);
    }

    // Fonts
    private Font? _labelFont;
    private Font? _valueFont;
    private Font? _titleFont;

    // Player colors for visual distinction
    private static readonly Rgba32[] PlayerColors =
    [
        new Rgba32(100, 150, 255),  // Player 1 - Blue
        new Rgba32(255, 150, 100),  // Player 2 - Orange
        new Rgba32(150, 255, 100)   // Player 3 - Green
    ];

    // Tetromino definitions (each has 4 rotations)
    private static readonly int[][,,] Tetrominoes =
    [
        // I piece (cyan)
        new int[4, 4, 4]
        {
            { { 0, 0, 0, 0 }, { 1, 1, 1, 1 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 1, 0 }, { 0, 0, 1, 0 }, { 0, 0, 1, 0 }, { 0, 0, 1, 0 } },
            { { 0, 0, 0, 0 }, { 0, 0, 0, 0 }, { 1, 1, 1, 1 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 1, 0, 0 } }
        },
        // O piece (yellow)
        new int[4, 4, 4]
        {
            { { 0, 1, 1, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 1, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 1, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 1, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } }
        },
        // T piece (purple)
        new int[4, 4, 4]
        {
            { { 0, 1, 0, 0 }, { 1, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 0, 1, 1, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 0, 0 }, { 1, 1, 1, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 1, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } }
        },
        // S piece (green)
        new int[4, 4, 4]
        {
            { { 0, 1, 1, 0 }, { 1, 1, 0, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 0, 1, 1, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 0, 0 }, { 0, 1, 1, 0 }, { 1, 1, 0, 0 }, { 0, 0, 0, 0 } },
            { { 1, 0, 0, 0 }, { 1, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } }
        },
        // Z piece (red)
        new int[4, 4, 4]
        {
            { { 1, 1, 0, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 1, 0 }, { 0, 1, 1, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 0, 0 }, { 1, 1, 0, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 1, 1, 0, 0 }, { 1, 0, 0, 0 }, { 0, 0, 0, 0 } }
        },
        // J piece (blue)
        new int[4, 4, 4]
        {
            { { 1, 0, 0, 0 }, { 1, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 1, 0 }, { 0, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 0, 0 }, { 1, 1, 1, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 0, 1, 0, 0 }, { 1, 1, 0, 0 }, { 0, 0, 0, 0 } }
        },
        // L piece (orange)
        new int[4, 4, 4]
        {
            { { 0, 0, 1, 0 }, { 1, 1, 1, 0 }, { 0, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 0, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 1, 1, 0 }, { 0, 0, 0, 0 } },
            { { 0, 0, 0, 0 }, { 1, 1, 1, 0 }, { 1, 0, 0, 0 }, { 0, 0, 0, 0 } },
            { { 1, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 0, 0 } }
        }
    ];

    // Block colors (matching classic Tetris)
    private static readonly Rgba32[] BlockColors =
    [
        new Rgba32(0, 0, 0),       // 0 - empty (not used)
        new Rgba32(0, 240, 240),   // 1 - I (cyan)
        new Rgba32(240, 240, 0),   // 2 - O (yellow)
        new Rgba32(160, 0, 240),   // 3 - T (purple)
        new Rgba32(0, 240, 0),     // 4 - S (green)
        new Rgba32(240, 0, 0),     // 5 - Z (red)
        new Rgba32(0, 0, 240),     // 6 - J (blue)
        new Rgba32(240, 160, 0)    // 7 - L (orange)
    ];

    private record Tetromino(int Type, int[,,] Shape);

    private class PlayerState
    {
        public int PlayerNumber { get; init; }
        public int[,] Grid { get; set; } = new int[GridWidth, GridHeight];
        public Tetromino? CurrentPiece { get; set; }
        public int CurrentX { get; set; }
        public int CurrentY { get; set; }
        public int CurrentRotation { get; set; }
        public int NextPieceType { get; set; }
        public int TargetX { get; set; }
        public int TargetRotation { get; set; }
        public bool HasTarget { get; set; }
        public float DropTimer { get; set; }
        public float DropInterval { get; set; } = 0.5f;
        public float MoveTimer { get; set; }
        public float LineClearTimer { get; set; }
        public List<int> ClearingLines { get; } = new();
        public bool IsClearing { get; set; }
        public int Score { get; set; }
        public int HighScore { get; set; }
        public int LinesCleared { get; set; }
        public int Level { get; set; } = 1;
        public bool IsGameOver { get; set; }
        public float GameOverTimer { get; set; }
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        InitializeFonts();

        // Use requested player count or randomly choose 1-3 players
        _playerCount = _requestedPlayerCount ?? _random.Next(1, 4);
        InitializePlayers();

        return Task.CompletedTask;
    }

    private void InitializeFonts()
    {
        try
        {
            var families = SystemFonts.Families.ToArray();
            var family = families.FirstOrDefault(f =>
                f.Name.Contains("Arial", StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains("Segoe", StringComparison.OrdinalIgnoreCase));

            if (family.Name != null)
            {
                _labelFont = family.CreateFont(10, FontStyle.Regular);
                _valueFont = family.CreateFont(12, FontStyle.Bold);
                _titleFont = family.CreateFont(14, FontStyle.Bold);
            }
            else if (families.Length > 0)
            {
                _labelFont = families[0].CreateFont(10, FontStyle.Regular);
                _valueFont = families[0].CreateFont(12, FontStyle.Bold);
                _titleFont = families[0].CreateFont(14, FontStyle.Bold);
            }
        }
        catch
        {
            // Font loading failed
        }
    }

    private void InitializePlayers()
    {
        _players.Clear();
        for (var i = 0; i < _playerCount; i++)
        {
            var player = new PlayerState
            {
                PlayerNumber = i + 1,
                NextPieceType = _random.Next(7)
            };
            _players.Add(player);
            ResetPlayer(player);
        }
    }

    private void ResetPlayer(PlayerState player)
    {
        if (player.Score > player.HighScore)
            player.HighScore = player.Score;

        player.Grid = new int[GridWidth, GridHeight];
        player.Score = 0;
        player.LinesCleared = 0;
        player.Level = 1;
        player.DropInterval = 0.5f;
        player.ClearingLines.Clear();
        player.IsClearing = false;
        player.IsGameOver = false;
        player.GameOverTimer = 0;
        player.NextPieceType = _random.Next(7);
        SpawnPiece(player);
    }

    private void SpawnPiece(PlayerState player)
    {
        var type = player.NextPieceType;
        player.NextPieceType = _random.Next(7);

        player.CurrentPiece = new Tetromino(type, Tetrominoes[type]);
        player.CurrentX = GridWidth / 2 - 2;
        player.CurrentY = 0;
        player.CurrentRotation = 0;
        player.HasTarget = false;

        if (!IsValidPosition(player, player.CurrentX, player.CurrentY, player.CurrentRotation))
        {
            player.IsGameOver = true;
            player.GameOverTimer = 0;
            return;
        }

        CalculateAITarget(player);
    }

    private void CalculateAITarget(PlayerState player)
    {
        if (player.CurrentPiece == null) return;

        var bestScore = int.MinValue;
        var bestX = player.CurrentX;
        var bestRotation = 0;

        for (var rotation = 0; rotation < 4; rotation++)
        {
            for (var x = -2; x < GridWidth + 2; x++)
            {
                if (!IsValidPosition(player, x, player.CurrentY, rotation)) continue;

                var landY = player.CurrentY;
                while (IsValidPosition(player, x, landY + 1, rotation))
                    landY++;

                var score = EvaluatePosition(player, x, landY, rotation);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestRotation = rotation;
                }
            }
        }

        player.TargetX = bestX;
        player.TargetRotation = bestRotation;
        player.HasTarget = true;
    }

    private int EvaluatePosition(PlayerState player, int x, int y, int rotation)
    {
        if (player.CurrentPiece == null) return int.MinValue;

        var testGrid = (int[,])player.Grid.Clone();
        PlacePieceOnGrid(testGrid, player.CurrentPiece, x, y, rotation);

        var score = 0;

        // Count complete lines
        var completeLines = 0;
        for (var row = 0; row < GridHeight; row++)
        {
            var complete = true;
            for (var col = 0; col < GridWidth; col++)
            {
                if (testGrid[col, row] == 0)
                {
                    complete = false;
                    break;
                }
            }
            if (complete) completeLines++;
        }

        // Tetris bonus
        if (completeLines == 4)
            score += 10000;
        else
            score += completeLines * 1000;

        // Prefer lower placements
        score += y * 10;

        // Penalize holes
        var holes = 0;
        for (var col = 0; col < GridWidth; col++)
        {
            var foundBlock = false;
            for (var row = 0; row < GridHeight; row++)
            {
                if (testGrid[col, row] != 0)
                    foundBlock = true;
                else if (foundBlock)
                    holes++;
            }
        }
        score -= holes * 500;

        // Penalize bumpiness
        var heights = new int[GridWidth];
        for (var col = 0; col < GridWidth; col++)
        {
            for (var row = 0; row < GridHeight; row++)
            {
                if (testGrid[col, row] != 0)
                {
                    heights[col] = GridHeight - row;
                    break;
                }
            }
        }

        var bumpiness = 0;
        for (var col = 0; col < GridWidth - 1; col++)
            bumpiness += Math.Abs(heights[col] - heights[col + 1]);
        score -= bumpiness * 50;

        // Penalize height
        var maxHeight = heights.Max();
        score -= maxHeight * 30;

        // Bonus for keeping right column clear (for Tetrises)
        if (heights[GridWidth - 1] == 0)
            score += 200;

        return score;
    }

    private static void PlacePieceOnGrid(int[,] grid, Tetromino piece, int pieceX, int pieceY, int rotation)
    {
        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                if (piece.Shape[rotation, py, px] != 0)
                {
                    var gx = pieceX + px;
                    var gy = pieceY + py;
                    if (gx >= 0 && gx < GridWidth && gy >= 0 && gy < GridHeight)
                        grid[gx, gy] = piece.Type + 1;
                }
            }
        }
    }

    private static bool IsValidPosition(PlayerState player, int x, int y, int rotation)
    {
        if (player.CurrentPiece == null) return false;

        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                if (player.CurrentPiece.Shape[rotation, py, px] != 0)
                {
                    var gx = x + px;
                    var gy = y + py;

                    if (gx < 0 || gx >= GridWidth || gy < 0 || gy >= GridHeight)
                        return false;

                    if (player.Grid[gx, gy] != 0)
                        return false;
                }
            }
        }
        return true;
    }

    private void LockPiece(PlayerState player)
    {
        if (player.CurrentPiece == null) return;

        PlacePieceOnGrid(player.Grid, player.CurrentPiece, player.CurrentX, player.CurrentY, player.CurrentRotation);

        // Check for complete lines
        player.ClearingLines.Clear();
        for (var row = 0; row < GridHeight; row++)
        {
            var complete = true;
            for (var col = 0; col < GridWidth; col++)
            {
                if (player.Grid[col, row] == 0)
                {
                    complete = false;
                    break;
                }
            }
            if (complete)
                player.ClearingLines.Add(row);
        }

        if (player.ClearingLines.Count > 0)
        {
            player.IsClearing = true;
            player.LineClearTimer = 0;
            player.LinesCleared += player.ClearingLines.Count;

            var lineScore = player.ClearingLines.Count switch
            {
                1 => 100,
                2 => 300,
                3 => 500,
                4 => 800,
                _ => 0
            };
            player.Score += lineScore * player.Level;

            player.Level = 1 + player.LinesCleared / 10;
            player.DropInterval = Math.Max(0.1f, 0.5f - (player.Level - 1) * 0.05f);

            if (player.Score > player.HighScore)
                player.HighScore = player.Score;
        }
        else
        {
            SpawnPiece(player);
        }
    }

    private void ClearLines(PlayerState player)
    {
        foreach (var row in player.ClearingLines.OrderByDescending(r => r))
        {
            for (var y = row; y > 0; y--)
            {
                for (var x = 0; x < GridWidth; x++)
                {
                    player.Grid[x, y] = player.Grid[x, y - 1];
                }
            }
            for (var x = 0; x < GridWidth; x++)
                player.Grid[x, 0] = 0;
        }

        player.ClearingLines.Clear();
        player.IsClearing = false;
        SpawnPiece(player);
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        // Update all players
        foreach (var player in _players)
        {
            UpdatePlayer(player, deltaTime);
        }

        // Render
        var image = new Image<Rgba32>(width, height, new Rgba32(20, 20, 30));

        image.Mutate(ctx =>
        {
            // Calculate layout based on player count
            var spacing = 10;
            var totalWidth = width - spacing * (_playerCount + 1);
            var boardWidth = totalWidth / _playerCount;

            // Calculate cell size to fit boards
            var maxCellWidth = (boardWidth - 80) / GridWidth; // Leave room for stats
            var maxCellHeight = (height - 60) / GridHeight;
            var cellSize = Math.Min(maxCellWidth, maxCellHeight);
            cellSize = Math.Max(8, Math.Min(cellSize, 20)); // Clamp between 8-20

            var gridPixelWidth = cellSize * GridWidth;
            var gridPixelHeight = cellSize * GridHeight;

            for (var i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                var boardStartX = spacing + i * (boardWidth + spacing);
                var offsetX = boardStartX + (boardWidth - gridPixelWidth - 70) / 2;
                var offsetY = (height - gridPixelHeight) / 2;

                // Draw player header
                if (_titleFont != null && _playerCount > 1)
                {
                    var playerColor = PlayerColors[i];
                    ctx.DrawText($"P{player.PlayerNumber}", _titleFont, playerColor,
                        new PointF(offsetX + gridPixelWidth / 2 - 10, offsetY - 22));
                }

                // Draw the game board
                DrawPlayerBoard(ctx, player, offsetX, offsetY, cellSize, gridPixelWidth, gridPixelHeight, i);
            }
        });

        return Task.FromResult(image);
    }

    private void UpdatePlayer(PlayerState player, float deltaTime)
    {
        // Handle game over
        if (player.IsGameOver)
        {
            player.GameOverTimer += deltaTime;
            if (player.GameOverTimer >= 3.0f)
            {
                ResetPlayer(player);
            }
            return;
        }

        // Handle line clearing
        if (player.IsClearing)
        {
            player.LineClearTimer += deltaTime;
            if (player.LineClearTimer >= LineClearDuration)
                ClearLines(player);
            return;
        }

        if (player.CurrentPiece == null) return;

        // AI movement
        if (player.HasTarget)
        {
            player.MoveTimer += deltaTime;
            if (player.MoveTimer >= MoveInterval)
            {
                player.MoveTimer = 0;

                if (player.CurrentRotation != player.TargetRotation)
                {
                    var newRotation = (player.CurrentRotation + 1) % 4;
                    if (IsValidPosition(player, player.CurrentX, player.CurrentY, newRotation))
                        player.CurrentRotation = newRotation;
                }
                else if (player.CurrentX < player.TargetX)
                {
                    if (IsValidPosition(player, player.CurrentX + 1, player.CurrentY, player.CurrentRotation))
                        player.CurrentX++;
                }
                else if (player.CurrentX > player.TargetX)
                {
                    if (IsValidPosition(player, player.CurrentX - 1, player.CurrentY, player.CurrentRotation))
                        player.CurrentX--;
                }
            }
        }

        // Drop piece
        player.DropTimer += deltaTime;

        var effectiveInterval = (player.CurrentX == player.TargetX && player.CurrentRotation == player.TargetRotation)
            ? player.DropInterval * 0.2f
            : player.DropInterval;

        if (player.DropTimer >= effectiveInterval)
        {
            player.DropTimer = 0;

            if (IsValidPosition(player, player.CurrentX, player.CurrentY + 1, player.CurrentRotation))
            {
                player.CurrentY++;
            }
            else
            {
                LockPiece(player);
            }
        }
    }

    private void DrawPlayerBoard(IImageProcessingContext ctx, PlayerState player,
        int offsetX, int offsetY, int cellSize, int gridPixelWidth, int gridPixelHeight, int playerIndex)
    {
        var playerColor = PlayerColors[playerIndex];

        // Draw background grid
        DrawGrid(ctx, offsetX, offsetY, cellSize, gridPixelWidth, gridPixelHeight);

        // Draw placed blocks
        DrawPlacedBlocks(ctx, player, offsetX, offsetY, cellSize);

        // Draw ghost piece
        if (player.CurrentPiece != null && !player.IsClearing && !player.IsGameOver)
            DrawGhostPiece(ctx, player, offsetX, offsetY, cellSize);

        // Draw current piece
        if (player.CurrentPiece != null && !player.IsClearing && !player.IsGameOver)
            DrawCurrentPiece(ctx, player, offsetX, offsetY, cellSize);

        // Draw clearing animation
        if (player.IsClearing)
            DrawClearingAnimation(ctx, player, offsetX, offsetY, cellSize);

        // Draw game over overlay
        if (player.IsGameOver)
            DrawGameOver(ctx, offsetX, offsetY, gridPixelWidth, gridPixelHeight);

        // Draw border with player color
        ctx.Draw(playerColor, 2,
            new RectangleF(offsetX - 2, offsetY - 2, gridPixelWidth + 4, gridPixelHeight + 4));

        // Draw stats panel
        DrawStats(ctx, player, offsetX + gridPixelWidth + 5, offsetY, cellSize, playerColor);
    }

    private static void DrawGrid(IImageProcessingContext ctx, int offsetX, int offsetY,
        int cellSize, int gridPixelWidth, int gridPixelHeight)
    {
        var gridColor = new Rgba32(40, 40, 50);

        for (var x = 0; x <= GridWidth; x++)
        {
            ctx.DrawLine(gridColor, 1,
                new PointF(offsetX + x * cellSize, offsetY),
                new PointF(offsetX + x * cellSize, offsetY + gridPixelHeight));
        }

        for (var y = 0; y <= GridHeight; y++)
        {
            ctx.DrawLine(gridColor, 1,
                new PointF(offsetX, offsetY + y * cellSize),
                new PointF(offsetX + gridPixelWidth, offsetY + y * cellSize));
        }
    }

    private static void DrawPlacedBlocks(IImageProcessingContext ctx, PlayerState player,
        int offsetX, int offsetY, int cellSize)
    {
        for (var y = 0; y < GridHeight; y++)
        {
            for (var x = 0; x < GridWidth; x++)
            {
                if (player.Grid[x, y] != 0)
                {
                    DrawBlock(ctx, offsetX + x * cellSize, offsetY + y * cellSize,
                        cellSize, BlockColors[player.Grid[x, y]]);
                }
            }
        }
    }

    private static void DrawCurrentPiece(IImageProcessingContext ctx, PlayerState player,
        int offsetX, int offsetY, int cellSize)
    {
        if (player.CurrentPiece == null) return;

        var color = BlockColors[player.CurrentPiece.Type + 1];

        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                if (player.CurrentPiece.Shape[player.CurrentRotation, py, px] != 0)
                {
                    var screenX = offsetX + (player.CurrentX + px) * cellSize;
                    var screenY = offsetY + (player.CurrentY + py) * cellSize;
                    DrawBlock(ctx, screenX, screenY, cellSize, color);
                }
            }
        }
    }

    private static void DrawGhostPiece(IImageProcessingContext ctx, PlayerState player,
        int offsetX, int offsetY, int cellSize)
    {
        if (player.CurrentPiece == null) return;

        var ghostY = player.CurrentY;
        while (IsValidPosition(player, player.CurrentX, ghostY + 1, player.CurrentRotation))
            ghostY++;

        if (ghostY == player.CurrentY) return;

        var color = BlockColors[player.CurrentPiece.Type + 1];
        var ghostColor = new Rgba32(color.R, color.G, color.B, 60);

        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                if (player.CurrentPiece.Shape[player.CurrentRotation, py, px] != 0)
                {
                    var screenX = offsetX + (player.CurrentX + px) * cellSize;
                    var screenY = offsetY + (ghostY + py) * cellSize;
                    ctx.Fill(ghostColor, new RectangleF(screenX + 1, screenY + 1, cellSize - 2, cellSize - 2));
                }
            }
        }
    }

    private static void DrawClearingAnimation(IImageProcessingContext ctx, PlayerState player,
        int offsetX, int offsetY, int cellSize)
    {
        var flash = (player.LineClearTimer * 10) % 1 > 0.5f;

        foreach (var row in player.ClearingLines)
        {
            var color = flash ? new Rgba32(255, 255, 255) : new Rgba32(200, 200, 100);
            ctx.Fill(color, new RectangleF(
                offsetX, offsetY + row * cellSize,
                GridWidth * cellSize, cellSize));
        }
    }

    private void DrawGameOver(IImageProcessingContext ctx, int offsetX, int offsetY,
        int gridPixelWidth, int gridPixelHeight)
    {
        // Semi-transparent overlay
        ctx.Fill(new Rgba32(0, 0, 0, 150),
            new RectangleF(offsetX, offsetY, gridPixelWidth, gridPixelHeight));

        // Game over text
        if (_titleFont != null)
        {
            var textX = offsetX + gridPixelWidth / 2 - 30;
            var textY = offsetY + gridPixelHeight / 2 - 10;
            ctx.DrawText("GAME", _titleFont, new Rgba32(255, 50, 50), new PointF(textX, textY));
            ctx.DrawText("OVER", _titleFont, new Rgba32(255, 50, 50), new PointF(textX, textY + 18));
        }
    }

    private static void DrawBlock(IImageProcessingContext ctx, int x, int y, int size, Rgba32 color)
    {
        ctx.Fill(color, new RectangleF(x + 1, y + 1, size - 2, size - 2));

        var highlight = new Rgba32(
            (byte)Math.Min(255, color.R + 60),
            (byte)Math.Min(255, color.G + 60),
            (byte)Math.Min(255, color.B + 60));
        ctx.Fill(highlight, new RectangleF(x + 1, y + 1, size - 2, 2));
        ctx.Fill(highlight, new RectangleF(x + 1, y + 1, 2, size - 2));

        var shadow = new Rgba32(
            (byte)(color.R * 0.5f),
            (byte)(color.G * 0.5f),
            (byte)(color.B * 0.5f));
        ctx.Fill(shadow, new RectangleF(x + 1, y + size - 3, size - 2, 2));
        ctx.Fill(shadow, new RectangleF(x + size - 3, y + 1, 2, size - 2));
    }

    private void DrawStats(IImageProcessingContext ctx, PlayerState player,
        int panelX, int offsetY, int cellSize, Rgba32 playerColor)
    {
        if (_labelFont == null || _valueFont == null)
            return;

        var labelColor = new Rgba32(150, 150, 170);
        var valueColor = new Rgba32(255, 255, 255);
        var currentY = offsetY;

        // Score
        ctx.DrawText("SCORE", _labelFont, labelColor, new PointF(panelX, currentY));
        currentY += 12;
        ctx.DrawText(player.Score.ToString("N0"), _valueFont, valueColor, new PointF(panelX, currentY));
        currentY += 18;

        // High Score
        ctx.DrawText("HIGH", _labelFont, labelColor, new PointF(panelX, currentY));
        currentY += 12;
        ctx.DrawText(player.HighScore.ToString("N0"), _valueFont, playerColor, new PointF(panelX, currentY));
        currentY += 18;

        // Level
        ctx.DrawText("LVL", _labelFont, labelColor, new PointF(panelX, currentY));
        currentY += 12;
        ctx.DrawText(player.Level.ToString(), _valueFont, new Rgba32(100, 255, 100), new PointF(panelX, currentY));
        currentY += 18;

        // Lines
        ctx.DrawText("LINES", _labelFont, labelColor, new PointF(panelX, currentY));
        currentY += 12;
        ctx.DrawText(player.LinesCleared.ToString(), _valueFont, new Rgba32(100, 200, 255), new PointF(panelX, currentY));
        currentY += 22;

        // Next piece preview
        ctx.DrawText("NEXT", _labelFont, labelColor, new PointF(panelX, currentY));
        currentY += 14;

        var previewSize = Math.Max(8, Math.Min(cellSize, 12));
        var previewBoxSize = previewSize * 4 + 4;
        ctx.Draw(new Rgba32(60, 60, 80), 1,
            new RectangleF(panelX, currentY, previewBoxSize, previewBoxSize));
        ctx.Fill(new Rgba32(30, 30, 40),
            new RectangleF(panelX + 1, currentY + 1, previewBoxSize - 2, previewBoxSize - 2));

        DrawNextPiece(ctx, panelX + 2, (int)currentY + 2, previewSize, player.NextPieceType);
    }

    private static void DrawNextPiece(IImageProcessingContext ctx, int x, int y, int cellSize, int pieceType)
    {
        var shape = Tetrominoes[pieceType];
        var color = BlockColors[pieceType + 1];

        var minX = 4;
        var maxX = 0;
        var minY = 4;
        var maxY = 0;

        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                if (shape[0, py, px] != 0)
                {
                    minX = Math.Min(minX, px);
                    maxX = Math.Max(maxX, px);
                    minY = Math.Min(minY, py);
                    maxY = Math.Max(maxY, py);
                }
            }
        }

        var pieceWidth = maxX - minX + 1;
        var pieceHeight = maxY - minY + 1;
        var offsetPx = (4 - pieceWidth) * cellSize / 2;
        var offsetPy = (4 - pieceHeight) * cellSize / 2;

        for (var py = 0; py < 4; py++)
        {
            for (var px = 0; px < 4; px++)
            {
                if (shape[0, py, px] != 0)
                {
                    var blockX = x + (px - minX) * cellSize + offsetPx;
                    var blockY = y + (py - minY) * cellSize + offsetPy;
                    DrawBlock(ctx, blockX, blockY, cellSize, color);
                }
            }
        }
    }
}
