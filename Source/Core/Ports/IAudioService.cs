namespace GameProject.Core.Ports;

/// <summary>
/// Predefined sound effect types matching game events and actions.
/// </summary>
public enum SfxType
{
    None = 0,
    ItemCollected,
    InteractPrompt,
    PhoIngredientPlaced,
    PhoIngredientRemoved,
    PhoCookSuccess,
    PhoCookFailed,
    StallSlide,
    StallBlocked,
    PuzzleReset,
    GateUnlocked,
    ColorBloomWave,
    JournalPageAwarded,
    StampAwarded,
    FactCardUnlocked,
    DialogueBlip,
    ChoiceSelected,
    AlbumPageTurn,
    ButtonClick,
    StepStreet,
    ErrorChime
}

/// <summary>
/// Predefined background music tracks.
/// </summary>
public enum MusicTrack
{
    None = 0,
    TitleTheme,
    HanoiStreetsTheme,
    PhoPuzzleTheme,
    StallPuzzleTheme,
    CelebrationFanfare,
    GrayShadowTheme,
    GrayShadowMystery = GrayShadowTheme
}

/// <summary>
/// Abstract port for audio triggers and music playback.
/// Allows headless test execution with zero sound card dependency.
/// </summary>
public interface IAudioService
{
    float MasterVolume { get; set; }
    float MusicVolume { get; set; }
    float SfxVolume { get; set; }
    bool IsMuted { get; set; }

    void PlaySfx(SfxType sfx, float volumeModifier = 1.0f);
    void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f);
    void StopMusic(float fadeDuration = 0.5f);
    void PauseMusic();
    void ResumeMusic();

    bool IsMusicPlaying { get; }
    MusicTrack CurrentTrack { get; }

    void Update(float deltaTime);
}
