using System;
using System.Text.Json.Serialization;

namespace GameProject.Core.Models;

/// <summary>
/// Root aggregate model for durable persistence and snapshot game state.
/// </summary>
public sealed class SaveData
{
    public const string CurrentSaveVersion = "1.0.0";

    private string _saveVersion = CurrentSaveVersion;
    private PlayerProfile _playerProfile = new();
    private WorldProgression _worldProgression = new();
    private Level1State _level1State = new();
    private CollectionAlbum _collectionAlbum = new();
    private AudioSettings _audioSettings = new();

    public string SaveVersion
    {
        get => _saveVersion;
        set => _saveVersion = string.IsNullOrWhiteSpace(value) ? CurrentSaveVersion : value;
    }

    public DateTimeOffset LastSavedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    public PlayerProfile PlayerProfile
    {
        get => _playerProfile ??= PlayerProfile.CreateDefault();
        set => _playerProfile = value ?? PlayerProfile.CreateDefault();
    }

    public WorldProgression WorldProgression
    {
        get => _worldProgression ??= WorldProgression.CreateDefault();
        set => _worldProgression = value ?? WorldProgression.CreateDefault();
    }

    public Level1State Level1State
    {
        get => _level1State ??= Level1State.CreateDefault();
        set => _level1State = value ?? Level1State.CreateDefault();
    }

    public CollectionAlbum CollectionAlbum
    {
        get => _collectionAlbum ??= CollectionAlbum.CreateDefault();
        set => _collectionAlbum = value ?? CollectionAlbum.CreateDefault();
    }

    public AudioSettings AudioSettings
    {
        get => _audioSettings ??= AudioSettings.CreateDefault();
        set => _audioSettings = value ?? AudioSettings.CreateDefault();
    }

    /// <summary>
    /// Convenience property providing direct, null-safe access to QuestFlags in Level1State.
    /// </summary>
    [JsonIgnore]
    public QuestFlags QuestFlags
    {
        get
        {
            Level1State ??= Level1State.CreateDefault();
            return Level1State.QuestFlags;
        }
    }

    public static SaveData CreateDefault()
    {
        return new SaveData
        {
            SaveVersion = CurrentSaveVersion,
            LastSavedTimestamp = DateTimeOffset.UtcNow,
            PlayerProfile = PlayerProfile.CreateDefault(),
            WorldProgression = WorldProgression.CreateDefault(),
            Level1State = Level1State.CreateDefault(),
            CollectionAlbum = CollectionAlbum.CreateDefault(),
            AudioSettings = AudioSettings.CreateDefault()
        };
    }

    public void Sanitize()
    {
        SaveVersion = string.IsNullOrWhiteSpace(SaveVersion) ? CurrentSaveVersion : SaveVersion;

        PlayerProfile ??= PlayerProfile.CreateDefault();
        WorldProgression ??= WorldProgression.CreateDefault();
        Level1State ??= Level1State.CreateDefault();
        CollectionAlbum ??= CollectionAlbum.CreateDefault();
        AudioSettings ??= AudioSettings.CreateDefault();

        PlayerProfile.Sanitize();
        WorldProgression.Sanitize();
        Level1State.Sanitize();
        CollectionAlbum.Sanitize();
        AudioSettings.ClampVolumes();
    }
}
