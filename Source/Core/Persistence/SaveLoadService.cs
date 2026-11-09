using System;
using System.IO;
using System.Text.Json;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Ports;

namespace GameProject.Core.Persistence;

/// <summary>
/// Production-grade persistence service implementing atomic writes via temporary files,
/// backup rotation (.bak), corruption isolation (.corrupt), and Vietnamese UTF-8 preservation.
/// </summary>
public class SaveLoadService
{
    public const string DefaultSavePath = "Data/savegame.json";
    public const string TempSuffix = ".tmp";
    public const string BackupSuffix = ".bak";
    public const string CorruptSuffix = ".corrupt";

    private readonly ISaveStorage _storage;
    private readonly string _saveFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IEventBus? _eventBus;

    public SaveLoadService(string? saveFilePath = null, ISaveStorage? storage = null, IEventBus? eventBus = null)
    {
        _saveFilePath = saveFilePath ?? DefaultSavePath;
        _storage = storage ?? new FileSaveStorage();
        _jsonOptions = SaveGameJsonContext.CreateOptions();
        _eventBus = eventBus;
    }

    public SaveLoadService(ISaveStorage storage, string? saveFilePath = null, IEventBus? eventBus = null)
        : this(saveFilePath, storage, eventBus) { }

    public string SaveFilePath => _saveFilePath;
    public ISaveStorage Storage => _storage;

    public bool HasSaveFile(string? path = null) => _storage.FileExists(path ?? _saveFilePath);

    /// <summary>
    /// Atomically persists game state to disk using a temporary file and backup rotation.
    /// </summary>
    public bool Save(SaveData data) => SaveGame(data, _saveFilePath);

    /// <summary>
    /// Saves game data to the specified path or the default path.
    /// </summary>
    public bool SaveGame(SaveData data, string? customPath = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        string targetPath = customPath ?? _saveFilePath;

        try
        {
            data.LastSavedTimestamp = DateTimeOffset.UtcNow;
            data.Sanitize();

            string json = JsonSerializer.Serialize(data, _jsonOptions);

            string? dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _storage.EnsureDirectoryExists(dir);
            }

            string tempPath = targetPath + TempSuffix;
            string backupPath = targetPath + BackupSuffix;

            // 1. Write payload to temporary file
            _storage.WriteAllText(tempPath, json);

            // 2. Atomically replace destination with backup rotation
            _storage.ReplaceFile(tempPath, targetPath, backupPath);

            _eventBus?.Publish(new GameSavedEvent(targetPath, Success: true));
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveLoadService] Save failed to '{targetPath}': {ex.Message}");
            _eventBus?.Publish(new GameSavedEvent(targetPath, Success: false, ErrorMessage: ex.Message));
            return false;
        }
    }

    /// <summary>
    /// Loads game state from storage with automatic corruption detection and clean fallback.
    /// </summary>
    public SaveData Load() => LoadGame(_saveFilePath);

    /// <summary>
    /// Loads game data from the specified path or the default path.
    /// </summary>
    public SaveData LoadGame(string? customPath = null)
    {
        string targetPath = customPath ?? _saveFilePath;

        if (!_storage.FileExists(targetPath))
        {
            // Fresh start: return clean default state
            var defaultData = SaveData.CreateDefault();
            _eventBus?.Publish(new GameLoadedEvent(targetPath, Success: true, WasCorruptedAndRecovered: false));
            return defaultData;
        }

        try
        {
            string json = _storage.ReadAllText(targetPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                BackupCorruptedFile(targetPath);
                var defaultData = SaveData.CreateDefault();
                SaveGame(defaultData, targetPath);
                _eventBus?.Publish(new GameLoadedEvent(targetPath, Success: true, WasCorruptedAndRecovered: true));
                return defaultData;
            }

            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, _jsonOptions);
            if (data == null)
            {
                BackupCorruptedFile(targetPath);
                var defaultData = SaveData.CreateDefault();
                SaveGame(defaultData, targetPath);
                _eventBus?.Publish(new GameLoadedEvent(targetPath, Success: true, WasCorruptedAndRecovered: true));
                return defaultData;
            }

            data.Sanitize();
            _eventBus?.Publish(new GameLoadedEvent(targetPath, Success: true, WasCorruptedAndRecovered: false));
            return data;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveLoadService] Corrupt save file detected at '{targetPath}': {ex.Message}");
            BackupCorruptedFile(targetPath);
            var defaultData = SaveData.CreateDefault();
            SaveGame(defaultData, targetPath);
            _eventBus?.Publish(new GameLoadedEvent(targetPath, Success: true, WasCorruptedAndRecovered: true));
            return defaultData;
        }
    }

    /// <summary>
    /// Deletes the save file and associated temporary files.
    /// </summary>
    public void DeleteSave(string? customPath = null)
    {
        string targetPath = customPath ?? _saveFilePath;
        _storage.DeleteFile(targetPath);

        string tempPath = targetPath + TempSuffix;
        if (_storage.FileExists(tempPath))
        {
            _storage.DeleteFile(tempPath);
        }
    }

    private void BackupCorruptedFile(string targetPath)
    {
        try
        {
            if (_storage.FileExists(targetPath))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string timestampedBackup = $"{targetPath}.{timestamp}{CorruptSuffix}";
                string genericBackup = $"{targetPath}{CorruptSuffix}";

                _storage.CopyFile(targetPath, timestampedBackup, overwrite: true);
                _storage.CopyFile(targetPath, genericBackup, overwrite: true);

                Console.WriteLine($"[SaveLoadService] Backed up corrupted save to: {timestampedBackup}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveLoadService] Failed to backup corrupt file: {ex.Message}");
        }
    }
}
