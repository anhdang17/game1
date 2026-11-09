using System;

namespace GameProject.Core.Models;

/// <summary>
/// Audio volume preferences container.
/// </summary>
public class AudioSettings
{
    public double MasterVolume { get; set; } = 0.8;
    public double SfxVolume { get; set; } = 0.9;
    public double MusicVolume { get; set; } = 0.7;

    public static AudioSettings CreateDefault() => new()
    {
        MasterVolume = 0.8,
        SfxVolume = 0.9,
        MusicVolume = 0.7
    };

    public void ClampVolumes()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0.0, 1.0);
        SfxVolume = Math.Clamp(SfxVolume, 0.0, 1.0);
        MusicVolume = Math.Clamp(MusicVolume, 0.0, 1.0);
    }
}

/// <summary>
/// Backward-compatible alias for AudioSettings.
/// </summary>
public sealed class AudioSettingsData : AudioSettings
{
    public new static AudioSettingsData CreateDefault() => new()
    {
        MasterVolume = 0.8,
        SfxVolume = 0.9,
        MusicVolume = 0.7
    };
}
