using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using GameProject.Audio;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.State;
using GameProject.Stages;
using GameProject.Tests.Mocks;
using GameProject.UI;
using Xunit;

namespace GameProject.Tests;

/// <summary>
/// Adversarial Empirical Stress Test Suite for Milestone 3 Challenger 2:
/// 1. Progression Climax & LevelProgressionService Idempotency (Duplication prevention, event spamming, multithreading)
/// 2. Persistence Durability & Re-hydration (Corrupted JSON, atomic overwrite, cross-instance rehydration, backup rotation)
/// 3. Long Biên Gate Interaction States (Pre-Q3 blocking, mid-Q3 clearance prompt, post-Q3 transition to Hue/Letter)
/// 4. GrayShadowLetterModal Typewriter & Skip Dynamics (Key spamming, clamping, negative/massive dt, layout boundaries)
/// </summary>
public class ChallengerM3ProgressionStressTests
{
    // =========================================================================
    // 1. PROGRESSION CLIMAX & LEVEL PROGRESSION SERVICE IDEMPOTENCY
    // =========================================================================

    [Fact]
    public void CompleteLevel1_RepeatedCalls_PreventAlbumDuplication()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        // Invoke 50 times in succession
        for (int i = 0; i < 50; i++)
        {
            progression.CompleteLevel1();
        }

        // Must strictly contain exactly 1 journal page and 1 travel stamp
        Assert.Single(data.CollectionAlbum.CollectedJournalPages);
        Assert.Single(data.CollectionAlbum.CollectedStamps);
        Assert.Equal(AlbumIds.HanoiJournalPage, data.CollectionAlbum.CollectedJournalPages[0]);
        Assert.Equal(AlbumIds.HanoiLotusStamp, data.CollectionAlbum.CollectedStamps[0]);

        // Flags must remain true
        Assert.True(data.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(data.Level1State.QuestFlags.Level1_Finished);
        Assert.True(data.Level1State.QuestFlags.Q3_Completed);
        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(data.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void CompleteLevel1_EventDispatching_TracksExactPublicationCount()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();

        int colorUpdatedCount = 0;
        int colorRestoredCount = 0;
        int regionCompletedCount = 0;
        int gateUnlockedCount = 0;
        int journalPageCount = 0;
        int stampAwardedCount = 0;
        int regionUnlockedCount = 0;
        int cutsceneTriggeredCount = 0;

        bus.Subscribe<RegionColorUpdatedEvent>(_ => colorUpdatedCount++);
        bus.Subscribe<ColorRestoredEvent>(_ => colorRestoredCount++);
        bus.Subscribe<RegionCompletedEvent>(_ => regionCompletedCount++);
        bus.Subscribe<GateUnlockedEvent>(_ => gateUnlockedCount++);
        bus.Subscribe<JournalPageAwardedEvent>(_ => journalPageCount++);
        bus.Subscribe<StampAwardedEvent>(_ => stampAwardedCount++);
        bus.Subscribe<RegionUnlockedEvent>(_ => regionUnlockedCount++);
        bus.Subscribe<CutsceneTriggeredEvent>(_ => cutsceneTriggeredCount++);

        var progression = new LevelProgressionService(data, bus);

        int callCount = 5;
        for (int i = 0; i < callCount; i++)
        {
            progression.CompleteLevel1();
        }

        // Verify each event is emitted exactly callCount times per LevelProgressionService design
        Assert.Equal(callCount, colorUpdatedCount);
        Assert.Equal(callCount, colorRestoredCount);
        Assert.Equal(callCount, regionCompletedCount);
        Assert.Equal(callCount, gateUnlockedCount);
        Assert.Equal(callCount, journalPageCount);
        Assert.Equal(callCount, stampAwardedCount);
        Assert.Equal(callCount, regionUnlockedCount);
        Assert.Equal(callCount, cutsceneTriggeredCount);
    }

    [Fact]
    public void CompleteLevel1_PrePopulatedAlbum_DoesNotThrowOrDuplicate()
    {
        var data = SaveData.CreateDefault();
        // Manually inject items beforehand
        data.CollectionAlbum.AddJournalPage(AlbumIds.HanoiJournalPage);
        data.CollectionAlbum.AddStamp(AlbumIds.HanoiLotusStamp);

        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        Assert.Single(data.CollectionAlbum.CollectedJournalPages);
        Assert.Single(data.CollectionAlbum.CollectedStamps);
    }

    [Fact]
    public async Task CompleteLevel1_ConcurrentInvocations_MaintainsDataIntegrity()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        // Run 20 tasks concurrently invoking CompleteLevel1
        var tasks = new Task[20];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => progression.CompleteLevel1());
        }
        await Task.WhenAll(tasks);

        Assert.Single(data.CollectionAlbum.CollectedJournalPages);
        Assert.Single(data.CollectionAlbum.CollectedStamps);
        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(data.Level1State.QuestFlags.Gate_LongBien_Unlocked);
    }

    // =========================================================================
    // 2. PERSISTENCE DURABILITY & CORRUPTION RECOVERY
    // =========================================================================

    [Fact]
    public void Persistence_AtomicSaveWithRealDisk_ProducesBackupAndCleanState()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Game1_Persistence_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string savePath = Path.Combine(tempDir, "savegame.json");

        try
        {
            var saveService = new SaveLoadService(savePath);
            var initialData = SaveData.CreateDefault();
            initialData.PlayerProfile.PlayerName = "Bé Mây Hà Nội";
            initialData.Level1State.QuestFlags.Q1_Completed = true;

            // 1. First Save
            bool firstSaveOk = saveService.Save(initialData);
            Assert.True(firstSaveOk);
            Assert.True(File.Exists(savePath));
            string firstContent = File.ReadAllText(savePath);
            Assert.Contains("Bé Mây Hà Nội", firstContent);

            // 2. Mutate state & Second Save (must trigger atomic replacement and create .bak)
            var climaxData = SaveData.CreateDefault();
            climaxData.PlayerProfile.PlayerName = "Bé Mây Chiến Thắng";
            var progression = new LevelProgressionService(climaxData, null, saveService);
            progression.CompleteLevel1();

            Assert.True(File.Exists(savePath));
            string secondContent = File.ReadAllText(savePath);
            Assert.Contains("Bé Mây Chiến Thắng", secondContent);
            Assert.Contains(AlbumIds.HanoiJournalPage, secondContent);
            Assert.Contains(AlbumIds.HanoiLotusStamp, secondContent);

            // Verify .bak file created containing previous save content
            string backupPath = savePath + SaveLoadService.BackupSuffix;
            Assert.True(File.Exists(backupPath));
            string backupContent = File.ReadAllText(backupPath);
            Assert.Contains("Bé Mây Hà Nội", backupContent);

            // Verify no stray .tmp file remains
            string tmpPath = savePath + SaveLoadService.TempSuffix;
            Assert.False(File.Exists(tmpPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("")]                                          // Empty file
    [InlineData("   \t \r\n   ")]                             // Whitespace only
    [InlineData("null")]                                      // JSON literal null
    [InlineData("{ \"SaveVersion\": \"1.0.0\", \"PlayerPro")]  // Truncated syntax
    [InlineData("<<<NOT_VALID_JSON_AT_ALL>>>")]               // Garbage payload
    [InlineData("{ \"Level1State\": \"invalid_type\" }")]     // Schema mismatch
    public void Persistence_CorruptedSaveFile_RecoversToCleanDefaultAndQuarantines(string corruptJson)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Game1_Corruption_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string savePath = Path.Combine(tempDir, "savegame.json");

        try
        {
            File.WriteAllText(savePath, corruptJson);
            Assert.True(File.Exists(savePath));

            var bus = new EventBus();
            bool loadedEventFired = false;
            bool wasRecoveredFlag = false;
            bus.Subscribe<GameLoadedEvent>(e =>
            {
                loadedEventFired = true;
                wasRecoveredFlag = e.WasCorruptedAndRecovered;
            });

            var service = new SaveLoadService(savePath, null, bus);
            SaveData loaded = service.Load();

            // Must return valid default state without throwing
            Assert.NotNull(loaded);
            Assert.Equal(SaveData.CurrentSaveVersion, loaded.SaveVersion);
            Assert.True(loadedEventFired);
            Assert.True(wasRecoveredFlag);

            // Corrupt file must be backed up with .corrupt suffix
            string genericCorruptBackup = savePath + SaveLoadService.CorruptSuffix;
            Assert.True(File.Exists(genericCorruptBackup));

            // The main save file should have been re-written with healthy default state
            Assert.True(File.Exists(savePath));
            string recoveredJson = File.ReadAllText(savePath);
            Assert.Contains(SaveData.CurrentSaveVersion, recoveredJson);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Persistence_CrossInstanceRehydration_PreservesVietnameseDiacriticsAndClimaxState()
    {
        var storage = new GameProject.Tests.Mocks.MockSaveStorage();
        var bus = new EventBus();
        string path = "Data/test_climax_save.json";

        // Instance 1: Complete level and save
        var service1 = new SaveLoadService(path, storage, bus);
        var data1 = SaveData.CreateDefault();
        data1.PlayerProfile.PlayerName = "Bé Mây Cầu Long Biên 1000 Năm Văn Hiến";
        var progression1 = new LevelProgressionService(data1, bus, service1);
        progression1.CompleteLevel1();

        Assert.True(storage.FileExists(path));

        // Instance 2: Brand new service, fresh rehydration from storage
        var service2 = new SaveLoadService(path, storage, bus);
        SaveData reloaded = service2.Load();

        Assert.Equal("Bé Mây Cầu Long Biên 1000 Năm Văn Hiến", reloaded.PlayerProfile.PlayerName);
        Assert.True(reloaded.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(reloaded.Level1State.QuestFlags.Level1_Finished);
        Assert.True(reloaded.Level1State.QuestFlags.Q3_Completed);
        Assert.Equal(100.0f, reloaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(reloaded.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(reloaded.WorldProgression.IsRegionUnlocked(RegionIds.Hue));

        // Rich album verification
        var albumService = new AlbumService();
        var pages = albumService.GetUnlockedJournalPages(reloaded.CollectionAlbum);
        Assert.Single(pages);
        Assert.Equal("Hương Vị Phố Cổ & Nhịp Cầu Thời Gian", pages[0].Title);

        var stamps = albumService.GetUnlockedTravelStamps(reloaded.CollectionAlbum);
        Assert.Single(stamps);
        Assert.Equal("Tem Hoa Sen Hà Nội", stamps[0].Name);
        Assert.Equal("HÀ NỘI - 1000 NĂM VĂN HIẾN", stamps[0].PostmarkText);
    }

    // =========================================================================
    // 3. LONG BIÊN GATE INTERACTION STATES
    // =========================================================================

    [Fact]
    public void LongBienGate_PreQ3States_BlockPassageAndProvideGuidance()
    {
        var gate = new LongBienGateInteractable();
        var data = SaveData.CreateDefault();
        var context = new InteractionContext(
            PlayerPosition: gate.Position,
            Inventory: new Inventory(),
            QuestManager: new QuestManager(),
            QuestFlags: data.Level1State.QuestFlags,
            EventBus: new EventBus(),
            AudioService: new GameProject.Tests.Mocks.NullAudioService()
        );

        // State A: Quest 1 & 2 not completed
        data.Level1State.QuestFlags.Q1_Completed = false;
        data.Level1State.QuestFlags.Q2_Completed = false;
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = false;

        string promptA = gate.GetPromptText(context);
        Assert.Contains("Bị chặn bởi sạp hàng", promptA);

        var resultA = gate.Interact(context);
        Assert.True(resultA.Success);
        Assert.Equal("Bút Chì", resultA.DialogueSpeaker);
        Assert.Contains("nấu phở trước đã", resultA.DialogueText);
        Assert.False(resultA.QuestCompleted);

        // State B: Quest 2 completed, Quest 3 stall puzzle pending
        data.Level1State.QuestFlags.Q1_Completed = true;
        data.Level1State.QuestFlags.Q2_Completed = true;
        data.Level1State.QuestFlags.Q3_Completed = false;
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = false;

        string promptB = gate.GetPromptText(context);
        Assert.Equal("[E] Dọn dẹp sạp hàng mở lối", promptB);

        var resultB = gate.Interact(context);
        Assert.True(resultB.Success);
        Assert.Equal("Bút Chì", resultB.DialogueSpeaker);
        Assert.Contains("sắp xếp lại đường đi", resultB.DialogueText);
    }

    [Fact]
    public void LongBienGate_PostQ3UnlockedState_OffersWorldMapTransition()
    {
        var gate = new LongBienGateInteractable();
        var data = SaveData.CreateDefault();
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        data.Level1State.QuestFlags.Q3_Completed = true;

        var context = new InteractionContext(
            PlayerPosition: gate.Position,
            Inventory: new Inventory(),
            QuestManager: new QuestManager(),
            QuestFlags: data.Level1State.QuestFlags,
            EventBus: new EventBus(),
            AudioService: new GameProject.Tests.Mocks.NullAudioService()
        );

        string prompt = gate.GetPromptText(context);
        Assert.Equal("[E] Đi qua Cầu Long Biên (Bản Đồ Thế Giới)", prompt);

        var result = gate.Interact(context);
        Assert.True(result.Success);
        Assert.Contains("Cố Đô Huế", result.DialogueText);
    }

    [Fact]
    public void LongBienGate_CollisionWorldBarrier_TogglesBasedOnGateUnlock()
    {
        var bus = new EventBus();
        var audio = new GameProject.Tests.Mocks.NullAudioService();
        var input = new GameProject.Core.Ports.MockInputService();
        var scene = new HanoiExplorationScene(audioService: audio, inputService: input, eventBus: bus);

        // 1. Initially gate is locked -> barrier obstacle must be present
        Assert.False(scene.QuestFlags.Gate_LongBien_Unlocked);
        // Position at (1480, 400) should be colliding with gate barrier
        var pointInsideBarrier = new AABB(1475f, 395f, 1485f, 405f);
        Assert.True(scene.CollisionWorld.CheckCollision(pointInsideBarrier, out _));

        // 2. Unlock gate & refresh obstacles via Enter()
        scene.QuestFlags.Gate_LongBien_Unlocked = true;
        scene.Enter();

        // 3. Barrier obstacle is removed
        Assert.False(scene.CollisionWorld.CheckCollision(pointInsideBarrier, out _));
    }

    // =========================================================================
    // 4. GRAY SHADOW LETTER MODAL TYPEWRITER & SKIP DYNAMICS
    // =========================================================================

    [Fact]
    public void GrayShadowLetterModal_InitialState_IsClean()
    {
        var modal = new GrayShadowLetterModal();
        Assert.False(modal.IsActive);
        Assert.Equal(0, modal.VisibleCharCount);
        Assert.False(modal.IsTypingComplete);
        Assert.False(modal.RequestClose);

        modal.Show();
        Assert.True(modal.IsActive);
        Assert.Equal(0, modal.VisibleCharCount);
        Assert.False(modal.IsTypingComplete);
        Assert.False(modal.RequestClose);
    }

    [Fact]
    public void GrayShadowLetterModal_SkipKey_InstantlyRevealsText_WithoutPrematureClose()
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();

        // Single frame with skip pressed
        modal.Update(0.016f, isConfirmPressed: false, isSkipPressed: true);

        // Text should be fully revealed
        Assert.True(modal.IsTypingComplete);
        Assert.Equal(GrayShadowLetterModal.LetterText.Length, modal.VisibleCharCount);

        // Crucial UX check: modal must NOT close on the same frame as skip
        Assert.True(modal.IsActive);
        Assert.False(modal.RequestClose);

        // Subsequent frame with confirm pressed -> closes modal
        modal.Update(0.016f, isConfirmPressed: true, isSkipPressed: false);
        Assert.False(modal.IsActive);
        Assert.True(modal.RequestClose);
    }

    [Fact]
    public void GrayShadowLetterModal_ConfirmKey_FirstSkips_ThenDismisses()
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();

        // Press confirm during typewriter
        modal.Update(0.016f, isConfirmPressed: true, isSkipPressed: false);

        // Step 1: Immediately reveal all text
        Assert.True(modal.IsTypingComplete);
        Assert.Equal(GrayShadowLetterModal.LetterText.Length, modal.VisibleCharCount);
        Assert.True(modal.IsActive);
        Assert.False(modal.RequestClose);

        // Step 2: Second press confirm -> dismiss
        modal.Update(0.016f, isConfirmPressed: true, isSkipPressed: false);
        Assert.False(modal.IsActive);
        Assert.True(modal.RequestClose);
    }

    [Fact]
    public void GrayShadowLetterModal_RapidKeySpamming_DoesNotCrashOrCorrupt()
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();

        var rand = new Random(12345);
        for (int i = 0; i < 500; i++)
        {
            float dt = (float)rand.NextDouble() * 0.05f;
            bool confirm = rand.Next(2) == 1;
            bool skip = rand.Next(2) == 1;

            modal.Update(dt, confirm, skip);

            // Substring must never throw ArgumentOutOfRangeException
            string visible = GrayShadowLetterModal.LetterText.Substring(0, modal.VisibleCharCount);
            Assert.NotNull(visible);
            Assert.True(modal.VisibleCharCount >= 0);
            Assert.True(modal.VisibleCharCount <= GrayShadowLetterModal.LetterText.Length);
        }
    }

    [Theory]
    [InlineData(-100.0f)]
    [InlineData(-0.001f)]
    [InlineData(0.0f)]
    [InlineData(0.00001f)]
    [InlineData(100.0f)]
    [InlineData(1000000.0f)]
    public void GrayShadowLetterModal_ExtremeDeltaTimes_ClampSafely(float dt)
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();

        modal.Update(dt);

        Assert.InRange(modal.VisibleCharCount, 0, GrayShadowLetterModal.LetterText.Length);
        string text = GrayShadowLetterModal.LetterText.Substring(0, modal.VisibleCharCount);
        Assert.Equal(modal.VisibleCharCount, text.Length);
    }

    [Fact]
    public void GrayShadowLetterModal_ContentAndParchmentLayout_PreservesBounds()
    {
        string fullText = GrayShadowLetterModal.LetterText;
        Assert.NotEmpty(fullText);
        Assert.Contains("Bóng Xám", fullText);
        Assert.Contains("Cầu Long Biên", fullText);
        Assert.Contains("Cố Đô Huế", fullText);

        string[] lines = fullText.Split('\n');
        Assert.Equal(10, lines.Length);

        // Verify no line is absurdly long to cause horizontal modal escape (modal width is 840px)
        foreach (var line in lines)
        {
            Assert.True(line.Length <= 120, $"Line exceeded max layout length: '{line}'");
        }
    }

    [Fact]
    public void GrayShadowLetterModal_DismissAndReShow_ResetsStateCleanly()
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();
        modal.Update(100f); // fast forward to complete
        modal.Dismiss();

        Assert.False(modal.IsActive);
        Assert.True(modal.RequestClose);

        // Re-show
        modal.Show();
        Assert.True(modal.IsActive);
        Assert.Equal(0, modal.VisibleCharCount);
        Assert.False(modal.IsTypingComplete);
        Assert.False(modal.RequestClose);
    }

    // =========================================================================
    // 5. EMPIRICAL ADVERSARIAL FINDING: DUPLICATE CUTSCENE & MISSING FLAG SYNC
    // =========================================================================

    [Fact]
    public void StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap()
    {
        var bus = new EventBus();
        var audio = new GameProject.Tests.Mocks.NullAudioService();
        var input = new GameProject.Core.Ports.MockInputService();
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Completed = true;
        saveData.Level1State.QuestFlags.Q2_Completed = true;

        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: audio,
            eventBus: bus
        );

        // Step 1: Long Bien Gate interaction opens StallPuzzleScene
        scene.Kinematics.Position = scene.LongBienGate.Position;
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.Equal(GameState.MiniGameStalls, sm.CurrentState);

        // Access StallPuzzleScene
        var stallModalField = typeof(HanoiExplorationScene).GetField("_stallPuzzleModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var stallModal = (StallPuzzleScene)stallModalField!.GetValue(scene)!;
        Assert.NotNull(stallModal);

        // Step 2: Solve the puzzle
        stallModal.Push("Obj_FruitStall", Direction.Left);
        stallModal.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(stallModal.IsSolved);

        // CompleteLevel1 was triggered
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(saveData.Level1State.QuestFlags.Q3_Completed);

        // Bug 1 Empirical Proof: GrayShadow_LetterRead remains false after solve
        Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);

        // Step 3: Trigger StallPuzzleScene's internal letter modal and simulate completion
        var letterModalField = typeof(StallPuzzleScene).GetField("_letterModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)letterModalField!.GetValue(stallModal)!;

        // Advance 3.1s so stall puzzle triggers letter modal
        for (int i = 0; i < 35; i++)
        {
            stallModal.Update(0.1f);
        }
        Assert.True(letterModal.IsActive);

        // Advance typewriter to completion
        for (int i = 0; i < 20; i++)
        {
            letterModal.Update(1.0f);
        }
        Assert.True(letterModal.IsTypingComplete);

        // Forward confirm input into stallModal to dismiss letter modal
        input.IsConfirmJustPressed = true;
        stallModal.Update(0.1f);
        input.IsConfirmJustPressed = false;

        // Verify StallPuzzleScene closed modal and requested next scene
        Assert.True(stallModal.RequestNextScene);

        // Step 4: Exploration scene pops StallPuzzleScene
        scene.Update(0.016f);
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // REMEDIATION CONFIRMED: GrayShadow_LetterRead was synchronized upon modal dismissal
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "REMEDIATION CONFIRMED: StallPuzzleScene sets GrayShadow_LetterRead flag upon modal dismissal");

        // Step 5: Player interacts with Long Bien gate again to enter World Map
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // REMEDIATION CONFIRMED: Player transitions to World Map without duplicate cutscene trap
        Assert.True(scene.RequestWorldMap, "REMEDIATION CONFIRMED: RequestWorldMap is true because GrayShadow_LetterRead was set");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }
}
