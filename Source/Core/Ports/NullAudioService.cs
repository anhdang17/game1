namespace GameProject.Core.Ports;

/// <summary>
/// Headless no-op implementation of IAudioService.
/// Safe for CI/test environments without audio hardware.
/// </summary>
public class NullAudioService : IAudioService
{
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool IsMuted { get; set; } = false;

    public bool IsMusicPlaying => false;
    public MusicTrack CurrentTrack => MusicTrack.None;

    public void PlaySfx(SfxType sfx, float volumeModifier = 1.0f) { }
    public void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f) { }
    public void StopMusic(float fadeDuration = 0.5f) { }
    public void PauseMusic() { }
    public void ResumeMusic() { }
    public void Update(float deltaTime) { }
}
