using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Type classification for particle simulation and vector rendering.
/// </summary>
public enum ParticleType
{
    CloudPuff,
    Steam,
    Sparkle,
    Confetti
}

/// <summary>
/// Individual particle instance with kinematics, appearance, and lifetime state.
/// </summary>
public struct Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Color;
    public float Size;
    public float Lifetime;
    public float MaxLifetime;
    public ParticleType Type;
    public float Rotation;
    public float RotationSpeed;
}

/// <summary>
/// Lightweight, headless-safe particle emitter supporting cloud puffs, broth steam, sparkles, and victory confetti.
/// </summary>
public sealed class ParticleEmitter
{
    private readonly List<Particle> _particles = new(256);
    private readonly Random _rand = new();
    private float _steamAccumulator = 0f;

    private static readonly Color[] ConfettiPalette =
    [
        new Color(230, 57, 70, 255),   // Crimson Red
        new Color(244, 162, 97, 255),  // Coral / Gold
        new Color(233, 196, 106, 255), // Saffron Yellow
        new Color(42, 157, 143, 255),  // Jade Green
        new Color(38, 70, 83, 255),    // Deep Teal
        new Color(155, 93, 229, 255),  // Magical Purple
        new Color(247, 37, 133, 255)   // Vibrant Pink
    ];

    public int ParticleCount => _particles.Count;

    /// <summary>
    /// Emits pastel cloud puff particles that expand, decelerate, and fade out.
    /// </summary>
    public void EmitCloudPuff(Vector2 pos, int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rand.NextDouble() * Math.PI * 2.0);
            float speed = (float)(15.0 + _rand.NextDouble() * 35.0);
            float offsetDist = (float)(_rand.NextDouble() * 6.0);
            Vector2 offset = new(MathF.Cos(angle) * offsetDist, MathF.Sin(angle) * offsetDist);

            _particles.Add(new Particle
            {
                Position = pos + offset,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                Color = new Color(245, 250, 255, 215),
                Size = (float)(6.0 + _rand.NextDouble() * 4.0),
                Lifetime = 0f,
                MaxLifetime = (float)(0.35 + _rand.NextDouble() * 0.2),
                Type = ParticleType.CloudPuff,
                Rotation = angle,
                RotationSpeed = (float)((_rand.NextDouble() - 0.5) * 4.0)
            });
        }
    }

    /// <summary>
    /// Continuously emits rising steam particles over time (e.g. from Bà An's phở pot or bowls).
    /// </summary>
    public void EmitSteam(Vector2 potPos, float dt)
    {
        if (dt <= 0f) return;
        _steamAccumulator += dt;
        if (_steamAccumulator > 0.5f) _steamAccumulator = 0.5f;

        while (_steamAccumulator >= 0.07f)
        {
            _steamAccumulator -= 0.07f;
            float offsetX = (float)((_rand.NextDouble() - 0.5) * 14.0);
            float offsetY = (float)((_rand.NextDouble() - 0.5) * 4.0);
            float speedY = (float)(-40.0 - _rand.NextDouble() * 30.0);
            float speedX = (float)((_rand.NextDouble() - 0.5) * 10.0);

            _particles.Add(new Particle
            {
                Position = potPos + new Vector2(offsetX, offsetY),
                Velocity = new Vector2(speedX, speedY),
                Color = new Color(245, 245, 240, 160),
                Size = (float)(4.0 + _rand.NextDouble() * 3.0),
                Lifetime = 0f,
                MaxLifetime = (float)(0.9 + _rand.NextDouble() * 0.4),
                Type = ParticleType.Steam,
                Rotation = 0f,
                RotationSpeed = (float)((_rand.NextDouble() - 0.5) * 2.0)
            });
        }
    }

    /// <summary>
    /// Emits a burst of sparkling magical stars or sparkles at the specified position.
    /// </summary>
    public void EmitSparkles(Vector2 pos, Color color, int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rand.NextDouble() * Math.PI * 2.0);
            float speed = (float)(50.0 + _rand.NextDouble() * 70.0);
            _particles.Add(new Particle
            {
                Position = pos,
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed),
                Color = color,
                Size = (float)(3.0 + _rand.NextDouble() * 2.5),
                Lifetime = 0f,
                MaxLifetime = (float)(0.3 + _rand.NextDouble() * 0.2),
                Type = ParticleType.Sparkle,
                Rotation = angle,
                RotationSpeed = (float)((_rand.NextDouble() - 0.5) * 8.0)
            });
        }
    }

    /// <summary>
    /// Emits celebratory multicolored confetti that flies upward and flutters down under gravity.
    /// </summary>
    public void EmitConfetti(Vector2 center, int count = 30)
    {
        for (int i = 0; i < count; i++)
        {
            Color confettiColor = ConfettiPalette[_rand.Next(ConfettiPalette.Length)];
            float vx = (float)((_rand.NextDouble() - 0.5) * 180.0);
            float vy = (float)(-150.0 - _rand.NextDouble() * 120.0);

            _particles.Add(new Particle
            {
                Position = center,
                Velocity = new Vector2(vx, vy),
                Color = confettiColor,
                Size = (float)(4.0 + _rand.NextDouble() * 3.0),
                Lifetime = 0f,
                MaxLifetime = (float)(1.2 + _rand.NextDouble() * 0.6),
                Type = ParticleType.Confetti,
                Rotation = (float)(_rand.NextDouble() * Math.PI * 2.0),
                RotationSpeed = (float)((_rand.NextDouble() - 0.5) * 12.0)
            });
        }
    }

    /// <summary>
    /// Updates particle physics, animations, and life timers.
    /// </summary>
    public void Update(float dt)
    {
        if (dt <= 0f) return;

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Lifetime += dt;
            if (p.Lifetime >= p.MaxLifetime)
            {
                _particles.RemoveAt(i);
                continue;
            }

            p.Position += p.Velocity * dt;
            p.Rotation += p.RotationSpeed * dt;

            switch (p.Type)
            {
                case ParticleType.CloudPuff:
                    p.Velocity *= MathF.Pow(0.15f, dt);
                    p.Size += 8.0f * dt;
                    break;

                case ParticleType.Steam:
                    p.Velocity = new Vector2(
                        p.Velocity.X + MathF.Sin(p.Lifetime * 5.0f) * 12.0f * dt,
                        p.Velocity.Y - 10.0f * dt
                    );
                    p.Size += 7.0f * dt;
                    break;

                case ParticleType.Sparkle:
                    p.Velocity *= MathF.Pow(0.1f, dt);
                    p.Size = MathF.Max(0.5f, p.Size - 3.0f * dt);
                    break;

                case ParticleType.Confetti:
                    p.Velocity = new Vector2(
                        p.Velocity.X * MathF.Pow(0.8f, dt),
                        p.Velocity.Y + 340.0f * dt // gravity
                    );
                    break;
            }

            _particles[i] = p;
        }
    }

    /// <summary>
    /// Renders active particles using Raylib 2D vector primitives.
    /// Strictly guarded by IsWindowReady() for headless test safety.
    /// </summary>
    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            float progress = Math.Clamp(p.Lifetime / p.MaxLifetime, 0f, 1f);
            float alphaFactor = 1f - progress;
            byte alpha = (byte)Math.Clamp((int)(p.Color.A * alphaFactor), 0, 255);
            Color drawColor = new(p.Color.R, p.Color.G, p.Color.B, alpha);

            switch (p.Type)
            {
                case ParticleType.CloudPuff:
                case ParticleType.Steam:
                    Raylib.DrawCircleV(p.Position, MathF.Max(1f, p.Size), drawColor);
                    break;

                case ParticleType.Sparkle:
                    float arm = MathF.Max(1f, p.Size);
                    Raylib.DrawLineV(p.Position - new Vector2(arm, 0), p.Position + new Vector2(arm, 0), drawColor);
                    Raylib.DrawLineV(p.Position - new Vector2(0, arm), p.Position + new Vector2(0, arm), drawColor);
                    Raylib.DrawCircleV(p.Position, MathF.Max(0.5f, arm * 0.4f), drawColor);
                    break;

                case ParticleType.Confetti:
                    Raylib.DrawRectanglePro(
                        new Rectangle(p.Position.X, p.Position.Y, p.Size, p.Size * 1.5f),
                        new Vector2(p.Size * 0.5f, p.Size * 0.75f),
                        p.Rotation * (180f / MathF.PI),
                        drawColor
                    );
                    break;
            }
        }
    }

    /// <summary>
    /// Combined convenience method to advance and render particle effects.
    /// </summary>
    public void UpdateAndDraw(float dt)
    {
        Update(dt);
        Draw();
    }

    /// <summary>
    /// Clears all active particles.
    /// </summary>
    public void Clear()
    {
        _particles.Clear();
        _steamAccumulator = 0f;
    }
}
