using System;
using System.Collections.Generic;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Quests;
using GameProject.Tests.Mocks;
using GameProject.UI;
using Xunit;

namespace GameProject.Tests;

public class LevelProgressionClimaxTests
{
    [Fact]
    public void CompleteLevel1_AwardsJournalPage1_WithCorrectMetadata()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        // 1. Verify ID in both property accessors
        Assert.Contains(AlbumIds.HanoiJournalPage, data.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumIds.HanoiJournalPage, data.CollectionAlbum.JournalPages);

        // 2. Verify AlbumService rich metadata
        var albumService = new AlbumService();
        var pages = albumService.GetUnlockedJournalPages(data.CollectionAlbum);
        Assert.Single(pages);
        Assert.Equal(AlbumIds.HanoiJournalPage, pages[0].Id);
        Assert.Contains("Phố Cổ", pages[0].Title);
        Assert.Equal(RegionIds.Hanoi, pages[0].Region);
        Assert.NotEmpty(pages[0].StoryText);
        Assert.Equal("Art_Journal_Hanoi_01", pages[0].IllustrationKey);
    }

    [Fact]
    public void CompleteLevel1_AwardsLotusStamp_WithTimestamp()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        // 1. Verify Stamp ID in collection
        Assert.Contains(AlbumIds.HanoiLotusStamp, data.CollectionAlbum.CollectedStamps);
        Assert.Contains(AlbumIds.HanoiLotusStamp, data.CollectionAlbum.TravelStamps);

        // 2. Verify Stamp rich metadata
        var albumService = new AlbumService();
        var stamps = albumService.GetUnlockedTravelStamps(data.CollectionAlbum);
        Assert.Single(stamps);
        Assert.Equal(AlbumIds.HanoiLotusStamp, stamps[0].Id);
        Assert.Equal("Tem Hoa Sen Hà Nội", stamps[0].Name);
        Assert.NotEmpty(stamps[0].DateEarned);
        Assert.Contains("1000 NĂM VĂN HIẾN", stamps[0].PostmarkText);
    }

    [Fact]
    public void CompleteLevel1_UnlocksLongBienGate_AndHueRegion()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        Assert.True(data.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(data.Level1State.QuestFlags.Level1_Finished);
        Assert.True(data.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void CompleteLevel1_TriggersDurableAtomicAutoSave()
    {
        var storage = new MockSaveStorage();
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var saveService = new SaveLoadService(SaveLoadService.DefaultSavePath, storage, bus);
        var progression = new LevelProgressionService(data, bus, saveService);

        progression.CompleteLevel1();

        // Verify save file exists in storage
        Assert.True(storage.FileExists(SaveLoadService.DefaultSavePath));

        // Reload data into fresh instance to verify durability
        var reloaded = saveService.Load();
        Assert.True(reloaded.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(reloaded.Level1State.QuestFlags.Level1_Finished);
        Assert.Contains(AlbumIds.HanoiJournalPage, reloaded.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumIds.HanoiLotusStamp, reloaded.CollectionAlbum.CollectedStamps);
        Assert.True(reloaded.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(reloaded.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void GrayShadowLetterModal_TypewriterAndSkipBehavior()
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();

        Assert.True(modal.IsActive);
        Assert.Equal(0, modal.VisibleCharCount);
        Assert.False(modal.IsTypingComplete);

        // Advance 1 second -> ~35 chars
        modal.Update(1.0f, isConfirmPressed: false, isSkipPressed: false);
        Assert.InRange(modal.VisibleCharCount, 30, 40);
        Assert.False(modal.IsTypingComplete);

        // Press skip -> Immediately reveal full letter
        modal.Update(0.016f, isConfirmPressed: false, isSkipPressed: true);
        Assert.True(modal.IsTypingComplete);
        Assert.Equal(GrayShadowLetterModal.LetterText.Length, modal.VisibleCharCount);

        // Press confirm -> Closes modal
        modal.Update(0.016f, isConfirmPressed: true, isSkipPressed: false);
        Assert.False(modal.IsActive);
        Assert.True(modal.RequestClose);
    }

    [Fact]
    public void LongBienGateInteractable_StatePromptsAndBehavior()
    {
        var gate = new LongBienGateInteractable();
        var data = SaveData.CreateDefault();
        var context = new InteractionContext(
            PlayerPosition: gate.Position,
            Inventory: new Inventory(),
            QuestManager: new QuestManager(),
            QuestFlags: data.Level1State.QuestFlags,
            EventBus: new EventBus(),
            AudioService: new NullAudioService()
        );

        // 1. Locked prompt
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = false;
        Assert.Contains("Bị chặn", gate.GetPromptText(context));

        // 2. Unlocked prompt
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        Assert.Contains("Đi qua Cầu Long Biên", gate.GetPromptText(context));
    }

    [Fact]
    public void CompleteLevel1_IsIdempotent_DoesNotDuplicateAwards()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();
        progression.CompleteLevel1();
        progression.CompleteLevel1();

        Assert.Single(data.CollectionAlbum.CollectedJournalPages);
        Assert.Single(data.CollectionAlbum.CollectedStamps);
        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
    }
}
