using System.Numerics;

namespace GameProject.Core.Ports;

/// <summary>
/// Abstract port for game input. Fully decouples player controls from windowing and graphics engines,
/// enabling scriptable and headless automated test simulation.
/// </summary>
public interface IInputService
{
    /// <summary>
    /// Gets the normalized 2D movement direction vector (X: -1 to 1, Y: -1 to 1).
    /// Opposing inputs (e.g. Left + Right) cancel out to zero.
    /// </summary>
    Vector2 MovementDirection { get; }

    /// <summary>True on the exact frame the player presses the primary interaction key [E] or Space.</summary>
    bool IsInteractJustPressed { get; }
    bool IsInteractPressed() => IsInteractJustPressed;

    /// <summary>True on the frame the player requests a companion hint via [H].</summary>
    bool IsHintJustPressed { get; }
    bool IsHintPressed() => IsHintJustPressed;

    /// <summary>True on the frame the player requests a puzzle reset via [R].</summary>
    bool IsResetJustPressed { get; }
    bool IsResetPressed() => IsResetJustPressed;

    /// <summary>True on the frame the player toggles the Collection Album via [J].</summary>
    bool IsAlbumJustPressed { get; }
    bool IsAlbumPressed() => IsAlbumJustPressed;

    /// <summary>True on the frame the player toggles Pause or backs out via [Escape].</summary>
    bool IsPauseJustPressed { get; }
    bool IsPausePressed() => IsPauseJustPressed;

    /// <summary>True on the frame the player confirms an option or advances dialogue via [Enter] or [Space].</summary>
    bool IsConfirmJustPressed { get; }
    bool IsConfirmPressed() => IsConfirmJustPressed;

    /// <summary>True on the frame the player cancels or closes a modal via [Escape] or [Backspace].</summary>
    bool IsCancelJustPressed { get; }
    bool IsCancelPressed() => IsCancelJustPressed;

    /// <summary>Screen coordinates of the pointer / cursor.</summary>
    Vector2 PointerPosition { get; }

    /// <summary>True while the primary mouse button / touch is held down.</summary>
    bool IsPointerDown { get; }

    /// <summary>True on the single frame the pointer is pressed.</summary>
    bool IsPointerJustPressed { get; }

    /// <summary>True on the single frame the pointer is released.</summary>
    bool IsPointerJustReleased { get; }

    /// <summary>Updates input states for the current frame.</summary>
    void Update(float deltaTime);

    /// <summary>Alias for Update.</summary>
    void Tick(float deltaTime) => Update(deltaTime);
}
