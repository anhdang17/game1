using System;

namespace GameProject.Core.Models;

/// <summary>
/// Represents the player's basic profile, current region, and accumulated play time.
/// </summary>
public sealed class PlayerProfile
{
    private string _playerName = "Bé Mây";
    private string _currentRegionId = "Hanoi";

    public string PlayerName
    {
        get => _playerName;
        set => _playerName = string.IsNullOrWhiteSpace(value) ? "Bé Mây" : value;
    }

    public string CurrentRegionId
    {
        get => _currentRegionId;
        set => _currentRegionId = string.IsNullOrWhiteSpace(value) ? "Hanoi" : value;
    }

    public double TotalPlayTimeSeconds { get; set; } = 0.0;

    public static PlayerProfile CreateDefault() => new()
    {
        PlayerName = "Bé Mây",
        CurrentRegionId = "Hanoi",
        TotalPlayTimeSeconds = 0.0
    };

    public void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            PlayerName = "Bé Mây";
        }

        if (string.IsNullOrWhiteSpace(CurrentRegionId))
        {
            CurrentRegionId = "Hanoi";
        }

        if (double.IsNaN(TotalPlayTimeSeconds) || double.IsInfinity(TotalPlayTimeSeconds) || TotalPlayTimeSeconds < 0)
        {
            TotalPlayTimeSeconds = 0.0;
        }
    }

    public void AddPlayTime(double seconds)
    {
        if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
        {
            TotalPlayTimeSeconds += seconds;
        }
    }
}
