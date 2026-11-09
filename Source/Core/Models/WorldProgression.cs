using System;
using System.Collections.Generic;
using System.Linq;

namespace GameProject.Core.Models;

/// <summary>
/// Predefined region identifiers across Vietnam.
/// </summary>
public static class RegionIds
{
    public const string Hanoi = "Hanoi";
    public const string Hue = "Hue";
    public const string HoiAn = "HoiAn";
    public const string SaiGon = "SaiGon";
    public const string Saigon = "SaiGon";
}

/// <summary>
/// Predefined quest identifiers for the vertical slice.
/// </summary>
public static class QuestIds
{
    public const string Hanoi01 = "Quest_Hanoi_01";
    public const string Hanoi02 = "Quest_Hanoi_02";
    public const string Hanoi03 = "Quest_Hanoi_03";
}

/// <summary>
/// Encapsulates global world progression, unlocked regions, completed regions, and color restoration.
/// </summary>
public sealed class WorldProgression
{
    public const string RegionHanoi = "Hanoi";
    public const string RegionHue = "Hue";
    public const string RegionHoiAn = "HoiAn";
    public const string RegionSaigon = "Saigon";

    private List<string> _unlockedRegions = new() { RegionHanoi };
    private List<string> _completedRegions = new();
    private Dictionary<string, float> _regionColorRestoration = new()
    {
        { RegionHanoi, 0.0f }
    };

    public List<string> UnlockedRegions
    {
        get => _unlockedRegions ??= new List<string> { RegionHanoi };
        set => _unlockedRegions = value ?? new List<string> { RegionHanoi };
    }

    public List<string> CompletedRegions
    {
        get => _completedRegions ??= new List<string>();
        set => _completedRegions = value ?? new List<string>();
    }

    public Dictionary<string, float> RegionColorRestoration
    {
        get => _regionColorRestoration ??= new Dictionary<string, float> { { RegionHanoi, 0.0f } };
        set => _regionColorRestoration = value ?? new Dictionary<string, float> { { RegionHanoi, 0.0f } };
    }

    public static WorldProgression CreateDefault() => new()
    {
        UnlockedRegions = new List<string> { RegionHanoi },
        CompletedRegions = new List<string>(),
        RegionColorRestoration = new Dictionary<string, float>
        {
            { RegionHanoi, 0.0f }
        }
    };

    public bool IsRegionUnlocked(string regionId) =>
        UnlockedRegions.Contains(regionId, StringComparer.OrdinalIgnoreCase);

    public bool IsRegionCompleted(string regionId) =>
        CompletedRegions.Contains(regionId, StringComparer.OrdinalIgnoreCase);

    public float GetColorRestoration(string regionId)
    {
        return RegionColorRestoration.TryGetValue(regionId, out var val) ? val : 0.0f;
    }

    public float GetRegionColor(string regionId) => GetColorRestoration(regionId);

    public void SetColorRestoration(string regionId, float percentage)
    {
        percentage = Math.Clamp(percentage, 0.0f, 100.0f);
        RegionColorRestoration[regionId] = percentage;
    }

    public void SetRegionColor(string regionId, float percentage) => SetColorRestoration(regionId, percentage);

    public bool UnlockRegion(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return false;
        if (!IsRegionUnlocked(regionId))
        {
            UnlockedRegions.Add(regionId);
            if (!RegionColorRestoration.ContainsKey(regionId))
            {
                RegionColorRestoration[regionId] = 0.0f;
            }
            return true;
        }
        return false;
    }

    public bool MarkRegionCompleted(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return false;
        if (!IsRegionCompleted(regionId))
        {
            CompletedRegions.Add(regionId);
            SetColorRestoration(regionId, 100.0f);
            return true;
        }
        return false;
    }

    public bool CompleteRegion(string regionId) => MarkRegionCompleted(regionId);

    public void Sanitize()
    {
        UnlockedRegions ??= new List<string>();
        UnlockedRegions.RemoveAll(string.IsNullOrWhiteSpace);
        if (!UnlockedRegions.Contains(RegionHanoi, StringComparer.OrdinalIgnoreCase))
        {
            UnlockedRegions.Add(RegionHanoi);
        }

        CompletedRegions ??= new List<string>();
        CompletedRegions.RemoveAll(string.IsNullOrWhiteSpace);

        RegionColorRestoration ??= new Dictionary<string, float>();

        foreach (var key in RegionColorRestoration.Keys.ToList())
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                RegionColorRestoration.Remove(key);
                continue;
            }

            float val = RegionColorRestoration[key];
            if (float.IsNaN(val) || float.IsInfinity(val))
            {
                RegionColorRestoration[key] = 0.0f;
            }
            else
            {
                RegionColorRestoration[key] = Math.Clamp(val, 0.0f, 100.0f);
            }
        }

        if (!RegionColorRestoration.ContainsKey(RegionHanoi))
        {
            RegionColorRestoration[RegionHanoi] = 0.0f;
        }
    }
}
