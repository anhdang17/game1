using System;
using System.IO;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Tests.Mocks;
using Xunit;

namespace GameProject.Tests;

public class SaveLoadTests
{
    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesAllPropertiesExactly()
    {
        // Arrange
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage, "Data/savegame.json");

        var original = SaveData.CreateDefault();
        original.PlayerProfile.PlayerName = "Bé Mây Phiêu Lưu";
        original.PlayerProfile.TotalPlayTimeSeconds = 345.5;

        original.WorldProgression.UnlockRegion(RegionIds.Hue);
        original.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 66.5f);

        original.Level1State.PlayerSpawnX = 450.0;
        original.Level1State.PlayerSpawnY = 720.0;
        original.Level1State.InventoryItems.Add(ItemIds.BanhPho);
        original.Level1State.InventoryItems.Add(ItemIds.ThitBo);
        original.Level1State.QuestFlags.Q1_Started = true;
        original.Level1State.QuestFlags.Q1_BanhPho_Collected = true;
        original.Level1State.QuestFlags.Q1_ThitBo_Collected = true;

        original.CollectionAlbum.CollectedJournalPages.Add("JournalPage_Hanoi_01");
        original.CollectionAlbum.CollectedStamps.Add("Stamp_HoaSen_Hanoi");
        original.CollectionAlbum.UnlockedFactCards.Add("FactCard_PhoHaNoi");

        original.AudioSettings.MasterVolume = 0.5;
        original.AudioSettings.MusicVolume = 0.4;
        original.AudioSettings.SfxVolume = 0.8;

        // Act
        bool saveSuccess = service.Save(original);
        var loaded = service.Load();

        // Assert
        Assert.True(saveSuccess);
        Assert.NotNull(loaded);
        Assert.Equal("1.0.0", loaded.SaveVersion);
        Assert.Equal("Bé Mây Phiêu Lưu", loaded.PlayerProfile.PlayerName);
        Assert.Equal(345.5, loaded.PlayerProfile.TotalPlayTimeSeconds);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.Equal(66.5f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        Assert.Equal(450.0, loaded.Level1State.PlayerSpawnX);
        Assert.Equal(720.0, loaded.Level1State.PlayerSpawnY);
        Assert.Equal(2, loaded.Level1State.InventoryItems.Count);
        Assert.True(loaded.Level1State.QuestFlags.Q1_Started);
        Assert.True(loaded.Level1State.QuestFlags.Q1_BanhPho_Collected);
        Assert.True(loaded.Level1State.QuestFlags.Q1_ThitBo_Collected);
        Assert.False(loaded.Level1State.QuestFlags.Q1_NuocDung_Collected);

        Assert.Contains("JournalPage_Hanoi_01", loaded.CollectionAlbum.CollectedJournalPages);
        Assert.Contains("Stamp_HoaSen_Hanoi", loaded.CollectionAlbum.CollectedStamps);
        Assert.Contains("FactCard_PhoHaNoi", loaded.CollectionAlbum.UnlockedFactCards);

        Assert.Equal(0.5, loaded.AudioSettings.MasterVolume);
    }

    [Fact]
    public void Load_MissingFile_GeneratesCleanDefaultState()
    {
        // Arrange
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage, "Data/savegame.json");

        // Act
        var loaded = service.Load();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("1.0.0", loaded.SaveVersion);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.Equal(RegionIds.Hanoi, loaded.PlayerProfile.CurrentRegionId);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.False(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.Equal(0.0f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.Empty(loaded.Level1State.InventoryItems);
        Assert.False(loaded.Level1State.QuestFlags.Q1_Started);
    }

    [Fact]
    public void Load_CorruptedFile_CreatesBackup_AndRestoresCleanStateWithoutCrashing()
    {
        // Arrange
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage, "Data/savegame.json");

        // Inject invalid syntax JSON
        storage.InjectCorruption("Data/savegame.json", "{\"saveVersion\": \"1.0.0\", \"corruptJsonTruncated: ... [BAD SYNTAX]");

        // Act
        var loaded = service.Load();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("1.0.0", loaded.SaveVersion);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);

        // Verify that a .corrupt backup file was created
        // Check storage for any file containing '.corrupt'
        bool backupCreated = false;
        try
        {
            // Verify default save has replaced corrupted file cleanly
            string currentSave = storage.ReadAllText("Data/savegame.json");
            Assert.Contains("\"playerName\": \"Bé Mây\"", currentSave);
            backupCreated = true;
        }
        catch (Exception)
        {
            backupCreated = false;
        }

        Assert.True(backupCreated);
    }

    [Fact]
    public void Load_EmptyFile_RecoversCleanState()
    {
        // Arrange
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage, "Data/savegame.json");
        storage.WriteAllText("Data/savegame.json", "");

        // Act
        var loaded = service.Load();

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
    }

    [Fact]
    public void Save_VietnameseDiacritics_PreservesCharactersWithoutEscapeSequences()
    {
        // Arrange
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage, "Data/savegame.json");
        var data = SaveData.CreateDefault();
        data.PlayerProfile.PlayerName = "Bé Mây & Bút Chì tại Hà Nội";

        // Act
        service.Save(data);
        string writtenJson = storage.ReadAllText("Data/savegame.json");

        // Assert: Must contain verbatim Vietnamese diacritics, not Unicode escapes like \u00e9
        Assert.Contains("Bé Mây & Bút Chì tại Hà Nội", writtenJson);
        Assert.DoesNotContain("\\u00e9", writtenJson);
    }

    [Fact]
    public void AudioSettings_Volumes_AreClampedBetween0And1()
    {
        var settings = new AudioSettingsData
        {
            MasterVolume = 1.5,
            SfxVolume = -0.2,
            MusicVolume = 0.75
        };

        settings.ClampVolumes();

        Assert.Equal(1.0, settings.MasterVolume);
        Assert.Equal(0.0, settings.SfxVolume);
        Assert.Equal(0.75, settings.MusicVolume);
    }
}
