using LCDPossible.Sdk;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using LCDPossible.Core.Rendering;
namespace LCDPossible.Plugins.Screensavers.Panels;

/// <summary>
/// Asteroids game simulation with vector-style graphics.
/// </summary>
public sealed class AsteroidsPanel : CanvasPanel
{
    private const int AsteroidCount = 8;
    private const float ShipSpeed = 150f;
    private const float BulletSpeed = 400f;

    private readonly Random _random;
    private readonly List<Asteroid> _asteroids = new();
    private readonly List<Bullet> _bullets = new();
    private Ship _ship;
    private DateTime _lastUpdate;
    private DateTime _lastShot;
    private int _width;
    private int _height;

    public override string PanelId => "asteroids";
    public override string DisplayName => "Asteroids";
    public override PanelRenderMode RenderMode => PanelRenderMode.Stream;

    public AsteroidsPanel()
    {
        _random = new Random();
        _lastUpdate = DateTime.UtcNow;
        _lastShot = DateTime.UtcNow;
        _ship = new Ship();
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
            Initialize();
        }

        var now = DateTime.UtcNow;
        var deltaTime = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        Update(deltaTime, now);

        var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));

        image.Mutate(ctx =>
        {
            // Draw asteroids
            foreach (var asteroid in _asteroids)
            {
                DrawAsteroid(ctx, asteroid);
            }

            // Draw bullets
            foreach (var bullet in _bullets)
            {
                ctx.Fill(new Rgba32(255, 255, 100),
                    new SixLabors.ImageSharp.Drawing.EllipsePolygon(bullet.X, bullet.Y, 2));
            }

            // Draw ship
            DrawShip(ctx, _ship);
        });

        return Task.FromResult(image);
    }

    private void Initialize()
    {
        _ship = new Ship
        {
            X = _width / 2f,
            Y = _height / 2f,
            Angle = -MathF.PI / 2f,
            RotationSpeed = 2f
        };

        _asteroids.Clear();
        for (var i = 0; i < AsteroidCount; i++)
        {
            _asteroids.Add(CreateAsteroid(null));
        }
    }

    private Asteroid CreateAsteroid(Asteroid? parent)
    {
        float x, y, size;

        if (parent.HasValue)
        {
            var p = parent.Value;
            x = p.X + _random.NextSingle() * 20 - 10;
            y = p.Y + _random.NextSingle() * 20 - 10;
            size = p.Size / 2f;
        }
        else
        {
            // Spawn at edge
            if (_random.NextDouble() < 0.5)
            {
                x = _random.NextDouble() < 0.5 ? 0 : _width;
                y = _random.NextSingle() * _height;
            }
            else
            {
                x = _random.NextSingle() * _width;
                y = _random.NextDouble() < 0.5 ? 0 : _height;
            }
            size = 30 + _random.NextSingle() * 30;
        }

        var angle = _random.NextSingle() * MathF.PI * 2;
        var speed = 30 + _random.NextSingle() * 50;

        // Generate asteroid shape (irregular polygon)
        var vertices = new float[8];
        for (var i = 0; i < 8; i++)
        {
            vertices[i] = 0.6f + _random.NextSingle() * 0.4f;
        }

        return new Asteroid
        {
            X = x,
            Y = y,
            Vx = MathF.Cos(angle) * speed,
            Vy = MathF.Sin(angle) * speed,
            Size = size,
            Rotation = _random.NextSingle() * MathF.PI * 2,
            RotationSpeed = (_random.NextSingle() - 0.5f) * 2f,
            Vertices = vertices
        };
    }

    private void Update(float deltaTime, DateTime now)
    {
        // AI: Rotate towards nearest asteroid and move
        if (_asteroids.Count > 0)
        {
            var nearestAsteroid = _asteroids.OrderBy(a =>
                MathF.Sqrt(MathF.Pow(a.X - _ship.X, 2) + MathF.Pow(a.Y - _ship.Y, 2))).First();

            var targetAngle = MathF.Atan2(nearestAsteroid.Y - _ship.Y, nearestAsteroid.X - _ship.X);
            var angleDiff = NormalizeAngle(targetAngle - _ship.Angle);

            if (MathF.Abs(angleDiff) > 0.1f)
            {
                _ship.Angle += MathF.Sign(angleDiff) * _ship.RotationSpeed * deltaTime;
            }
        }

        // Move ship forward
        _ship.X += MathF.Cos(_ship.Angle) * ShipSpeed * deltaTime;
        _ship.Y += MathF.Sin(_ship.Angle) * ShipSpeed * deltaTime;

        // Wrap ship
        _ship.X = (_ship.X + _width) % _width;
        _ship.Y = (_ship.Y + _height) % _height;

        // Shoot periodically
        if ((now - _lastShot).TotalSeconds > 0.3)
        {
            _lastShot = now;
            _bullets.Add(new Bullet
            {
                X = _ship.X + MathF.Cos(_ship.Angle) * 15,
                Y = _ship.Y + MathF.Sin(_ship.Angle) * 15,
                Vx = MathF.Cos(_ship.Angle) * BulletSpeed,
                Vy = MathF.Sin(_ship.Angle) * BulletSpeed,
                Life = 1.5f
            });
        }

        // Update bullets
        for (var i = _bullets.Count - 1; i >= 0; i--)
        {
            var bullet = _bullets[i];
            bullet.X += bullet.Vx * deltaTime;
            bullet.Y += bullet.Vy * deltaTime;
            bullet.Life -= deltaTime;

            if (bullet.Life <= 0 || bullet.X < -10 || bullet.X > _width + 10 ||
                bullet.Y < -10 || bullet.Y > _height + 10)
            {
                _bullets.RemoveAt(i);
                continue;
            }

            _bullets[i] = bullet;

            // Check collision with asteroids
            for (var j = _asteroids.Count - 1; j >= 0; j--)
            {
                var asteroid = _asteroids[j];
                var dist = MathF.Sqrt(MathF.Pow(bullet.X - asteroid.X, 2) +
                                      MathF.Pow(bullet.Y - asteroid.Y, 2));
                if (dist < asteroid.Size)
                {
                    _bullets.RemoveAt(i);

                    if (asteroid.Size > 15)
                    {
                        // Split asteroid
                        _asteroids.Add(CreateAsteroid(asteroid));
                        _asteroids.Add(CreateAsteroid(asteroid));
                    }
                    _asteroids.RemoveAt(j);
                    break;
                }
            }
        }

        // Update asteroids
        for (var i = 0; i < _asteroids.Count; i++)
        {
            var asteroid = _asteroids[i];
            asteroid.X += asteroid.Vx * deltaTime;
            asteroid.Y += asteroid.Vy * deltaTime;
            asteroid.Rotation += asteroid.RotationSpeed * deltaTime;

            // Wrap
            asteroid.X = (asteroid.X + _width) % _width;
            asteroid.Y = (asteroid.Y + _height) % _height;

            _asteroids[i] = asteroid;
        }

        // Respawn asteroids if too few
        while (_asteroids.Count < AsteroidCount)
        {
            _asteroids.Add(CreateAsteroid(null));
        }
    }

    private void DrawShip(IImageProcessingContext ctx, Ship ship)
    {
        var color = new Rgba32(100, 200, 255);
        var size = 12f;

        // Ship is a triangle pointing in direction of angle
        var nose = new PointF(
            ship.X + MathF.Cos(ship.Angle) * size,
            ship.Y + MathF.Sin(ship.Angle) * size);

        var left = new PointF(
            ship.X + MathF.Cos(ship.Angle + 2.5f) * size * 0.8f,
            ship.Y + MathF.Sin(ship.Angle + 2.5f) * size * 0.8f);

        var right = new PointF(
            ship.X + MathF.Cos(ship.Angle - 2.5f) * size * 0.8f,
            ship.Y + MathF.Sin(ship.Angle - 2.5f) * size * 0.8f);

        ctx.DrawLine(color, 2f, nose, left);
        ctx.DrawLine(color, 2f, left, right);
        ctx.DrawLine(color, 2f, right, nose);

        // Thruster
        var thruster = new PointF(
            ship.X - MathF.Cos(ship.Angle) * size * 0.5f,
            ship.Y - MathF.Sin(ship.Angle) * size * 0.5f);
        ctx.DrawLine(new Rgba32(255, 150, 50), 2f, left, thruster);
        ctx.DrawLine(new Rgba32(255, 150, 50), 2f, right, thruster);
    }

    private void DrawAsteroid(IImageProcessingContext ctx, Asteroid asteroid)
    {
        var color = new Rgba32(150, 150, 150);
        var points = new PointF[asteroid.Vertices.Length];

        for (var i = 0; i < asteroid.Vertices.Length; i++)
        {
            var angle = asteroid.Rotation + i * MathF.PI * 2 / asteroid.Vertices.Length;
            var radius = asteroid.Size * asteroid.Vertices[i];
            points[i] = new PointF(
                asteroid.X + MathF.Cos(angle) * radius,
                asteroid.Y + MathF.Sin(angle) * radius);
        }

        for (var i = 0; i < points.Length; i++)
        {
            var next = (i + 1) % points.Length;
            ctx.DrawLine(color, 1.5f, points[i], points[next]);
        }
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.PI * 2;
        while (angle < -MathF.PI) angle += MathF.PI * 2;
        return angle;
    }

    private struct Ship
    {
        public float X, Y, Angle, RotationSpeed;
    }

    private struct Asteroid
    {
        public float X, Y, Vx, Vy, Size, Rotation, RotationSpeed;
        public float[] Vertices;
    }

    private struct Bullet
    {
        public float X, Y, Vx, Vy, Life;
    }
}
