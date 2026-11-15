using System;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;

namespace GameProject.Core.Quests;

/// <summary>
/// Domain service coordinating level milestones, regional color bloom waves, and album reward delivery.
/// </summary>
public sealed class LevelProgressionService
{
    private readonly SaveData _saveData;
    private readonly IEventBus? _eventBus;
    private readonly SaveLoadService? _saveLoadService;

    public LevelProgressionService(SaveData saveData, IEventBus? eventBus = null, SaveLoadService? saveLoadService = null)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _eventBus = eventBus;
        _saveLoadService = saveLoadService;
    }

    /// <summary>
    /// Triggered upon Quest 2 resolution: blooms 33.3% regional color to Zone A.
    /// </summary>
    public void OnQuest2Completed()
    {
        float oldColor = _saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi);
        float newColor = 33.3f;
        _saveData.WorldProgression.SetColorRestoration(WorldProgression.RegionHanoi, newColor);
        _saveData.Level1State.QuestFlags.Q2_Completed = true;

        _eventBus?.Publish(new RegionColorUpdatedEvent(WorldProgression.RegionHanoi, newColor));
        _eventBus?.Publish(new ColorRestoredEvent(WorldProgression.RegionHanoi, oldColor, newColor, HanoiQuests.Quest2Id));
    }

    /// <summary>
    /// Triggered upon Quest 3 resolution: completes Hanoi level, unlocks Long Bien gate,
    /// delivers Memory Journal page 1, Lotus Stamp, unlocks Hue region, and triggers Gray Shadow letter cutscene.
    /// </summary>
    public void CompleteLevel1()
    {
        // 1. 100% Color Bloom
        float oldColor = _saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi);
        _saveData.WorldProgression.SetColorRestoration(WorldProgression.RegionHanoi, 100.0f);
        _saveData.WorldProgression.MarkRegionCompleted(WorldProgression.RegionHanoi);

        _eventBus?.Publish(new RegionColorUpdatedEvent(WorldProgression.RegionHanoi, 100.0f));
        _eventBus?.Publish(new ColorRestoredEvent(WorldProgression.RegionHanoi, oldColor, 100.0f, HanoiQuests.Quest3Id));
        _eventBus?.Publish(new RegionCompletedEvent(WorldProgression.RegionHanoi));

        // 2. Gate Unlock
        _saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        _saveData.Level1State.QuestFlags.Q3_Completed = true;
        _eventBus?.Publish(new GateUnlockedEvent("Gate_LongBien", WorldProgression.RegionHanoi));

        // 3. Award Memory Journal Page #1
        _saveData.CollectionAlbum.AddJournalPage(AlbumIds.HanoiJournalPage);
        _eventBus?.Publish(new JournalPageAwardedEvent(AlbumIds.HanoiJournalPage, "Hương Vị Phố Cổ & Nhịp Cầu Thời Gian", WorldProgression.RegionHanoi));

        // 4. Award Lotus Stamp #1
        _saveData.CollectionAlbum.AddStamp(AlbumIds.HanoiLotusStamp);
        _eventBus?.Publish(new StampAwardedEvent(AlbumIds.HanoiLotusStamp, "Tem Hoa Sen Hà Nội", WorldProgression.RegionHanoi));

        // 5. Unlock Hue Region
        _saveData.WorldProgression.UnlockRegion(WorldProgression.RegionHue);
        _eventBus?.Publish(new RegionUnlockedEvent(WorldProgression.RegionHue));

        // 6. Trigger Gray Shadow letter narrative
        _saveData.Level1State.QuestFlags.Level1_Finished = true;
        _eventBus?.Publish(new CutsceneTriggeredEvent("GrayShadow_Letter"));

        // 7. Atomic Persistent Auto-Save
        _saveLoadService?.Save(_saveData);
    }

    /// <summary>
    /// Triggered upon Hue Quest 2 resolution: blooms 33.3% regional color to Hue.
    /// </summary>
    public void OnHueQuest2Completed()
    {
        float oldColor = _saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue);
        float newColor = 33.3f;
        _saveData.WorldProgression.SetColorRestoration(WorldProgression.RegionHue, newColor);
        _saveData.QuestFlags.Hue_Q2_Completed = true;

        _eventBus?.Publish(new RegionColorUpdatedEvent(WorldProgression.RegionHue, newColor));
        _eventBus?.Publish(new ColorRestoredEvent(WorldProgression.RegionHue, oldColor, newColor, HueQuests.Quest2Id));
    }

    /// <summary>
    /// Triggered upon Hue Quest 3 / Climax resolution: completes Hue level, unlocks Hoi An gate,
    /// delivers Memory Journal page 2, Thien Mu Stamp, unlocks Hoi An region, and auto-saves.
    /// </summary>
    public void CompleteHue()
    {
        // 1. 100% Color Bloom
        float oldColor = _saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue);
        _saveData.WorldProgression.SetColorRestoration(WorldProgression.RegionHue, 100.0f);
        _saveData.WorldProgression.MarkRegionCompleted(WorldProgression.RegionHue);

        _eventBus?.Publish(new RegionColorUpdatedEvent(WorldProgression.RegionHue, 100.0f));
        _eventBus?.Publish(new ColorRestoredEvent(WorldProgression.RegionHue, oldColor, 100.0f, HueQuests.Quest3Id));
        _eventBus?.Publish(new RegionCompletedEvent(WorldProgression.RegionHue));

        // 2. Gate Unlock
        _saveData.QuestFlags.Gate_HoiAn_Unlocked = true;
        _saveData.QuestFlags.Hue_Q3_Completed = true;
        _saveData.QuestFlags.Hue_Finished = true;
        _eventBus?.Publish(new GateUnlockedEvent("Gate_HoiAn", WorldProgression.RegionHue));

        // 3. Award Memory Journal Page #2
        _saveData.CollectionAlbum.AddJournalPage(Album.AlbumService.HuePage2.Id);
        _eventBus?.Publish(new JournalPageAwardedEvent(Album.AlbumService.HuePage2.Id, Album.AlbumService.HuePage2.Title, WorldProgression.RegionHue));

        // 4. Award Hue Stamp
        _saveData.CollectionAlbum.AddStamp(Album.AlbumService.HueStamp.Id);
        _eventBus?.Publish(new StampAwardedEvent(Album.AlbumService.HueStamp.Id, Album.AlbumService.HueStamp.Name, WorldProgression.RegionHue));

        // 5. Unlock Hoi An Region
        _saveData.WorldProgression.UnlockRegion(WorldProgression.RegionHoiAn);
        _eventBus?.Publish(new RegionUnlockedEvent(WorldProgression.RegionHoiAn));

        // 6. Atomic Persistent Auto-Save
        _saveLoadService?.Save(_saveData);
    }
}
