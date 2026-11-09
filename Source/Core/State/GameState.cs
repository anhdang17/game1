namespace GameProject.Core.State;

/// <summary>
/// Defines all primary scenes and modal overlay game states for Du Ký Bé Mây.
/// </summary>
public enum GameState
{
    /// <summary>Main title / start screen.</summary>
    Title,

    /// <summary>2D Regional World Map of Vietnam.</summary>
    WorldMap,

    /// <summary>2D street exploration mode in Hanoi (Bé Mây walking, talking, picking items).</summary>
    Exploration,

    /// <summary>Quest 2 Culinary assembly mini-game (Phở bowl ordering).</summary>
    MiniGamePho,

    /// <summary>Quest 3 Spatial grid clearance puzzle (Long Biên Bridge stalls).</summary>
    MiniGameStalls,

    /// <summary>Quest 3 Spatial grid clearance puzzle (alias for MiniGameStalls).</summary>
    MiniGameStall = MiniGameStalls,

    /// <summary>Active modal dialogue tree interaction.</summary>
    Dialogue,

    /// <summary>Story narrative cutscene (e.g. Gray Shadow letter sequence).</summary>
    Cutscene,

    /// <summary>Collection Album viewer overlay (Journal pages, Stamps, Fact cards).</summary>
    Album,

    /// <summary>In-game pause menu overlay.</summary>
    Pause
}
