using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Quests;
using Xunit;

namespace GameProject.Tests;

public class HueChapter2Tests
{
    #region Dialogue Tests

    [Fact]
    public void DialogueService_LoadDialogueFile_LoadsHueDialogues()
    {
        var service = new DialogueService();
        service.LoadDialogueFile("Data/dialogues_hue.json");

        Assert.True(service.LoadedGraphs.Count >= 14, $"Expected at least 14 dialogue graphs, found {service.LoadedGraphs.Count}");
        Assert.NotNull(service.StartDialogue("Dialogue_Hue_Intro"));
        Assert.NotNull(service.StartDialogue("Dialogue_MeHue_Intro"));
        Assert.NotNull(service.StartDialogue("Dialogue_MeHue_InProgress"));
        Assert.NotNull(service.StartDialogue("Dialogue_MeHue_TurnIn"));
        Assert.NotNull(service.StartDialogue("Dialogue_MeHue_PostCook"));

        // 5 Vendors
        Assert.NotNull(service.StartDialogue("Dialogue_Stall_BunSoiTo"));
        Assert.NotNull(service.StartDialogue("Dialogue_Stall_BapBo"));
        Assert.NotNull(service.StartDialogue("Dialogue_Stall_ChaCua"));
        Assert.NotNull(service.StartDialogue("Dialogue_Stall_NuocDungHue"));
        Assert.NotNull(service.StartDialogue("Dialogue_Stall_RauSongHue"));

        // River & Climax
        Assert.NotNull(service.StartDialogue("Dialogue_Npc_SongHuong"));
        Assert.NotNull(service.StartDialogue("Dialogue_SongHuong_PostPuzzle"));
        Assert.NotNull(service.StartDialogue("Dialogue_Gate_HoiAn"));

        // PoIs
        Assert.NotNull(service.StartDialogue("Dialogue_PoI_NgoMon"));
        Assert.NotNull(service.StartDialogue("Dialogue_PoI_ThienMu"));
        Assert.NotNull(service.StartDialogue("Dialogue_PoI_TrangTien"));
    }

    [Fact]
    public void DialogueService_HueMeHueIntro_TriggersQuest1Action()
    {
        var service = new DialogueService();
        service.LoadDialogueFile("Data/dialogues_hue.json");

        string? triggeredAction = null;
        service.OnActionTriggered += action => triggeredAction = action;

        var startNode = service.StartDialogue("Dialogue_MeHue_Intro");
        Assert.NotNull(startNode);
        Assert.Equal("Mệ Bích", startNode.Speaker);

        var nextNode = service.AdvanceToNext();
        Assert.NotNull(nextNode);
        Assert.Equal("Bé Mây", nextNode.Speaker);
        Assert.Equal(2, nextNode.Branches.Count);

        service.SelectChoice(0); // Choice_Accept
        Assert.Equal("Start_Hue_Quest_1", triggeredAction);
        Assert.Equal("Node_MeHue_Accept", service.CurrentNode?.Id);

        // Advance to end dialogue
        service.AdvanceToNext();
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void DialogueService_HueGateHoiAn_EvaluatesFlagCondition()
    {
        var service = new DialogueService();
        service.LoadDialogueFile("Data/dialogues_hue.json");

        var flags = new QuestFlags { Gate_HoiAn_Unlocked = false };
        var startNode = service.StartDialogue("Dialogue_Gate_HoiAn");
        Assert.NotNull(startNode);

        var availableBranches = service.GetAvailableBranches(startNode, flags);
        Assert.Single(availableBranches);
        Assert.Equal("Choice_Inspect", availableBranches[0].ChoiceId);

        // Unlock gate
        flags.Gate_HoiAn_Unlocked = true;
        availableBranches = service.GetAvailableBranches(startNode, flags);
        Assert.Equal(2, availableBranches.Count);
        Assert.Contains(availableBranches, b => b.ChoiceId == "Choice_Cross");

        // Take Choice_Cross
        string? action = null;
        service.OnActionTriggered += a => action = a;
        service.SelectChoice(1);
        Assert.Equal("Open_World_Map", action);
    }

    #endregion

    #region QuestFlags Tests

    [Fact]
    public void QuestFlags_HueProperties_GetAndSetViaSwitch()
    {
        var flags = new QuestFlags();

        // Initial default is false
        Assert.False(flags.Hue_Q1_Started);
        Assert.False(flags.GetFlag("Hue_Q1_Started"));

        flags.SetFlag("Hue_Q1_Started", true);
        Assert.True(flags.Hue_Q1_Started);
        Assert.True(flags.GetFlag("Hue_Q1_Started"));

        flags.SetFlag("Hue_Q1_BunSoiTo_Collected", true);
        flags.SetFlag("Hue_Q1_BapBo_Collected", true);
        flags.SetFlag("Hue_Q1_ChaCua_Collected", true);
        flags.SetFlag("Hue_Q1_NuocDungHue_Collected", true);
        flags.SetFlag("Hue_Q1_RauSongHue_Collected", true);

        Assert.Equal(5, flags.GetHueQ1CollectedCount());
        Assert.True(flags.AreAllHueQ1IngredientsCollected());

        flags.SetFlag("Hue_Q1_Completed", true);
        flags.SetFlag("Hue_Q2_Started", true);
        flags.SetFlag("Hue_Q2_BunBoCooked", true);
        flags.SetFlag("Hue_Q2_Completed", true);
        flags.SetFlag("Hue_Q3_Started", true);
        flags.SetFlag("Hue_Q3_PuzzleSolved", true);
        flags.SetFlag("Hue_Q3_Completed", true);
        flags.SetFlag("Gate_HoiAn_Unlocked", true);
        flags.SetFlag("Hue_Finished", true);

        Assert.True(flags.GetFlag("Hue_Q1_Completed"));
        Assert.True(flags.GetFlag("Hue_Q2_Started"));
        Assert.True(flags.GetFlag("Hue_Q2_BunBoCooked"));
        Assert.True(flags.GetFlag("Hue_Q2_Completed"));
        Assert.True(flags.GetFlag("Hue_Q3_Started"));
        Assert.True(flags.GetFlag("Hue_Q3_PuzzleSolved"));
        Assert.True(flags.GetFlag("Hue_Q3_Completed"));
        Assert.True(flags.GetFlag("Gate_HoiAn_Unlocked"));
        Assert.True(flags.GetFlag("Hue_Finished"));
    }

    #endregion

    #region Inventory Tests

    [Fact]
    public void Inventory_HueItems_AdditionAndQueries()
    {
        var inventory = new Inventory();

        Assert.True(inventory.AddItem(ItemIds.BunSoiTo));
        Assert.True(inventory.AddItem(ItemIds.BapBo));
        Assert.True(inventory.AddItem(ItemIds.ChaCua));
        Assert.True(inventory.AddItem(ItemIds.NuocDungHue));
        Assert.True(inventory.AddItem(ItemIds.RauSongHue));
        Assert.True(inventory.AddItem(ItemIds.HoaDang));
        Assert.True(inventory.AddItem(ItemIds.NonBaiTho));

        // Duplicate rejection
        Assert.False(inventory.AddItem(ItemIds.BunSoiTo));

        Assert.Equal(7, inventory.Count);
        Assert.True(inventory.HasItem(ItemIds.BunSoiTo));
        Assert.True(inventory.HasItem(ItemIds.BapBo));
        Assert.True(inventory.HasItem(ItemIds.ChaCua));
        Assert.True(inventory.HasItem(ItemIds.NuocDungHue));
        Assert.True(inventory.HasItem(ItemIds.RauSongHue));
        Assert.True(inventory.HasItem(ItemIds.HoaDang));
        Assert.True(inventory.HasItem(ItemIds.NonBaiTho));

        foreach (var ingredient in ItemIds.AllBunBoIngredients)
        {
            Assert.True(inventory.HasItem(ingredient));
        }
    }

    #endregion

    #region Quest Registration & Lifecycle Tests

    [Fact]
    public void HueQuests_RegisterAll_RegistersThreeQuests()
    {
        var manager = new QuestManager();
        HueQuests.RegisterAll(manager);

        var q1 = manager.GetQuest(HueQuests.Quest1Id);
        var q2 = manager.GetQuest(HueQuests.Quest2Id);
        var q3 = manager.GetQuest(HueQuests.Quest3Id);

        Assert.NotNull(q1);
        Assert.Equal(WorldProgression.RegionHue, q1.RegionId);
        Assert.Equal(5, q1.Objectives.Count);

        Assert.NotNull(q2);
        Assert.Equal(WorldProgression.RegionHue, q2.RegionId);
        Assert.Contains(HueQuests.Quest1Id, q2.PrerequisiteQuestIds);

        Assert.NotNull(q3);
        Assert.Equal(WorldProgression.RegionHue, q3.RegionId);
        Assert.Contains(HueQuests.Quest2Id, q3.PrerequisiteQuestIds);
    }

    [Fact]
    public void HueQuests_Lifecycle_CompleteThroughToClimax()
    {
        var manager = new QuestManager();
        HueQuests.RegisterAll(manager);

        Assert.True(manager.StartQuest(HueQuests.Quest1Id));
        Assert.Equal(QuestState.InProgress, manager.GetQuestState(HueQuests.Quest1Id));

        // Advance 5 gathering objectives
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.BunSoiTo, 1));
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.BapBo, 1));
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.ChaCua, 1));
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.NuocDungHue, 1));
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.RauSongHue, 1));

        var q1 = manager.GetQuest(HueQuests.Quest1Id);
        Assert.NotNull(q1);
        Assert.True(q1.AreAllMandatoryObjectivesComplete);
        Assert.True(manager.CompleteQuest(HueQuests.Quest1Id));

        // Start Q2
        Assert.True(manager.StartQuest(HueQuests.Quest2Id));
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest2Id, "cook_bun_bo", 1));
        Assert.True(manager.CompleteQuest(HueQuests.Quest2Id));

        // Start Q3
        Assert.True(manager.StartQuest(HueQuests.Quest3Id));
        Assert.True(manager.RecordObjectiveProgress(HueQuests.Quest3Id, "release_hoa_dang", 1));
        Assert.True(manager.CompleteQuest(HueQuests.Quest3Id));

        Assert.Equal(QuestState.Completed, manager.GetQuestState(HueQuests.Quest3Id));
    }

    #endregion

    #region WorldProgression Tests

    [Fact]
    public void WorldProgression_HueCompletion_UnlocksHoiAn()
    {
        var progression = WorldProgression.CreateDefault();

        Assert.True(progression.IsRegionUnlocked(WorldProgression.RegionHanoi));
        Assert.False(progression.IsRegionUnlocked(WorldProgression.RegionHue));
        Assert.False(progression.IsRegionUnlocked(WorldProgression.RegionHoiAn));

        // Complete Hanoi -> unlocks Hue
        progression.MarkRegionCompleted(WorldProgression.RegionHanoi);
        Assert.True(progression.IsRegionUnlocked(WorldProgression.RegionHue));
        Assert.False(progression.IsRegionUnlocked(WorldProgression.RegionHoiAn));

        // Complete Hue -> unlocks Hoi An
        progression.MarkRegionCompleted(WorldProgression.RegionHue);
        Assert.True(progression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
    }

    [Fact]
    public void WorldProgression_Sanitize_HealsHoiAnWhenHueCompleted()
    {
        var progression = new WorldProgression
        {
            CompletedRegions = new List<string> { WorldProgression.RegionHanoi, WorldProgression.RegionHue },
            UnlockedRegions = new List<string> { WorldProgression.RegionHanoi }
        };

        progression.Sanitize();

        Assert.Contains(WorldProgression.RegionHue, progression.UnlockedRegions);
        Assert.Contains(WorldProgression.RegionHoiAn, progression.UnlockedRegions);
    }

    #endregion

    #region SaveData Tests

    [Fact]
    public void SaveData_Sanitize_UnlocksHoiAnWhenHueFinishedOrCompleted()
    {
        var saveData = SaveData.CreateDefault();
        saveData.QuestFlags.Hue_Finished = true;

        saveData.Sanitize();

        Assert.True(saveData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
    }

    [Fact]
    public void SaveData_SerializationRoundTrip_PreservesHueFlags()
    {
        var original = SaveData.CreateDefault();
        original.QuestFlags.Hue_Q1_Started = true;
        original.QuestFlags.Hue_Q1_Completed = true;
        original.QuestFlags.Hue_Q2_BunBoCooked = true;
        original.QuestFlags.Gate_HoiAn_Unlocked = true;
        original.QuestFlags.Hue_Finished = true;
        original.CollectionAlbum.AddJournalPage(AlbumService.HuePage2.Id);
        original.CollectionAlbum.AddStamp(AlbumService.HueStamp.Id);
        original.CollectionAlbum.AddFactCard(AlbumService.FactCardHueBunBo);

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SaveData>(json);

        Assert.NotNull(restored);
        Assert.True(restored.QuestFlags.Hue_Q1_Started);
        Assert.True(restored.QuestFlags.Hue_Q1_Completed);
        Assert.True(restored.QuestFlags.Hue_Q2_BunBoCooked);
        Assert.True(restored.QuestFlags.Gate_HoiAn_Unlocked);
        Assert.True(restored.QuestFlags.Hue_Finished);
        Assert.Contains(AlbumService.HuePage2.Id, restored.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, restored.CollectionAlbum.CollectedStamps);
        Assert.Contains(AlbumService.FactCardHueBunBo, restored.CollectionAlbum.UnlockedFactCards);
    }

    #endregion

    #region AlbumService Tests

    [Fact]
    public void AlbumService_HueFactCards_RegisteredAndUnlockable()
    {
        var service = new AlbumService();
        var album = new CollectionAlbum();

        Assert.False(service.IsFactCardUnlocked(album, AlbumService.FactCardHueBunBo));
        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueBunBo));
        Assert.True(service.IsFactCardUnlocked(album, AlbumService.FactCardHueBunBo));
        Assert.True(service.IsFactCardUnlocked(album, "FactCard_BunBoHue"));

        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueNgoMon));
        Assert.True(service.IsFactCardUnlocked(album, AlbumService.FactCardHueNgoMon));

        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueSongHuong));
        Assert.True(service.IsFactCardUnlocked(album, AlbumService.FactCardHueSongHuong));

        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueThienMu));
        Assert.True(service.IsFactCardUnlocked(album, AlbumService.FactCardHueThienMu));

        var unlockedCards = service.GetUnlockedFactCards(album);
        Assert.Contains(unlockedCards, c => c.Id == AlbumService.FactCardHueBunBo && c.Title == "Hương Vị Bún Bò Cố Đô");
        Assert.Contains(unlockedCards, c => c.Id == AlbumService.FactCardHueNgoMon && c.Title == "Ngọ Môn & Hoàng Thành Huế");
        Assert.Contains(unlockedCards, c => c.Id == AlbumService.FactCardHueSongHuong && c.Title == "Dòng Sông Hương & Cầu Tràng Tiền");
        Assert.Contains(unlockedCards, c => c.Id == AlbumService.FactCardHueThienMu && c.Title == "Chùa Thiên Mụ & Tháp Phước Duyên");
    }

    [Fact]
    public void AlbumService_HuePageAndStamp_Registered()
    {
        var service = new AlbumService();
        var pages = service.GetAllJournalPages();
        var stamps = service.GetAllRegisteredStamps();

        Assert.Contains(pages, p => p.Id == AlbumService.HuePage2.Id);
        Assert.Contains(stamps, s => s.Id == AlbumService.HueStamp.Id);
    }

    #endregion

    #region LevelProgressionService Tests

    [Fact]
    public void LevelProgressionService_OnHueQuest2Completed_BloomsColorAndPublishesEvents()
    {
        var saveData = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var progressionService = new LevelProgressionService(saveData, eventBus);

        RegionColorUpdatedEvent? colorUpdated = null;
        ColorRestoredEvent? colorRestored = null;
        eventBus.Subscribe<RegionColorUpdatedEvent>(e => colorUpdated = e);
        eventBus.Subscribe<ColorRestoredEvent>(e => colorRestored = e);

        progressionService.OnHueQuest2Completed();

        Assert.True(saveData.QuestFlags.Hue_Q2_Completed);
        Assert.Equal(33.3f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));

        Assert.NotNull(colorUpdated);
        Assert.Equal(WorldProgression.RegionHue, colorUpdated.RegionId);
        Assert.True(Math.Abs(colorUpdated.NewPercentage - 33.3f) < 0.1f);

        Assert.NotNull(colorRestored);
        Assert.Equal(WorldProgression.RegionHue, colorRestored.RegionId);
        Assert.Equal(HueQuests.Quest2Id, colorRestored.SourceTrigger);
    }

    [Fact]
    public void LevelProgressionService_CompleteHue_FullClimaxProgression()
    {
        var saveData = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var progressionService = new LevelProgressionService(saveData, eventBus);

        RegionColorUpdatedEvent? colorUpdated = null;
        ColorRestoredEvent? colorRestored = null;
        RegionCompletedEvent? regionCompleted = null;
        GateUnlockedEvent? gateUnlocked = null;
        JournalPageAwardedEvent? journalAwarded = null;
        StampAwardedEvent? stampAwarded = null;
        RegionUnlockedEvent? regionUnlocked = null;

        eventBus.Subscribe<RegionColorUpdatedEvent>(e => colorUpdated = e);
        eventBus.Subscribe<ColorRestoredEvent>(e => colorRestored = e);
        eventBus.Subscribe<RegionCompletedEvent>(e => regionCompleted = e);
        eventBus.Subscribe<GateUnlockedEvent>(e => gateUnlocked = e);
        eventBus.Subscribe<JournalPageAwardedEvent>(e => journalAwarded = e);
        eventBus.Subscribe<StampAwardedEvent>(e => stampAwarded = e);
        eventBus.Subscribe<RegionUnlockedEvent>(e => regionUnlocked = e);

        progressionService.CompleteHue();

        // 1. 100% color & Region completion
        Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
        Assert.True(saveData.WorldProgression.IsRegionCompleted(WorldProgression.RegionHue));

        // 2. Flags
        Assert.True(saveData.QuestFlags.Gate_HoiAn_Unlocked);
        Assert.True(saveData.QuestFlags.Hue_Q3_Completed);
        Assert.True(saveData.QuestFlags.Hue_Finished);

        // 3. Rewards
        Assert.Contains(AlbumService.HuePage2.Id, saveData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, saveData.CollectionAlbum.CollectedStamps);

        // 4. Hoi An unlock
        Assert.True(saveData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));

        // 5. Events published
        Assert.NotNull(colorUpdated);
        Assert.Equal(WorldProgression.RegionHue, colorUpdated.RegionId);
        Assert.Equal(100.0f, colorUpdated.NewPercentage);

        Assert.NotNull(colorRestored);
        Assert.Equal(WorldProgression.RegionHue, colorRestored.RegionId);
        Assert.Equal(HueQuests.Quest3Id, colorRestored.SourceTrigger);

        Assert.NotNull(regionCompleted);
        Assert.Equal(WorldProgression.RegionHue, regionCompleted.RegionId);

        Assert.NotNull(gateUnlocked);
        Assert.Equal("Gate_HoiAn", gateUnlocked.GateId);

        Assert.NotNull(journalAwarded);
        Assert.Equal(AlbumService.HuePage2.Id, journalAwarded.PageId);

        Assert.NotNull(stampAwarded);
        Assert.Equal(AlbumService.HueStamp.Id, stampAwarded.StampId);

        Assert.NotNull(regionUnlocked);
        Assert.Equal(WorldProgression.RegionHoiAn, regionUnlocked.RegionId);
    }

    #endregion
}
