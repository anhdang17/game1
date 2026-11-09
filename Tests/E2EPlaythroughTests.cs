using System.IO;
using System.Numerics;
using System.Text;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Models;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.WorldMap;
using GameProject.Graphics;
using GameProject.Tests.Mocks;
using Xunit;

namespace GameProject.Tests;

public class E2EPlaythroughTests
{
    [Fact]
    public void FullLevel1GoldenPlaythrough_FromFreshInstallToClimaxAndReload()
    {
        // ==========================================
        // 1. BOOT GAME (FRESH INSTALL)
        // ==========================================
        var driver = new TestSimulationDriver();

        // Check World Map
        var initialNodes = driver.WorldMapService.GetRegionNodes(driver.CurrentData.WorldProgression);
        var hanoiNode = initialNodes[0];
        var hueNode = initialNodes[1];

        Assert.Equal(RegionStatus.Unlocked, hanoiNode.Status);
        Assert.Equal(0.0f, hanoiNode.ColorRestorationPercent);
        Assert.Equal(RegionStatus.Locked, hueNode.Status);

        // Player cannot select Hue yet
        bool canSelectHue = driver.WorldMapService.TrySelectRegion(RegionIds.Hue, driver.CurrentData.WorldProgression, out _);
        Assert.False(canSelectHue);

        // ==========================================
        // 2. ENTER HANOI & QUEST 1 (INGREDIENT GATHERING)
        // ==========================================
        driver.AdvanceTime(0.5f); // simulate scene load

        // Talk to Bà An -> Start Quest 1
        driver.QuestManager.StartQuest(QuestIds.Hanoi01);
        driver.CurrentData.Level1State.QuestFlags.Q1_Started = true;

        // Player walks to Hàng Bánh Phở
        driver.InputService.SimulateMovement(1.0f, 0.0f);
        driver.AdvanceTicks(30);
        driver.InputService.ReleaseAll();
        driver.SimulateCollectIngredient(ItemIds.BanhPho);

        // Player walks to Hàng Thịt Bò
        driver.SimulateCollectIngredient(ItemIds.ThitBo);

        // Player walks to Bếp Nước Dùng
        driver.SimulateCollectIngredient(ItemIds.NuocDung);

        // Player walks to Gánh Hành Rau
        driver.SimulateCollectIngredient(ItemIds.HanhLa);

        // Player walks to Hiệu Thuốc Lãn Ông
        driver.SimulateCollectIngredient(ItemIds.ThaoMoc);

        // Verify 5/5 ingredients held in inventory
        Assert.Equal(5, driver.CurrentData.Level1State.InventoryItems.Count);
        Assert.Equal(QuestState.RequirementsMet, driver.QuestManager.GetQuestState(QuestIds.Hanoi01));

        // Return to Bà An and turn in quest
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi01);
        driver.CurrentData.Level1State.QuestFlags.Q1_Completed = true;
        Assert.Equal(QuestState.Completed, driver.QuestManager.GetQuestState(QuestIds.Hanoi01));

        // ==========================================
        // 3. QUEST 2 (PHỞ CULINARY ASSEMBLY)
        // ==========================================
        Assert.True(driver.QuestManager.CanStartQuest(QuestIds.Hanoi02));
        driver.QuestManager.StartQuest(QuestIds.Hanoi02);
        driver.CurrentData.Level1State.QuestFlags.Q2_Started = true;

        // Drag ingredients into slots in authentic culinary order:
        driver.PhoEngine.PlaceIngredient(0, ItemIds.BanhPho);
        driver.PhoEngine.PlaceIngredient(1, ItemIds.ThitBo);
        driver.PhoEngine.PlaceIngredient(2, ItemIds.HanhLa);
        driver.PhoEngine.PlaceIngredient(3, ItemIds.NuocDung);
        driver.PhoEngine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool assemblyValid = driver.PhoEngine.ValidateAssembly(out string phoError);
        Assert.True(assemblyValid);
        Assert.Empty(phoError);

        driver.QuestManager.RecordObjectiveProgress(QuestIds.Hanoi02, "Objective_AssemblePho", 1);
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi02);
        driver.CurrentData.Level1State.QuestFlags.Q2_PhoAssembled = true;
        driver.CurrentData.Level1State.QuestFlags.Q2_Completed = true;

        // Color Bloom #1 (+33.3%)
        driver.CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 33.3f);
        Assert.Equal(33.3f, driver.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // ==========================================
        // 4. QUEST 3 (SPATIAL GRID STALL CLEARANCE)
        // ==========================================
        Assert.True(driver.QuestManager.CanStartQuest(QuestIds.Hanoi03));
        driver.QuestManager.StartQuest(QuestIds.Hanoi03);
        driver.CurrentData.Level1State.QuestFlags.Q3_Started = true;

        // Clear grid puzzle
        driver.GridEngine.PushObject("Obj_FruitStall", Direction.Left);
        driver.GridEngine.PushObject("Obj_FlowerCart", Direction.Down);

        bool pathClear = driver.GridEngine.IsPathClear();
        Assert.True(pathClear);

        driver.QuestManager.RecordObjectiveProgress(QuestIds.Hanoi03, "Objective_ClearStallPath", 1);
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi03);
        driver.CurrentData.Level1State.QuestFlags.Q3_GridSolved = true;
        driver.CurrentData.Level1State.QuestFlags.Q3_Completed = true;

        // Full 100% Color Wave
        driver.CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 100.0f);

        // ==========================================
        // 5. NARRATIVE CLIMAX & REWARDS
        // ==========================================
        driver.SimulateLevelClimax();

        Assert.True(driver.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.GrayShadow_LetterRead);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Level1_Finished);

        Assert.Contains("JournalPage_Hanoi_01", driver.CurrentData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains("Stamp_HoaSen_Hanoi", driver.CurrentData.CollectionAlbum.CollectedStamps);

        // ==========================================
        // 6. SAVE PROGRESS, SIMULATE REBOOT, RELOAD
        // ==========================================
        driver.SaveLoadService.Save(driver.CurrentData);

        // Create new driver simulating clean application reboot with preserved storage
        var rebootDriver = new TestSimulationDriver(driver.Storage);
        rebootDriver.CurrentData = rebootDriver.SaveLoadService.Load();

        // Verify loaded state is 100% intact
        Assert.Equal(100.0f, rebootDriver.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(rebootDriver.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(rebootDriver.CurrentData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.True(rebootDriver.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.Equal(5, rebootDriver.CurrentData.Level1State.InventoryItems.Count);
        Assert.Contains("JournalPage_Hanoi_01", rebootDriver.CurrentData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains("Stamp_HoaSen_Hanoi", rebootDriver.CurrentData.CollectionAlbum.CollectedStamps);

        // Huế is now selectable on World Map
        bool canSelectHueNow = rebootDriver.WorldMapService.TrySelectRegion(RegionIds.Hue, rebootDriver.CurrentData.WorldProgression, out string reason);
        Assert.True(canSelectHueNow);
        Assert.Empty(reason);
    }

    [Fact]
    public void StuckPlayer_IdlePromptAndHintAssistanceFlow()
    {
        var driver = new TestSimulationDriver();
        driver.QuestManager.StartQuest(QuestIds.Hanoi01);

        // Player wanders without making progress for 50 seconds
        driver.AdvanceTime(50.0f);

        // Dynamic prompt fires
        bool idlePrompt = driver.HintManager.CheckIdlePrompt(50.0f, 0, out string prompt);
        Assert.True(idlePrompt);
        Assert.Contains("Bút Chì", prompt);

        // Player presses [H] for on-demand hint
        driver.InputService.PressHint();
        string clue = driver.HintManager.GetCurrentHint(QuestIds.Hanoi01, driver.CurrentData.Level1State);
        Assert.Contains("Lãn Ông", clue);

        // Player follows clue and gathers first ingredient
        driver.SimulateCollectIngredient(ItemIds.BanhPho);
        Assert.Single(driver.CurrentData.Level1State.InventoryItems);
    }

    [Fact]
    public void EdgeCase13_OpposingMovementKeys_CancelOutToZeroVector()
    {
        var input = new MockInputService();

        // Press Left and Right simultaneously (A + D)
        input.SimulateOpposingKeys(up: false, down: false, left: true, right: true);
        Assert.Equal(Vector2.Zero, input.MovementDirection);

        // Press Up and Down simultaneously (W + S)
        input.SimulateOpposingKeys(up: true, down: true, left: false, right: false);
        Assert.Equal(Vector2.Zero, input.MovementDirection);

        // Press all 4 directions simultaneously (W + A + S + D)
        input.SimulateOpposingKeys(up: true, down: true, left: true, right: true);
        Assert.Equal(Vector2.Zero, input.MovementDirection);

        // Press Up and Right (diagonal)
        input.SimulateOpposingKeys(up: true, down: false, left: false, right: true);
        Assert.NotEqual(Vector2.Zero, input.MovementDirection);
        Assert.Equal(1.0f, input.MovementDirection.Length(), 3);
    }

    [Fact]
    public void FullContinuousVerticalSlicePlaythrough_ExercisingRequirementsR1ToR4()
    {
        // =========================================================================
        // 1. R1: TITLE / WORLD MAP (HÀ NỘI UNLOCKED AT 0% COLOR, HUẾ LOCKED)
        // =========================================================================
        var driver = new TestSimulationDriver();

        var initialNodes = driver.WorldMapService.GetRegionNodes(driver.CurrentData.WorldProgression);
        Assert.True(initialNodes.Count >= 2);

        var hanoiNode = initialNodes[0];
        Assert.Equal(RegionIds.Hanoi, hanoiNode.RegionId);
        Assert.Equal(RegionStatus.Unlocked, hanoiNode.Status);
        Assert.Equal(0.0f, hanoiNode.ColorRestorationPercent);

        var hueNode = initialNodes[1];
        Assert.Equal(RegionIds.Hue, hueNode.RegionId);
        Assert.Equal(RegionStatus.Locked, hueNode.Status);

        // Attempting to select Hue must fail with locked notification
        bool canSelectHue = driver.WorldMapService.TrySelectRegion(RegionIds.Hue, driver.CurrentData.WorldProgression, out string lockReason);
        Assert.False(canSelectHue);
        Assert.NotEmpty(lockReason);

        // =========================================================================
        // 2. R3: DIALOGUE SYSTEM & FORM C VIETNAMESE DIACRITICS
        // =========================================================================
        string dialoguePath = File.Exists("Data/dialogues_hanoi.json")
            ? "Data/dialogues_hanoi.json"
            : (File.Exists("Source/Data/dialogues_hanoi.json") ? "Source/Data/dialogues_hanoi.json" : "../../../Source/Data/dialogues_hanoi.json");

        Assert.True(File.Exists(dialoguePath), $"Dialogue file must exist at {dialoguePath}");
        driver.DialogueService.LoadDialogueFile(dialoguePath);
        Assert.True(driver.DialogueService.LoadedGraphs.Count >= 5);

        // Start introduction dialogue with Bà An
        var introNode = driver.DialogueService.StartDialogue("Dialogue_BaAn_Intro");
        Assert.NotNull(introNode);
        Assert.Equal("Bà Cụ Bán Phở", introNode.Speaker);
        Assert.Equal("Right", introNode.AvatarPosition);

        // Verify UTF-8 Form C Vietnamese diacritics integrity in dialogue text
        Assert.True(introNode.Text.IsNormalized(NormalizationForm.FormC));
        Assert.Equal(introNode.Text, FontManager.NormalizeText(introNode.Text));
        Assert.Contains("Phố cổ mình giờ ảm đạm quá", introNode.Text);

        // Advance to Bé Mây node with branching options
        var mayNode = driver.DialogueService.AdvanceToNext();
        Assert.NotNull(mayNode);
        Assert.Equal("Bé Mây", mayNode.Speaker);
        Assert.Equal(2, mayNode.Branches.Count);

        // Verify branch choice and accept quest
        var acceptedNode = driver.DialogueService.SelectChoice("Choice_Accept");
        Assert.NotNull(acceptedNode);
        Assert.Equal("Start_Quest_1", acceptedNode.ActionTrigger);

        driver.DialogueService.AdvanceToNext();
        Assert.False(driver.DialogueService.IsDialogueActive);

        // =========================================================================
        // 3. R2 - Q1: HANOI EXPLORATION, CONTROLS & COLLECT 5 PHỞ INGREDIENTS
        // =========================================================================
        driver.QuestManager.StartQuest(QuestIds.Hanoi01);
        driver.CurrentData.Level1State.QuestFlags.Q1_Started = true;

        // Test movement controls: WASD / arrows
        driver.InputService.SimulateMovement(0.0f, -1.0f); // Up
        driver.AdvanceTicks(10);
        Assert.Equal(new Vector2(0.0f, -1.0f), driver.InputService.MovementDirection);

        driver.InputService.SimulateMovement(1.0f, 0.0f); // Right
        driver.AdvanceTicks(10);
        Assert.Equal(new Vector2(1.0f, 0.0f), driver.InputService.MovementDirection);

        // Opposing key cancellation test
        driver.InputService.SimulateOpposingKeys(up: true, down: true, left: false, right: false);
        Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);
        driver.InputService.ReleaseAll();

        // Collect 5 phở ingredients across market stalls with [E] interactions
        driver.InputService.PressInteract();
        driver.SimulateCollectIngredient(ItemIds.BanhPho);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Q1_BanhPho_Collected);

        driver.SimulateCollectIngredient(ItemIds.ThitBo);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Q1_ThitBo_Collected);

        driver.SimulateCollectIngredient(ItemIds.NuocDung);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Q1_NuocDung_Collected);

        driver.SimulateCollectIngredient(ItemIds.HanhLa);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Q1_HanhLa_Collected);

        // =========================================================================
        // 4. R3: BÚT CHÌ HINT ENGINE (ON-DEMAND [H] & IDLE > 45s HINTS)
        // =========================================================================
        // Before 5th ingredient: simulate idle player stalled > 45 seconds
        driver.AdvanceTime(48.0f);
        bool hasIdlePrompt = driver.HintManager.CheckIdlePrompt(48.0f, 4, out string idleAdvice);
        Assert.True(hasIdlePrompt);
        Assert.Contains("Bút Chì", idleAdvice);

        // On-demand hint request via [H]
        driver.InputService.PressHint();
        string onDemandClue = driver.HintManager.GetCurrentHint(QuestIds.Hanoi01, driver.CurrentData.Level1State);
        Assert.Contains("Lãn Ông", onDemandClue);

        // Player follows hint and collects the final 5th ingredient
        driver.SimulateCollectIngredient(ItemIds.ThaoMoc);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Q1_ThaoMoc_Collected);
        Assert.Equal(5, driver.CurrentData.Level1State.InventoryItems.Count);
        Assert.Equal(QuestState.RequirementsMet, driver.QuestManager.GetQuestState(QuestIds.Hanoi01));

        // Return to Bà An and complete Q1
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi01);
        driver.CurrentData.Level1State.QuestFlags.Q1_Completed = true;
        Assert.Equal(QuestState.Completed, driver.QuestManager.GetQuestState(QuestIds.Hanoi01));

        // =========================================================================
        // 5. R2 - Q2: PHỞ CULINARY ASSEMBLY & +33.3% COLOR BLOOM
        // =========================================================================
        Assert.True(driver.QuestManager.CanStartQuest(QuestIds.Hanoi02));
        driver.QuestManager.StartQuest(QuestIds.Hanoi02);
        driver.CurrentData.Level1State.QuestFlags.Q2_Started = true;

        // Assembly puzzle: arrange 5 ingredients into authentic culinary order
        // Layer 1: Bánh Phở, Layer 2: Thịt Bò, Layer 3: Hành Lá, Layer 4: Nước Dùng, Layer 5: Thảo Mộc
        driver.PhoEngine.PlaceIngredient(0, ItemIds.BanhPho);
        driver.PhoEngine.PlaceIngredient(1, ItemIds.ThitBo);
        driver.PhoEngine.PlaceIngredient(2, ItemIds.HanhLa);
        driver.PhoEngine.PlaceIngredient(3, ItemIds.NuocDung);
        driver.PhoEngine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool phoValid = driver.PhoEngine.ValidateAssembly(out string phoError);
        Assert.True(phoValid);
        Assert.Empty(phoError);

        driver.QuestManager.RecordObjectiveProgress(QuestIds.Hanoi02, "Objective_AssemblePho", 1);
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi02);
        driver.CurrentData.Level1State.QuestFlags.Q2_PhoAssembled = true;
        driver.CurrentData.Level1State.QuestFlags.Q2_Completed = true;

        // Color Bloom Wave #1 (+33.3%)
        driver.CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 33.3f);
        Assert.Equal(33.3f, driver.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // =========================================================================
        // 6. R2 - Q3: SPATIAL GRID PUZZLE (7x5 BFS PATH TO LONG BIÊN) & 100% COLOR WAVE
        // =========================================================================
        Assert.True(driver.QuestManager.CanStartQuest(QuestIds.Hanoi03));
        driver.QuestManager.StartQuest(QuestIds.Hanoi03);
        driver.CurrentData.Level1State.QuestFlags.Q3_Started = true;

        // Push obstacles on 7x5 grid to clear path from (0, 2) to (6, 2)
        driver.GridEngine.PushObject("Obj_FruitStall", Direction.Left);
        driver.GridEngine.PushObject("Obj_FlowerCart", Direction.Down);

        bool pathClear = driver.GridEngine.IsPathClear();
        Assert.True(pathClear);

        driver.QuestManager.RecordObjectiveProgress(QuestIds.Hanoi03, "Objective_ClearStallPath", 1);
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi03);
        driver.CurrentData.Level1State.QuestFlags.Q3_GridSolved = true;
        driver.CurrentData.Level1State.QuestFlags.Q3_Completed = true;

        // 100% Full Color Wave
        driver.CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 100.0f);
        Assert.Equal(100.0f, driver.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // =========================================================================
        // 7. R2 - CLIMAX: LONG BIÊN GATE UNLOCK, REWARDS & GRAY SHADOW LETTER
        // =========================================================================
        driver.SimulateLevelClimax();

        Assert.True(driver.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.GrayShadow_LetterRead);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Level1_Finished);
        Assert.True(driver.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));

        // =========================================================================
        // 8. R4: COLLECTION ALBUM 3 TABS VERIFICATION
        // =========================================================================
        // Tab 0: Cuốn Sổ Ký Ức
        var allPages = driver.AlbumService.GetAllJournalPages();
        Assert.True(allPages.Count >= 2);
        Assert.True(driver.AlbumService.IsJournalPageUnlocked(driver.CurrentData.CollectionAlbum, "JournalPage_Hanoi_01"));
        Assert.False(driver.AlbumService.IsJournalPageUnlocked(driver.CurrentData.CollectionAlbum, "JournalPage_Hue_02")); // locked silhouette

        var unlockedPages = driver.AlbumService.GetUnlockedJournalPages(driver.CurrentData.CollectionAlbum);
        Assert.Single(unlockedPages);
        Assert.Equal("JournalPage_Hanoi_01", unlockedPages[0].Id);
        Assert.Contains("Long Biên", unlockedPages[0].StoryText);

        // Tab 1: Tem Du Lịch
        var unlockedStamps = driver.AlbumService.GetUnlockedTravelStamps(driver.CurrentData.CollectionAlbum);
        Assert.Single(unlockedStamps);
        Assert.Equal("Stamp_HoaSen_Hanoi", unlockedStamps[0].Id);
        Assert.Equal("HÀ NỘI - 1000 NĂM VĂN HIẾN", unlockedStamps[0].PostmarkText);

        // Tab 2: Thẻ Văn Hóa (Trivia fact cards)
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, "FactCard_PhoHaNoi");
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, "FactCard_CauLongBien");
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, "FactCard_HoaSenTayHo");
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, "FactCard_36PhoPhuong");

        var factCards = driver.AlbumService.GetUnlockedFactCards(driver.CurrentData.CollectionAlbum);
        Assert.Equal(4, factCards.Count);
        Assert.Contains(factCards, c => c.Title == "Nét Tinh Túy Phở Hà Nội");
        Assert.Contains(factCards, c => c.Title.Contains("Cầu Long Biên"));

        // =========================================================================
        // 9. R1 - PROGRESSION: WORLD MAP NODE REFLECTION & HUẾ UNLOCKED
        // =========================================================================
        var updatedNodes = driver.WorldMapService.GetRegionNodes(driver.CurrentData.WorldProgression);
        var updatedHanoi = updatedNodes[0];
        var updatedHue = updatedNodes[1];

        Assert.Equal(RegionStatus.Completed, updatedHanoi.Status);
        Assert.Equal(100.0f, updatedHanoi.ColorRestorationPercent);
        Assert.Equal(RegionStatus.Unlocked, updatedHue.Status);

        bool canTravelToHue = driver.WorldMapService.TrySelectRegion(RegionIds.Hue, driver.CurrentData.WorldProgression, out string travelErr);
        Assert.True(canTravelToHue);
        Assert.Empty(travelErr);

        // =========================================================================
        // 10. R1 - PERSISTENCE: DURABLE JSON STORAGE ROUND-TRIP & REBOOT FIDELITY
        // =========================================================================
        driver.SaveLoadService.Save(driver.CurrentData);

        // Reboot application from persistent storage
        var rebootHarness = new TestSimulationDriver(driver.Storage);
        rebootHarness.CurrentData = rebootHarness.SaveLoadService.Load();

        // Verify loaded state matches live playthrough exactly
        Assert.Equal(100.0f, rebootHarness.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(rebootHarness.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(rebootHarness.CurrentData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.True(rebootHarness.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(rebootHarness.CurrentData.Level1State.QuestFlags.Level1_Finished);
        Assert.Equal(5, rebootHarness.CurrentData.Level1State.InventoryItems.Count);

        // Album persisted state
        Assert.True(rebootHarness.AlbumService.IsJournalPageUnlocked(rebootHarness.CurrentData.CollectionAlbum, "JournalPage_Hanoi_01"));
        Assert.False(rebootHarness.AlbumService.IsJournalPageUnlocked(rebootHarness.CurrentData.CollectionAlbum, "JournalPage_Hue_02"));
        Assert.True(rebootHarness.AlbumService.IsStampUnlocked(rebootHarness.CurrentData.CollectionAlbum, "Stamp_HoaSen_Hanoi"));
        Assert.Equal(4, rebootHarness.AlbumService.GetUnlockedFactCards(rebootHarness.CurrentData.CollectionAlbum).Count);

        // World map travel
        bool rebootCanTravelHue = rebootHarness.WorldMapService.TrySelectRegion(RegionIds.Hue, rebootHarness.CurrentData.WorldProgression, out _);
        Assert.True(rebootCanTravelHue);
    }
}
