using System.Numerics;
using GameProject.Core.Ports;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Raylib-cs hardware adapter implementing IInputService for keyboard and mouse input.
/// </summary>
public sealed class RaylibInputService : IInputService
{
    public Vector2 MovementDirection { get; private set; } = Vector2.Zero;

    public bool IsInteractJustPressed { get; private set; }
    public bool IsHintJustPressed { get; private set; }
    public bool IsResetJustPressed { get; private set; }
    public bool IsAlbumJustPressed { get; private set; }
    public bool IsPauseJustPressed { get; private set; }
    public bool IsConfirmJustPressed { get; private set; }
    public bool IsCancelJustPressed { get; private set; }

    public Vector2 PointerPosition { get; private set; } = Vector2.Zero;
    public bool IsPointerDown { get; private set; }
    public bool IsPointerJustPressed { get; private set; }
    public bool IsPointerJustReleased { get; private set; }

    public void Update(float deltaTime)
    {
        if (!Raylib.IsWindowReady()) return;

        // 1. Movement axes with opposing input cancellation
        float x = 0f;
        float y = 0f;

        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) x -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) x += 1f;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) y -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) y += 1f;

        Vector2 raw = new(x, y);
        MovementDirection = raw == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(raw);

        // 2. Action keys
        IsInteractJustPressed = Raylib.IsKeyPressed(KeyboardKey.E) || Raylib.IsKeyPressed(KeyboardKey.Space);
        IsHintJustPressed = Raylib.IsKeyPressed(KeyboardKey.H);
        IsResetJustPressed = Raylib.IsKeyPressed(KeyboardKey.R);
        IsAlbumJustPressed = Raylib.IsKeyPressed(KeyboardKey.J);
        IsPauseJustPressed = Raylib.IsKeyPressed(KeyboardKey.Escape);
        IsConfirmJustPressed = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
        IsCancelJustPressed = Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Backspace);

        // 3. Pointer / Mouse
        PointerPosition = Raylib.GetMousePosition();
        IsPointerDown = Raylib.IsMouseButtonDown(MouseButton.Left);
        IsPointerJustPressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        IsPointerJustReleased = Raylib.IsMouseButtonReleased(MouseButton.Left);
    }
}

