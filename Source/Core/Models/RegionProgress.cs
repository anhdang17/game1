namespace GameProject.Core.Models;

/// <summary>
/// Status descriptor for a regional node on the 2D world map.
/// </summary>
public sealed class RegionProgress
{
    public string RegionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public bool IsCompleted { get; set; }
    public float ColorPercent { get; set; } // 0.0f to 100.0f

    public RegionProgress() { }

    public RegionProgress(string regionId, string displayName, bool isUnlocked, bool isCompleted, float colorPercent)
    {
        RegionId = regionId;
        DisplayName = displayName;
        IsUnlocked = isUnlocked;
        IsCompleted = isCompleted;
        ColorPercent = colorPercent;
    }
}
