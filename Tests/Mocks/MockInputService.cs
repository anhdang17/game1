using System;
using System.Numerics;
using GameProject.Core.Ports;

namespace GameProject.Tests.Mocks;

public sealed class MockInputService : IInputService
{
    public Vector2 MovementDirection { get; set; } = Vector2.Zero;

    public bool InteractPressed { get; set; }
    public bool HintPressed { get; set; }
    public bool ResetPressed { get; set; }
    public bool AlbumPressed { get; set; }
    public bool PausePressed { get; set; }
    public bool ConfirmPressed { get; set; }
    public bool CancelPressed { get; set; }

    public bool IsInteractJustPressed => InteractPressed;
    public bool IsHintJustPressed => HintPressed;
    public bool IsResetJustPressed => ResetPressed;
    public bool IsAlbumJustPressed => AlbumPressed;
    public bool IsPauseJustPressed => PausePressed;
    public bool IsConfirmJustPressed => ConfirmPressed;
    public bool IsCancelJustPressed => CancelPressed;

    public Vector2 PointerPosition { get; set; } = Vector2.Zero;
    public bool IsPointerDown { get; set; }
    public bool IsPointerJustPressed { get; set; }
    public bool IsPointerJustReleased { get; set; }

    public bool IsInteractPressed() => InteractPressed;
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

    public void PressInteract() => InteractPressed = true;
    public void PressHint() => HintPressed = true;
    public void PressReset() => ResetPressed = true;
    public void PressAlbum() => AlbumPressed = true;
    public void PressPause() => PausePressed = true;
    public void PressConfirm() => ConfirmPressed = true;
    public void PressCancel() => CancelPressed = true;

    public void ReleaseAll()
    {
        MovementDirection = Vector2.Zero;
        InteractPressed = false;
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
