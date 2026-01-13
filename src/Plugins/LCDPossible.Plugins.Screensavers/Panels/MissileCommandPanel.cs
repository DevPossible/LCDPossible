using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Missile Command game simulation - defend cities from incoming missiles.
/// </summary>
public sealed class MissileCommandPanel : CanvasPanel
{
    private readonly Random _random = new();
    private readonly List<EnemyMissile> _enemyMissiles = new();
    private readonly List<DefenseMissile> _defenseMissiles = new();
    private readonly List<Explosion> _explosions = new();
    private readonly bool[] _citiesAlive = new bool[6];
    private readonly int[] _batteryAmmo = new int[3];

    private DateTime _lastUpdate;
    private DateTime _lastSpawn;
    private int _width;
    private int _height;
    private int _wave = 1;
    private int _score;
    private float _difficultyMultiplier = 1f;

    private const float EnemyMissileSpeed = 40f;
    private const float DefenseMissileSpeed = 200f;
    private const float ExplosionGrowthRate = 80f;
    private const float MaxExplosionRadius = 40f;
    private const int MaxAmmo = 10;

    public override string PanelId => "missile-command";
    public override string DisplayName => "Missile Command";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public MissileCommandPanel()
    {
        _lastUpdate = DateTime.UtcNow;
        _lastSpawn = DateTime.UtcNow;
        ResetGame();
    }

    private void ResetGame()
    {
        _enemyMissiles.Clear();
        _defenseMissiles.Clear();
        _explosions.Clear();

        for (var i = 0; i < 6; i++)
            _citiesAlive[i] = true;

        for (var i = 0; i < 3; i++)
            _batteryAmmo[i] = MaxAmmo;

        _wave = 1;
        _score = 0;
        _difficultyMultiplier = 1f;
    }

    public override Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public override Task<Image<Rgba32>> RenderFrameAsync(int width, int height, CancellationToken cancellationToken = default)
    {
        if (_width != width || _height != height)
        {
            _width = width;
            _height = height;
        }

        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        Update(deltaTime, now);

        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            // Draw ground
            ctx.Fill(new Rgba32(139, 90, 43), new RectangleF(0, height - 30, width, 30));

            // Draw cities
            DrawCities(ctx);

            // Draw missile batteries
            DrawBatteries(ctx);

            // Draw enemy missiles and their trails
            foreach (var missile in _enemyMissiles)
            {
                DrawEnemyMissile(ctx, missile);
            }

            // Draw defense missiles and their trails
            foreach (var missile in _defenseMissiles)
            {
                DrawDefenseMissile(ctx, missile);
            }

            // Draw explosions
            foreach (var explosion in _explosions)
            {
                DrawExplosion(ctx, explosion);
            }

            // Draw score and wave
            DrawHUD(ctx);
        });

        return Task.FromResult(image);
    }

    private void Update(float deltaTime, DateTime now)
    {
        // Check if game over (all cities destroyed)
        if (_citiesAlive.All(c => !c))
        {
            // Reset after a delay
            if ((now - _lastSpawn).TotalSeconds > 3)
            {
                ResetGame();
            }
            return;
        }

        // Spawn enemy missiles
        var spawnInterval = Math.Max(0.5f, 2f - _difficultyMultiplier * 0.3f);
        if ((now - _lastSpawn).TotalSeconds > spawnInterval)
        {
            _lastSpawn = now;
            SpawnEnemyMissile();
        }

        // Update enemy missiles
        for (var i = _enemyMissiles.Count - 1; i >= 0; i--)
        {
            var missile = _enemyMissiles[i];
            var speed = EnemyMissileSpeed * _difficultyMultiplier;

            missile.X += missile.Dx * speed * deltaTime;
            missile.Y += missile.Dy * speed * deltaTime;

            // Check if hit ground
            if (missile.Y >= _height - 30)
            {
                // Check if hit a city
                CheckCityHit(missile.X);
                _enemyMissiles.RemoveAt(i);
                continue;
            }

            // Check if caught in explosion
            var destroyed = false;
            foreach (var explosion in _explosions.Where(e => e.Growing))
            {
                var dx = missile.X - explosion.X;
                var dy = missile.Y - explosion.Y;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist < explosion.Radius)
                {
                    destroyed = true;
                    _score += 25;
                    break;
                }
            }

            if (destroyed)
            {
                _enemyMissiles.RemoveAt(i);
                continue;
            }

            _enemyMissiles[i] = missile;
        }

        // Update defense missiles
        for (var i = _defenseMissiles.Count - 1; i >= 0; i--)
        {
            var missile = _defenseMissiles[i];

            missile.X += missile.Dx * DefenseMissileSpeed * deltaTime;
            missile.Y += missile.Dy * DefenseMissileSpeed * deltaTime;

            // Check if reached target
            var dx = missile.TargetX - missile.X;
            var dy = missile.TargetY - missile.Y;
            var dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist < 10f)
            {
                // Create explosion at target
                _explosions.Add(new Explosion
                {
                    X = missile.TargetX,
                    Y = missile.TargetY,
                    Radius = 5f,
                    Growing = true,
                    Color = missile.Color
                });
                _defenseMissiles.RemoveAt(i);
                continue;
            }

            _defenseMissiles[i] = missile;
        }

        // Update explosions
        for (var i = _explosions.Count - 1; i >= 0; i--)
        {
            var explosion = _explosions[i];

            if (explosion.Growing)
            {
                explosion.Radius += ExplosionGrowthRate * deltaTime;
                if (explosion.Radius >= MaxExplosionRadius)
                {
                    explosion.Growing = false;
                }
            }
            else
            {
                explosion.Radius -= ExplosionGrowthRate * 0.5f * deltaTime;
                if (explosion.Radius <= 0)
                {
                    _explosions.RemoveAt(i);
                    continue;
                }
            }

            _explosions[i] = explosion;
        }

        // AI: Fire defense missiles at incoming threats
        AIFireDefense();

        // Check for wave completion
        if (_enemyMissiles.Count == 0 && _defenseMissiles.Count == 0 && _explosions.Count == 0)
        {
            _wave++;
            _difficultyMultiplier = 1f + (_wave - 1) * 0.2f;

            // Replenish ammo
            for (var i = 0; i < 3; i++)
                _batteryAmmo[i] = MaxAmmo;

            // Bonus points for surviving cities
            _score += _citiesAlive.Count(c => c) * 100;
        }
    }

    private void SpawnEnemyMissile()
    {
        var startX = _random.NextSingle() * _width;

        // Target a city or battery
        float targetX;
        var targetIndex = _random.Next(9); // 6 cities + 3 batteries

        if (targetIndex < 6)
        {
            if (!_citiesAlive[targetIndex])
            {
                // Find another alive city
                var aliveCities = Enumerable.Range(0, 6).Where(i => _citiesAlive[i]).ToList();
                if (aliveCities.Count == 0) return;
                targetIndex = aliveCities[_random.Next(aliveCities.Count)];
            }
            targetX = GetCityX(targetIndex);
        }
        else
        {
            targetX = GetBatteryX(targetIndex - 6);
        }

        var dx = targetX - startX;
        var dy = _height - 30;
        var length = MathF.Sqrt(dx * dx + dy * dy);

        _enemyMissiles.Add(new EnemyMissile
        {
            X = startX,
            Y = 0,
            StartX = startX,
            StartY = 0,
            Dx = dx / length,
            Dy = dy / length,
            TargetX = targetX
        });
    }

    private void AIFireDefense()
    {
        // Find threatening missiles
        foreach (var enemy in _enemyMissiles)
        {
            // Check if already being targeted
            var alreadyTargeted = _defenseMissiles.Any(d =>
                MathF.Abs(d.TargetX - enemy.X) < 30 && MathF.Abs(d.TargetY - enemy.Y) < 30);

            if (alreadyTargeted) continue;

            // Find best battery to fire from
            var bestBattery = -1;
            var bestDist = float.MaxValue;

            for (var i = 0; i < 3; i++)
            {
                if (_batteryAmmo[i] <= 0) continue;

                var batteryX = GetBatteryX(i);
                var dist = MathF.Abs(batteryX - enemy.X);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestBattery = i;
                }
            }

            if (bestBattery < 0) continue;

            // Predict where to intercept
            var interceptY = enemy.Y + 50 + _random.NextSingle() * 100;
            if (interceptY > _height - 80) interceptY = _height - 80;

            var timeToIntercept = (interceptY - enemy.Y) / (EnemyMissileSpeed * _difficultyMultiplier * enemy.Dy);
            var interceptX = enemy.X + enemy.Dx * EnemyMissileSpeed * _difficultyMultiplier * timeToIntercept;

            FireDefenseMissile(bestBattery, interceptX, interceptY);
            break; // Fire one at a time
        }
    }

    private void FireDefenseMissile(int batteryIndex, float targetX, float targetY)
    {
        if (_batteryAmmo[batteryIndex] <= 0) return;

        _batteryAmmo[batteryIndex]--;

        var startX = GetBatteryX(batteryIndex);
        var startY = _height - 40f;

        var dx = targetX - startX;
        var dy = targetY - startY;
        var length = MathF.Sqrt(dx * dx + dy * dy);

        var color = batteryIndex switch
        {
            0 => new Rgba32(0, 255, 255),   // Cyan
            1 => new Rgba32(255, 255, 0),   // Yellow
            2 => new Rgba32(255, 0, 255),   // Magenta
            _ => new Rgba32(255, 255, 255)
        };

        _defenseMissiles.Add(new DefenseMissile
        {
            X = startX,
            Y = startY,
            StartX = startX,
            StartY = startY,
            Dx = dx / length,
            Dy = dy / length,
            TargetX = targetX,
            TargetY = targetY,
            Color = color
        });
    }

    private void CheckCityHit(float x)
    {
        for (var i = 0; i < 6; i++)
        {
            if (!_citiesAlive[i]) continue;

            var cityX = GetCityX(i);
            if (MathF.Abs(cityX - x) < 30)
            {
                _citiesAlive[i] = false;

                // Create explosion effect
                _explosions.Add(new Explosion
                {
                    X = cityX,
                    Y = _height - 40,
                    Radius = 5f,
                    Growing = true,
                    Color = new Rgba32(255, 100, 0)
                });
                break;
            }
        }
    }

    private float GetCityX(int index)
    {
        // Cities are positioned between batteries
        // Layout: Battery0, City0, City1, Battery1, City2, City3, Battery2, City4, City5
        var spacing = _width / 9f;
        return index switch
        {
            0 => spacing * 1,
            1 => spacing * 2,
            2 => spacing * 4,
            3 => spacing * 5,
            4 => spacing * 7,
            5 => spacing * 8,
            _ => spacing
        };
    }

    private float GetBatteryX(int index)
    {
        var spacing = _width / 9f;
        return index switch
        {
            0 => spacing * 0.5f,
            1 => spacing * 4.5f,
            2 => spacing * 8.5f,
            _ => spacing * 4.5f
        };
    }

    private void DrawCities(IImageProcessingContext ctx)
    {
        for (var i = 0; i < 6; i++)
        {
            var cityX = GetCityX(i);
            var cityY = _height - 30;

            if (_citiesAlive[i])
            {
                // Draw intact city (simple building silhouettes)
                var cityColor = new Rgba32(0, 150, 255);

                // Buildings of varying heights
                ctx.Fill(cityColor, new RectangleF(cityX - 20, cityY - 25, 8, 25));
                ctx.Fill(cityColor, new RectangleF(cityX - 10, cityY - 35, 10, 35));
                ctx.Fill(cityColor, new RectangleF(cityX + 2, cityY - 20, 8, 20));
                ctx.Fill(cityColor, new RectangleF(cityX + 12, cityY - 30, 8, 30));
            }
            else
            {
                // Draw destroyed city (rubble)
                var rubbleColor = new Rgba32(100, 60, 30);
                ctx.Fill(rubbleColor, new RectangleF(cityX - 20, cityY - 8, 40, 8));
            }
        }
    }

    private void DrawBatteries(IImageProcessingContext ctx)
    {
        for (var i = 0; i < 3; i++)
        {
            var batteryX = GetBatteryX(i);
            var batteryY = _height - 30;

            var batteryColor = new Rgba32(0, 200, 0);

            // Draw battery base (triangle/pyramid shape)
            var path = new SixLabors.ImageSharp.Drawing.PathBuilder();
            path.MoveTo(new PointF(batteryX - 25, batteryY));
            path.LineTo(new PointF(batteryX + 25, batteryY));
            path.LineTo(new PointF(batteryX, batteryY - 20));
            path.CloseFigure();
            ctx.Fill(batteryColor, path.Build());

            // Draw ammo indicator
            var ammoColor = _batteryAmmo[i] > 3 ? new Rgba32(0, 255, 0) :
                           _batteryAmmo[i] > 0 ? new Rgba32(255, 255, 0) :
                           new Rgba32(255, 0, 0);

            for (var j = 0; j < _batteryAmmo[i]; j++)
            {
                ctx.Fill(ammoColor, new EllipsePolygon(batteryX - 12 + j * 3, batteryY - 5, 1.5f));
            }
        }
    }

    private void DrawEnemyMissile(IImageProcessingContext ctx, EnemyMissile missile)
    {
        var trailColor = new Rgba32(255, 50, 50);
        var headColor = new Rgba32(255, 255, 255);

        // Draw trail
        ctx.DrawLine(trailColor, 2f,
            new PointF(missile.StartX, missile.StartY),
            new PointF(missile.X, missile.Y));

        // Draw missile head
        ctx.Fill(headColor, new EllipsePolygon(missile.X, missile.Y, 3));
    }

    private void DrawDefenseMissile(IImageProcessingContext ctx, DefenseMissile missile)
    {
        // Draw trail
        ctx.DrawLine(missile.Color, 2f,
            new PointF(missile.StartX, missile.StartY),
            new PointF(missile.X, missile.Y));

        // Draw missile head
        ctx.Fill(new Rgba32(255, 255, 255), new EllipsePolygon(missile.X, missile.Y, 2));

        // Draw target crosshair
        var crossColor = new Rgba32(missile.Color.R, missile.Color.G, missile.Color.B, 128);
        ctx.DrawLine(crossColor, 1f,
            new PointF(missile.TargetX - 5, missile.TargetY),
            new PointF(missile.TargetX + 5, missile.TargetY));
        ctx.DrawLine(crossColor, 1f,
            new PointF(missile.TargetX, missile.TargetY - 5),
            new PointF(missile.TargetX, missile.TargetY + 5));
    }

    private void DrawExplosion(IImageProcessingContext ctx, Explosion explosion)
    {
        // Multi-colored explosion rings
        var alpha = (byte)(explosion.Growing ? 255 : (int)(explosion.Radius / MaxExplosionRadius * 255));

        var outerColor = new Rgba32(explosion.Color.R, explosion.Color.G, explosion.Color.B, alpha);
        var innerColor = new Rgba32(255, 255, 200, alpha);

        ctx.Fill(outerColor, new EllipsePolygon(explosion.X, explosion.Y, explosion.Radius));
        ctx.Fill(innerColor, new EllipsePolygon(explosion.X, explosion.Y, explosion.Radius * 0.6f));
    }

    private void DrawHUD(IImageProcessingContext ctx)
    {
        // Score
        var scoreText = $"SCORE: {_score}";
        var waveText = $"WAVE: {_wave}";

        // Simple score display using rectangles (no fonts needed)
        DrawDigitalText(ctx, scoreText, 10, 10, new Rgba32(255, 255, 0));
        DrawDigitalText(ctx, waveText, _width - 100, 10, new Rgba32(0, 255, 255));
    }

    private void DrawDigitalText(IImageProcessingContext ctx, string text, float x, float y, Rgba32 color)
    {
        // Simple block-based text rendering
        foreach (var c in text)
        {
            if (c >= '0' && c <= '9')
            {
                DrawDigit(ctx, c - '0', x, y, color);
                x += 12;
            }
            else if (c >= 'A' && c <= 'Z')
            {
                DrawLetter(ctx, c, x, y, color);
                x += 12;
            }
            else if (c == ':')
            {
                ctx.Fill(color, new RectangleF(x + 2, y + 3, 3, 3));
                ctx.Fill(color, new RectangleF(x + 2, y + 10, 3, 3));
                x += 8;
            }
            else if (c == ' ')
            {
                x += 8;
            }
        }
    }

    private void DrawDigit(IImageProcessingContext ctx, int digit, float x, float y, Rgba32 color)
    {
        // 7-segment style display
        var segments = digit switch
        {
            0 => new[] { true, true, true, false, true, true, true },
            1 => new[] { false, false, true, false, false, true, false },
            2 => new[] { true, false, true, true, true, false, true },
            3 => new[] { true, false, true, true, false, true, true },
            4 => new[] { false, true, true, true, false, true, false },
            5 => new[] { true, true, false, true, false, true, true },
            6 => new[] { true, true, false, true, true, true, true },
            7 => new[] { true, false, true, false, false, true, false },
            8 => new[] { true, true, true, true, true, true, true },
            9 => new[] { true, true, true, true, false, true, true },
            _ => new[] { false, false, false, false, false, false, false }
        };

        // Top, Upper-left, Upper-right, Middle, Lower-left, Lower-right, Bottom
        if (segments[0]) ctx.Fill(color, new RectangleF(x + 1, y, 8, 2));
        if (segments[1]) ctx.Fill(color, new RectangleF(x, y + 1, 2, 6));
        if (segments[2]) ctx.Fill(color, new RectangleF(x + 8, y + 1, 2, 6));
        if (segments[3]) ctx.Fill(color, new RectangleF(x + 1, y + 7, 8, 2));
        if (segments[4]) ctx.Fill(color, new RectangleF(x, y + 9, 2, 6));
        if (segments[5]) ctx.Fill(color, new RectangleF(x + 8, y + 9, 2, 6));
        if (segments[6]) ctx.Fill(color, new RectangleF(x + 1, y + 14, 8, 2));
    }

    private void DrawLetter(IImageProcessingContext ctx, char letter, float x, float y, Rgba32 color)
    {
        // Simple block letters for common characters
        switch (letter)
        {
            case 'S':
                ctx.Fill(color, new RectangleF(x + 1, y, 8, 2));
                ctx.Fill(color, new RectangleF(x, y + 1, 2, 6));
                ctx.Fill(color, new RectangleF(x + 1, y + 7, 8, 2));
                ctx.Fill(color, new RectangleF(x + 8, y + 9, 2, 6));
                ctx.Fill(color, new RectangleF(x + 1, y + 14, 8, 2));
                break;
            case 'C':
                ctx.Fill(color, new RectangleF(x + 1, y, 8, 2));
                ctx.Fill(color, new RectangleF(x, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 1, y + 14, 8, 2));
                break;
            case 'O':
                ctx.Fill(color, new RectangleF(x + 1, y, 8, 2));
                ctx.Fill(color, new RectangleF(x, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 8, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 1, y + 14, 8, 2));
                break;
            case 'R':
                ctx.Fill(color, new RectangleF(x + 1, y, 7, 2));
                ctx.Fill(color, new RectangleF(x, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 8, y + 1, 2, 6));
                ctx.Fill(color, new RectangleF(x + 1, y + 7, 8, 2));
                ctx.Fill(color, new RectangleF(x + 5, y + 9, 2, 6));
                break;
            case 'E':
                ctx.Fill(color, new RectangleF(x + 1, y, 8, 2));
                ctx.Fill(color, new RectangleF(x, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 1, y + 7, 6, 2));
                ctx.Fill(color, new RectangleF(x + 1, y + 14, 8, 2));
                break;
            case 'W':
                ctx.Fill(color, new RectangleF(x, y, 2, 14));
                ctx.Fill(color, new RectangleF(x + 8, y, 2, 14));
                ctx.Fill(color, new RectangleF(x + 4, y + 6, 2, 8));
                ctx.Fill(color, new RectangleF(x + 1, y + 14, 8, 2));
                break;
            case 'A':
                ctx.Fill(color, new RectangleF(x + 1, y, 8, 2));
                ctx.Fill(color, new RectangleF(x, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 8, y + 1, 2, 14));
                ctx.Fill(color, new RectangleF(x + 1, y + 7, 8, 2));
                break;
            case 'V':
                ctx.Fill(color, new RectangleF(x, y, 2, 10));
                ctx.Fill(color, new RectangleF(x + 8, y, 2, 10));
                ctx.Fill(color, new RectangleF(x + 2, y + 10, 2, 4));
                ctx.Fill(color, new RectangleF(x + 6, y + 10, 2, 4));
                ctx.Fill(color, new RectangleF(x + 4, y + 14, 2, 2));
                break;
        }
    }

    private struct EnemyMissile
    {
        public float X, Y, StartX, StartY, Dx, Dy, TargetX;
    }

    private struct DefenseMissile
    {
        public float X, Y, StartX, StartY, Dx, Dy, TargetX, TargetY;
        public Rgba32 Color;
    }

    private struct Explosion
    {
        public float X, Y, Radius;
        public bool Growing;
        public Rgba32 Color;
    }
}
