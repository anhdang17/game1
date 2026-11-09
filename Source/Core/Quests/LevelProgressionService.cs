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
}
