using System.Numerics;
using GameProject.Core.Models;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.WorldMap;
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
}
