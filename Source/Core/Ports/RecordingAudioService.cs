using System.Collections.Generic;

namespace GameProject.Core.Ports;

/// <summary>
/// Recording audio mock that captures sound effect and music calls for test assertions.
/// </summary>
public class RecordingAudioService : IAudioService
{
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool IsMuted { get; set; } = false;

    public bool IsMusicPlaying => CurrentTrack != MusicTrack.None;
    public MusicTrack CurrentTrack { get; private set; } = MusicTrack.None;

    public List<SfxType> PlayedSfx { get; } = new();
    public List<MusicTrack> PlayedTracks { get; } = new();

    public SfxType LastSfx => PlayedSfx.Count > 0 ? PlayedSfx[^1] : SfxType.None;
    public int SfxCallCount => PlayedSfx.Count;

    public void PlaySfx(SfxType sfx, float volumeModifier = 1.0f)
    {
        PlayedSfx.Add(sfx);
    }

    public void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f)
    {
        CurrentTrack = track;
        PlayedTracks.Add(track);
    }

    public void StopMusic(float fadeDuration = 0.5f)
    {
        CurrentTrack = MusicTrack.None;
    }

    public void PauseMusic() { }
    public void ResumeMusic() { }
    public void Update(float deltaTime) { }

    public void Clear()
    {
        PlayedSfx.Clear();
        PlayedTracks.Clear();
        CurrentTrack = MusicTrack.None;
    }
}
