using System;
using System.Numerics;
using GameProject.Core.Ports;

namespace GameProject.Tests.Mocks;

public sealed class MockInputService : IInputService
{
    public Vector2 MovementDirection { get; set; } = Vector2.Zero;

    public bool InteractPressed { get; set; }
    public bool DashPressed { get; set; }
    public bool HintPressed { get; set; }
    public bool ResetPressed { get; set; }
    public bool AlbumPressed { get; set; }
    public bool PausePressed { get; set; }
    public bool ConfirmPressed { get; set; }
    public bool CancelPressed { get; set; }

    public bool IsInteractJustPressed
    {
        get => InteractPressed;
        set => InteractPressed = value;
    }
    public bool IsDashJustPressed
    {
        get => DashPressed;
        set => DashPressed = value;
    }
    public bool IsHintJustPressed
    {
        get => HintPressed;
        set => HintPressed = value;
    }
    public bool IsResetJustPressed
    {
        get => ResetPressed;
        set => ResetPressed = value;
    }
    public bool IsAlbumJustPressed
    {
        get => AlbumPressed;
        set => AlbumPressed = value;
    }
    public bool IsPauseJustPressed
    {
        get => PausePressed;
        set => PausePressed = value;
    }
    public bool IsConfirmJustPressed
    {
        get => ConfirmPressed;
        set => ConfirmPressed = value;
    }
    public bool IsCancelJustPressed
    {
        get => CancelPressed;
        set => CancelPressed = value;
    }

    public Vector2 PointerPosition { get; set; } = Vector2.Zero;
    public bool IsPointerDown { get; set; }
    public bool IsPointerJustPressed { get; set; }
    public bool IsPointerJustReleased { get; set; }

    public bool IsInteractPressed() => InteractPressed;
    public bool IsDashPressed() => DashPressed;
    public bool IsHintPressed() => HintPressed;
    public bool IsResetPressed() => ResetPressed;
    public bool IsAlbumPressed() => AlbumPressed;
    public bool IsPausePressed() => PausePressed;
    public bool IsConfirmPressed() => ConfirmPressed;
    public bool IsCancelPressed() => CancelPressed;

    public void Update(float deltaTime)
    {
        // Single frame pulse reset
    }

    public void SimulateMovement(float x, float y)
    {
        var raw = new Vector2(x, y);
        MovementDirection = raw == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(raw);
    }

    public void SimulateMove(float x, float y) => SimulateMovement(x, y);

    public void SimulateOpposingKeys(bool up, bool down, bool left, bool right)
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

    public void SimulateOpposingMove(bool up, bool down, bool left, bool right) => SimulateOpposingKeys(up, down, left, right);

    public void PressInteract() => InteractPressed = true;
    public void PressDash() => DashPressed = true;
    public void PressHint() => HintPressed = true;
    public void PressReset() => ResetPressed = true;
    public void PressAlbum() => AlbumPressed = true;
    public void PressPause() => PausePressed = true;
    public void PressConfirm() => ConfirmPressed = true;
    public void PressCancel() => CancelPressed = true;

    public void PulseInteract() => InteractPressed = true;
    public void PulseDash() => DashPressed = true;
    public void PulseHint() => HintPressed = true;
    public void PulseReset() => ResetPressed = true;
    public void PulseAlbum() => AlbumPressed = true;
    public void PulsePause() => PausePressed = true;
    public void PulseConfirm() => ConfirmPressed = true;
    public void PulseCancel() => CancelPressed = true;
    public void ResetPulses() => ReleaseAll();

    public void ReleaseAll()
    {
        MovementDirection = Vector2.Zero;
        InteractPressed = false;
        DashPressed = false;
        HintPressed = false;
        ResetPressed = false;
        AlbumPressed = false;
        PausePressed = false;
        ConfirmPressed = false;
        CancelPressed = false;
        IsPointerDown = false;
        IsPointerJustPressed = false;
        IsPointerJustReleased = false;
    }
}
