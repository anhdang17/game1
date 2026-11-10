using System.Numerics;

namespace GameProject.Core.Ports;

/// <summary>
/// Scriptable input service mock for driving headless automated tests and game simulations.
/// </summary>
public class MockInputService : IInputService
{
    public Vector2 MovementDirection { get; set; } = Vector2.Zero;

    public bool IsInteractJustPressed { get; set; }
    public bool IsDashJustPressed { get; set; }
    public bool IsHintJustPressed { get; set; }
    public bool IsResetJustPressed { get; set; }
    public bool IsAlbumJustPressed { get; set; }
    public bool IsPauseJustPressed { get; set; }
    public bool IsConfirmJustPressed { get; set; }
    public bool IsCancelJustPressed { get; set; }

    public bool IsInteractPressed() => IsInteractJustPressed;
    public bool IsDashPressed() => IsDashJustPressed;
    public bool IsHintPressed() => IsHintJustPressed;
    public bool IsResetPressed() => IsResetJustPressed;
    public bool IsAlbumPressed() => IsAlbumJustPressed;
    public bool IsPausePressed() => IsPauseJustPressed;
    public bool IsConfirmPressed() => IsConfirmJustPressed;
    public bool IsCancelPressed() => IsCancelJustPressed;

    public Vector2 PointerPosition { get; set; } = Vector2.Zero;
    public bool IsPointerDown { get; set; }
    public bool IsPointerJustPressed { get; set; }
    public bool IsPointerJustReleased { get; set; }

    public void Update(float deltaTime)
    {
        // Single-frame pulses can be reset here if requested, or manually cleared via ResetPulses()
    }

    public void Tick(float deltaTime) => Update(deltaTime);

    public void SimulateMove(float x, float y)
    {
        var raw = new Vector2(x, y);
        MovementDirection = raw == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(raw);
    }

    /// <summary>
    /// Simulates simultaneous key presses. Opposing inputs cancel to Vector2.Zero (Edge Case #13).
    /// </summary>
    public void SimulateOpposingMove(bool up, bool down, bool left, bool right)
    {
        float x = 0;
        float y = 0;
        if (left) x -= 1;
        if (right) x += 1;
        if (up) y -= 1;
        if (down) y += 1;

        var v = new Vector2(x, y);
        MovementDirection = v == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(v);
    }

    public void PulseInteract() => IsInteractJustPressed = true;
    public void PulseDash() => IsDashJustPressed = true;
    public void PulseHint() => IsHintJustPressed = true;
    public void PulseReset() => IsResetJustPressed = true;
    public void PulseAlbum() => IsAlbumJustPressed = true;
    public void PulsePause() => IsPauseJustPressed = true;
    public void PulseConfirm() => IsConfirmJustPressed = true;
    public void PulseCancel() => IsCancelJustPressed = true;

    public void SetPointer(float x, float y, bool isDown)
    {
        PointerPosition = new Vector2(x, y);
        if (isDown && !IsPointerDown)
        {
            IsPointerJustPressed = true;
        }
        else if (!isDown && IsPointerDown)
        {
            IsPointerJustReleased = true;
        }
        IsPointerDown = isDown;
    }

    public void ResetPulses()
    {
        IsInteractJustPressed = false;
        IsDashJustPressed = false;
        IsHintJustPressed = false;
        IsResetJustPressed = false;
        IsAlbumJustPressed = false;
        IsPauseJustPressed = false;
        IsConfirmJustPressed = false;
        IsCancelJustPressed = false;
        IsPointerJustPressed = false;
        IsPointerJustReleased = false;
    }

    public void ResetAll()
    {
        MovementDirection = Vector2.Zero;
        ResetPulses();
        PointerPosition = Vector2.Zero;
        IsPointerDown = false;
    }
}
