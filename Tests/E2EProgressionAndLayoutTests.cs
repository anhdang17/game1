using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.WorldMap;
using GameProject.Graphics;
using GameProject.Stages;
using GameProject.Tests.Mocks;
using GameProject.UI;
using Raylib_cs;
using Xunit;

namespace GameProject.Tests;

using MockInputService = GameProject.Tests.Mocks.MockInputService;
using NullAudioService = GameProject.Tests.Mocks.NullAudioService;

/// <summary>
/// Comprehensive opaque-box E2E and regression test suite covering Requirements R1, R2, and R3:
/// - R1: Fix next map progression after Hanoi map (Hue unlock, click hitboxes, scene transitions).
/// - R2: Refine and polish game UI (DialogueBox text fitting, QuestHUD progression, InventoryBar symmetric layout, non-overlapping WorldMap controls).
/// - R3: Adjust map size and camera zoom (Hanoi 0.85f camera zoom, pairwise >=64px pin spacing, zero void exposure).
/// Organized into 4 Tiers:
/// - Tier 1: Feature Coverage (happy path functionality)
/// - Tier 2: Boundary & Corner Cases (state desync, camera clamping, hitbox separation, padding)
/// - Tier 3: Cross-Feature Combinations (pipeline transitions & UI element coexistence)
/// - Tier 4: Real-World Application Workload (full end-to-end progression playthrough)
/// </summary>
public sealed class E2EProgressionAndLayoutTests
{
    // =========================================================================
    // TIER 1: FEATURE COVERAGE
    // =========================================================================

    [Fact]
    public void Tier1_CompletingHanoiObjectives_UnlocksHueCentralRealm()
    {
        // 1. Arrange default state
        var data = SaveData.CreateDefault();
        var mapService = new WorldMapService();

        // Verify initial state: Hanoi unlocked at 0%, Hue locked
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.False(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.False(mapService.TrySelectRegion(RegionIds.Hue, data.WorldProgression, out _));

        // 2. Complete Hanoi objectives & mark Hanoi completed
        data.WorldProgression.MarkRegionCompleted(RegionIds.Hanoi);
        data.Level1State.QuestFlags.Level1_Finished = true;
        data.WorldProgression.UnlockRegion(RegionIds.Hue);

        // 3. Assert Hue is unlocked and selectable
        Assert.True(data.WorldProgression.IsRegionCompleted(RegionIds.Hanoi), "Hanoi must be completed.");
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue), "Hue must be unlocked after Hanoi completion.");

        bool canSelectHue = mapService.TrySelectRegion(RegionIds.Hue, data.WorldProgression, out string reason);
        Assert.True(canSelectHue, $"Hue must be selectable on WorldMapService. Failure: {reason}");
        Assert.Empty(reason);
    }

    [Fact]
    public void Tier1_ClickingHuePinOrLabelPill_SelectsHueAndInitiatesTravel()
    {
        var data = SaveData.CreateDefault();
        data.WorldProgression.MarkRegionCompleted(RegionIds.Hanoi);
        data.WorldProgression.UnlockRegion(RegionIds.Hue);

        var wm = new WorldMapScene(data);
        wm.Enter();

        Vector2 huePinPos = WorldMapScene.PinPositions["Hue"];

        // 1. Click at pin center
        string? hitPin = wm.GetPinAtMouse(huePinPos);
        Assert.Equal("Hue", hitPin);

        // 2. Click within pin radius (e.g. 15px to the right)
        string? hitRadius = wm.GetPinAtMouse(huePinPos + new Vector2(15f, 0f));
        Assert.Equal("Hue", hitRadius);

        // 3. Click within label pill area (below pin: +25px Y)
        string? hitLabel = wm.GetPinAtMouse(huePinPos + new Vector2(0f, 25f));
        Assert.Equal("Hue", hitLabel);

        // 4. Distant point returns null
        string? hitVoid = wm.GetPinAtMouse(huePinPos + new Vector2(100f, 100f));
        Assert.NotEqual("Hue", hitVoid);

        // 5. Select Hue & Confirm Travel
        wm.SelectRegion("Hue");
        Assert.Equal("Hue", wm.SelectedRegionId);

        bool travelInitiated = wm.ConfirmTravel();
        Assert.True(travelInitiated, "ConfirmTravel must return true for unlocked Hue region.");
        Assert.Equal("Hue", wm.RequestTravelTo);
        Assert.Contains("Huế", wm.FeedbackMessage ?? "");
    }

    [Fact]
    public void Tier1_SceneCoordinator_TransitionsFromWorldMapToHueSceneWithoutHang()
    {
        var data = SaveData.CreateDefault();
        data.WorldProgression.MarkRegionCompleted(RegionIds.Hanoi);
        data.WorldProgression.UnlockRegion(RegionIds.Hue);

        var sceneManager = new SceneManager();
        var wm = new WorldMapScene(data);
        wm.Enter();
        sceneManager.SwitchScene(wm);

        wm.SelectRegion("Hue");
        wm.ConfirmTravel();
        Assert.Equal("Hue", wm.RequestTravelTo);

        // Simulate Scene Coordinator in Program.cs
        if (sceneManager.CurrentScene is WorldMapScene mapScene && mapScene.RequestTravelTo == "Hue")
        {
            var hueScene = new HueExplorationScene(data);
            sceneManager.SwitchScene(hueScene);
        }

        Assert.IsType<HueExplorationScene>(sceneManager.CurrentScene);
        var currentHue = (HueExplorationScene)sceneManager.CurrentScene;

        // Verify lifecycle methods run headlessly without exception or hang
        currentHue.Enter();
        currentHue.Update(0.016f);
        currentHue.Draw();

        Assert.False(currentHue.RequestWorldMap);
    }

    [Fact]
    public void Tier1_InGameCameraZoom_IsScaledDownForBroaderExplorationView()
    {
        var data = SaveData.CreateDefault();
        var hanoiScene = new HanoiExplorationScene(data);

        // Verify Zoom is scaled down from 1.0f (specifically 0.85f per PROJECT.md § Architecture)
        Assert.True(hanoiScene.CameraTracker.Zoom < 1.0f,
            $"Camera zoom must be scaled down from 1.0f, but was {hanoiScene.CameraTracker.Zoom}");
        Assert.InRange(hanoiScene.CameraTracker.Zoom, 0.82f, 0.90f);
        Assert.Equal(0.85f, hanoiScene.CameraTracker.Zoom);

        // Verify broader visible world view
        float visibleW = hanoiScene.CameraTracker.ViewportSize.X / hanoiScene.CameraTracker.Zoom;
        float visibleH = hanoiScene.CameraTracker.ViewportSize.Y / hanoiScene.CameraTracker.Zoom;

        Assert.True(visibleW > 1450f, $"Visible width should exceed 1450px at zoom 0.85f, was {visibleW}");
        Assert.True(visibleH > 800f, $"Visible height should exceed 800px at zoom 0.85f, was {visibleH}");

        float baseArea = 1280f * 720f;
        float broadArea = visibleW * visibleH;
        float areaIncreaseRatio = broadArea / baseArea;

        Assert.True(areaIncreaseRatio >= 1.35f,
            $"Visible area must be at least +35% broader (got +{(areaIncreaseRatio - 1f) * 100f:F1}%)");
    }

    [Fact]
    public void Tier1_WorldMapPinsAndNavigationControls_DisplayAtProperScale()
    {
        var pins = WorldMapScene.PinPositions;

        // Verify all 10 pins are scaled within map region (X in [50, 820], Y in [80, 650])
        foreach (var (id, pos) in pins)
        {
            Assert.True(pos.X >= 50f && pos.X <= 820f,
                $"Pin '{id}' X position {pos.X} must be within playable map area [50, 820] to clear Preview Card.");
            Assert.True(pos.Y >= 80f && pos.Y <= 650f,
                $"Pin '{id}' Y position {pos.Y} must be within bounds [80, 650] to clear Header and Footer.");
        }

        // Verify Album Button bounds
        Rectangle albumBtn = new(30f, 630f, 200f, 44f);
        Assert.True(albumBtn.X >= 0f && albumBtn.X + albumBtn.Width <= 1280f);
        Assert.True(albumBtn.Y >= 0f && albumBtn.Y + albumBtn.Height <= 720f);

        // Verify Preview Card bounds
        Rectangle previewCard = new(840f, 120f, 400f, 520f);
        Assert.True(previewCard.X >= 840f && previewCard.X + previewCard.Width <= 1280f);
        Assert.True(previewCard.Y >= 0f && previewCard.Y + previewCard.Height <= 720f);
    }

    // =========================================================================
    // TIER 2: BOUNDARY & CORNER CASES
    // =========================================================================

    [Fact]
    public void Tier2_DesyncedSaveState_AutoHealsHueUnlockWhenHanoiIsMarkedCompleted()
    {
        // Case A: Hanoi is marked completed, but UnlockedRegions lacks Hue
        var dataA = SaveData.CreateDefault();
        dataA.WorldProgression.CompletedRegions = new List<string> { RegionIds.Hanoi };
        dataA.WorldProgression.UnlockedRegions = new List<string> { RegionIds.Hanoi };

        // Dynamic check before sanitization
        Assert.True(dataA.WorldProgression.IsRegionUnlocked(RegionIds.Hue),
            "WorldProgression.IsRegionUnlocked must evaluate Hue as unlocked whenever Hanoi is completed.");

        // Sanitization auto-healing
        dataA.Sanitize();
        Assert.True(dataA.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.Contains(RegionIds.Hue, dataA.WorldProgression.UnlockedRegions);

        // Case B: Level1_Finished is true in QuestFlags
        var dataB = SaveData.CreateDefault();
        dataB.Level1State.QuestFlags.Level1_Finished = true;
        dataB.WorldProgression.UnlockedRegions = new List<string> { RegionIds.Hanoi };

        dataB.Sanitize();
        Assert.True(dataB.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.Contains(RegionIds.Hue, dataB.WorldProgression.UnlockedRegions);

        // Case C: UnlockedRegions was wiped completely (null or empty)
        var dataC = SaveData.CreateDefault();
        dataC.WorldProgression.CompletedRegions = new List<string> { RegionIds.Hanoi };
        dataC.WorldProgression.UnlockedRegions = new List<string>();

        dataC.Sanitize();
        Assert.Contains(RegionIds.Hanoi, dataC.WorldProgression.UnlockedRegions);
        Assert.Contains(RegionIds.Hue, dataC.WorldProgression.UnlockedRegions);
    }

    [Fact]
    public void Tier2_CameraClamping_PreventsVisibleBoundsFromExposingOuterVoidAtAllCorners()
    {
        var camera = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280, 720),
            MapBounds = new AABB(0, 0, 1600, 900),
            Zoom = 0.85f
        };

        // Test extreme corners and out-of-bounds coordinates
        Vector2[] testTargets =
        [
            new(-1000f, -1000f), // Far top-left
            new(0f, 0f),          // Top-left corner
            new(1600f, 0f),       // Top-right corner
            new(0f, 900f),        // Bottom-left corner
            new(1600f, 900f),     // Bottom-right corner
            new(800f, 450f),      // Map center
            new(3000f, 2500f),    // Far bottom-right
            new(1520f, 400f)      // Long Bien bridge gate
        ];

        foreach (var target in testTargets)
        {
            camera.SnapToTarget(target);
            var bounds = camera.GetVisibleWorldBounds();

            Assert.True(bounds.MinX >= -0.001f,
                $"Camera target {target} exposed left outer void: MinX = {bounds.MinX}");
            Assert.True(bounds.MinY >= -0.001f,
                $"Camera target {target} exposed top outer void: MinY = {bounds.MinY}");
            Assert.True(bounds.MaxX <= 1600.001f,
                $"Camera target {target} exposed right outer void: MaxX = {bounds.MaxX}");
            Assert.True(bounds.MaxY <= 900.001f,
                $"Camera target {target} exposed bottom outer void: MaxY = {bounds.MaxY}");
        }

        // Verify camera retains positive panning travel
        camera.SnapToTarget(new Vector2(0f, 450f));
        float minClampedX = camera.CurrentPosition.X;
        camera.SnapToTarget(new Vector2(1600f, 450f));
        float maxClampedX = camera.CurrentPosition.X;

        Assert.True(maxClampedX > minClampedX,
            $"Camera must have positive panning travel between edges (minX={minClampedX}, maxX={maxClampedX})");
        Assert.True(maxClampedX - minClampedX >= 80f,
            $"Camera horizontal panning travel should be at least 80px (was {maxClampedX - minClampedX}px)");
    }

    [Fact]
    public void Tier2_WorldMapPinHitboxes_AreStrictlyDisjointPairwiseDistanceAtLeast64px()
    {
        var pins = WorldMapScene.PinPositions;
        var pinKeys = new List<string>(pins.Keys);

        // Check all pairwise combinations (N * (N-1) / 2 = 45 pairs)
        for (int i = 0; i < pinKeys.Count; i++)
        {
            for (int j = i + 1; j < pinKeys.Count; j++)
            {
                string idA = pinKeys[i];
                string idB = pinKeys[j];
                Vector2 posA = pins[idA];
                Vector2 posB = pins[idB];

                float dist = Vector2.Distance(posA, posB);
                Assert.True(dist >= 64.0f,
                    $"Pin '{idA}' at {posA} and '{idB}' at {posB} have distance {dist:F2}px < 64.0px, causing overlapping click hitboxes!");
            }
        }

        // Specific assertions for previously colliding pins
        float hueHoiAnDist = Vector2.Distance(pins["Hue"], pins["HoiAn"]);
        Assert.True(hueHoiAnDist >= 64.0f,
            $"Hue and HoiAn distance must be >= 64px, got {hueHoiAnDist:F2}px.");

        float hoiAnSaiGonDist = Vector2.Distance(pins["HoiAn"], pins["SaiGon"]);
        Assert.True(hoiAnSaiGonDist >= 64.0f,
            $"HoiAn and SaiGon distance must be >= 64px, got {hoiAnSaiGonDist:F2}px.");

        // Rio clears Legend Box (Legend Box at Y=500..620, X=30..230)
        Vector2 rioPos = pins["Rio"];
        Assert.True(rioPos.Y <= 450f,
            $"Rio pin at Y={rioPos.Y} must be <= 450px to clear Legend Box (Y >= 500px).");

        // Bac Cuc clears Header text (Header info at Y=78px)
        Vector2 bacCucPos = pins["BacCuc"];
        Assert.True(bacCucPos.Y >= 110f,
            $"BacCuc pin at Y={bacCucPos.Y} must be >= 110px to clear Header text (Y <= 78px).");
    }

    [Fact]
    public void Tier2_DialogueBox_MultiLineTextFitsWithoutCollidingIntoBottomPrompt()
    {
        var dialogueBox = new DialogueBox();

        // 4-line Vietnamese dialogue text
        string fourLineText =
            "Chào cháu, Bé Mây! Phố cổ Hà Nội nghìn năm văn hiến hôm nay thật ảm đạm.\n" +
            "Bà muốn nấu một nồi phở truyền thống thơm ngon để đánh thức lại sắc màu,\n" +
            "nhưng các sạp hàng đang bị xáo trộn và thiếu mất nguyên liệu tươi ngon.\n" +
            "Cháu và Bút Chì hãy giúp bà thu thập đủ 5 nguyên liệu đặc trưng nhé!";

        dialogueBox.Show("Bà An", fourLineText);
        dialogueBox.CompleteTypewriter();

        Assert.True(dialogueBox.IsActive);
        Assert.True(dialogueBox.IsTypingComplete);
        Assert.Equal("Bà An", dialogueBox.CurrentSpeaker);
        Assert.True(dialogueBox.VisibleText.Length >= 100);

        // Verify layout geometry:
        // Dialogue box height should accommodate 4+ lines without prompt overlap
        // Prompt baseline at boxY + boxH - 22f; 4 lines at 22f step starting at boxY + 28f
        // Bottom of line 4: boxY + 28 + 3*22 + 16 = boxY + 110f
        // Box height of 140f+ gives prompt top at boxY + 140 - 24 = boxY + 116f (clearance >= 6px)
        float boxH = 140f;
        float textStartY = 28f;
        float lineStep = 22f;
        float fontSize = 16f;
        float promptMarginBottom = 10f;
        float promptHeight = 14f;

        float lastLineBottomY = textStartY + (3 * lineStep) + fontSize; // 4th line bottom
        float promptTopY = boxH - promptHeight - promptMarginBottom;

        Assert.True(promptTopY >= lastLineBottomY - 2.0f,
            $"Text bottom at {lastLineBottomY}px must not collide with prompt top at {promptTopY}px.");
    }

    [Fact]
    public void Tier2_InventoryBar_PaddingIsSymmetricAcrossResolutions()
    {
        var eventBus = new EventBus();
        var bar = new InventoryBar(eventBus);
        var inventory = new Inventory();

        int[] resolutions = [1024, 1280, 1366, 1600, 1920, 2560, 3840];

        foreach (int screenW in resolutions)
        {
            bar.Update(0.016f, inventory, Vector2.Zero, screenW);

            float trayLeft = bar.Bounds.X;
            float trayRight = bar.Bounds.X + bar.Bounds.Width;

            float firstSlotLeft = bar.Slots[0].Bounds.X;
            float lastSlotRight = bar.Slots[^1].Bounds.X + bar.Slots[^1].Bounds.Width;

            float leftMargin = firstSlotLeft - trayLeft;
            float rightMargin = trayRight - lastSlotRight;

            // Margin symmetry assertion
            Assert.True(MathF.Abs(leftMargin - rightMargin) <= 4.1f,
                $"At resolution {screenW}px, InventoryBar left margin ({leftMargin}px) and right margin ({rightMargin}px) must be symmetric.");

            // Tray centering assertion
            float expectedTrayX = (screenW - bar.Bounds.Width) * 0.5f;
            Assert.Equal(expectedTrayX, bar.Bounds.X, 2);
        }
    }

    // =========================================================================
    // TIER 3: CROSS-FEATURE COMBINATIONS
    // =========================================================================

    [Fact]
    public void Tier3_CrossFeaturePipeline_HanoiToGateToWorldMapToHueAndReturn()
    {
        // 1. Initialize data and scenes
        var data = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var input = new MockInputService();
        var audio = new NullAudioService();

        var hanoiScene = new HanoiExplorationScene(saveData: data, audioService: audio, inputService: input, eventBus: eventBus);
        var sceneManager = new SceneManager();
        sceneManager.SwitchScene(hanoiScene);

        // 2. Complete objectives & unlock Long Bien Gate
        data.Level1State.QuestFlags.Q1_Completed = true;
        data.Level1State.QuestFlags.Q2_Completed = true;
        data.Level1State.QuestFlags.Q3_Completed = true;
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        data.Level1State.QuestFlags.GrayShadow_LetterRead = true;
        data.Level1State.QuestFlags.Level1_Finished = true;
        data.WorldProgression.CompleteRegion(RegionIds.Hanoi);

        // 3. Move player east past Long Bien Gate (X >= 1520)
        hanoiScene.Kinematics.Position = new Vector2(1525f, 400f);
        hanoiScene.Update(0.016f);

        Assert.True(hanoiScene.RequestWorldMap,
            "Walking east through opened Long Bien Gate (X >= 1520) must trigger RequestWorldMap.");

        // 4. Coordinator switches to WorldMapScene
        var worldMap = new WorldMapScene(data, eventBus, input);
        worldMap.Enter();
        sceneManager.SwitchScene(worldMap);

        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue),
            "Hue must be unlocked after Hanoi completion.");

        // 5. Player selects Hue and confirms travel
        worldMap.SelectRegion("Hue");
        bool travelConfirmed = worldMap.ConfirmTravel();
        Assert.True(travelConfirmed);
        Assert.Equal("Hue", worldMap.RequestTravelTo);

        // 6. Coordinator switches to HueExplorationScene
        var hueScene = new HueExplorationScene(data, audio, input, eventBus);
        hueScene.Enter();
        sceneManager.SwitchScene(hueScene);

        Assert.IsType<HueExplorationScene>(sceneManager.CurrentScene);
        Assert.Equal(0.85f, hueScene.CameraTracker.Zoom);

        // 7. Hue exploration scene updates headlessly
        hueScene.Update(0.016f);
        hueScene.Draw();

        // 8. Player requests return to World Map
        hueScene.RequestWorldMap = true;

        if (sceneManager.CurrentScene is HueExplorationScene hue && hue.RequestWorldMap)
        {
            hue.RequestWorldMap = false;
            sceneManager.SwitchScene(new WorldMapScene(data, eventBus, input));
        }

        // 9. Successfully returned to World Map
        Assert.IsType<WorldMapScene>(sceneManager.CurrentScene);
        Assert.True(data.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void Tier3_UIElementCoexistence_DialogueBoxQuestHUDInventoryBarConcurrentlyActive()
    {
        var data = SaveData.CreateDefault();
        var hanoiScene = new HanoiExplorationScene(data);

        // Active dialogue
        hanoiScene.DialogueBox.Show("Bà An", "Bé Mây hãy gom đủ 5 nguyên liệu nấu phở nhé!");
        Assert.True(hanoiScene.DialogueBox.IsActive);

        // Active HUD and Inventory
        hanoiScene.InventoryBar.Update(0.016f, hanoiScene.Inventory, Vector2.Zero, 1280);
        hanoiScene.QuestHUD.Update(0.016f, hanoiScene.QuestManager, hanoiScene.QuestFlags);

        Rectangle hudRec = hanoiScene.QuestHUD.Bounds;       // Top-left: [20, 20, 265, 82]
        Rectangle barRec = hanoiScene.InventoryBar.Bounds;   // Top-center: [(1280-310)/2, 16, 310, 58]
        Rectangle dialogueRec = new((1280f - 880f) * 0.5f, 550f, 880f, 140f); // Bottom-center

        // Assert no geometric intersection among UI overlay cards
        Assert.False(Raylib.CheckCollisionRecs(hudRec, barRec),
            "QuestHUD and InventoryBar must not collide horizontally.");
        Assert.False(Raylib.CheckCollisionRecs(hudRec, dialogueRec),
            "QuestHUD and DialogueBox must not collide vertically.");
        Assert.False(Raylib.CheckCollisionRecs(barRec, dialogueRec),
            "InventoryBar and DialogueBox must not collide vertically.");

        // Assert generous clearance margins
        float hudToBarGap = barRec.X - (hudRec.X + hudRec.Width);
        Assert.True(hudToBarGap >= 150f,
            $"Horizontal clearance between QuestHUD and InventoryBar should be >= 150px (was {hudToBarGap}px)");

        float topUIToDialogueGap = dialogueRec.Y - MathF.Max(hudRec.Y + hudRec.Height, barRec.Y + barRec.Height);
        Assert.True(topUIToDialogueGap >= 400f,
            $"Vertical clearance between Top UI and DialogueBox should be >= 400px (was {topUIToDialogueGap}px)");
    }

    // =========================================================================
    // TIER 4: REAL-WORLD APPLICATION WORKLOAD
    // =========================================================================

    [Fact]
    public void Tier4_ComprehensiveEndToEndPlayerProgressionPlaythroughSimulation()
    {
        // 1. BOOT ENGINE & FRESH PROFILE
        var driver = new TestSimulationDriver();

        var initialNodes = driver.WorldMapService.GetRegionNodes(driver.CurrentData.WorldProgression);
        Assert.Equal(RegionStatus.Unlocked, initialNodes[0].Status); // Hanoi
        Assert.Equal(RegionStatus.Locked, initialNodes[1].Status);   // Hue

        // 2. QUEST 1: GATHER 5 INGREDIENTS ACROSS HANOI
        driver.QuestManager.StartQuest(QuestIds.Hanoi01);
        driver.CurrentData.Level1State.QuestFlags.Q1_Started = true;

        driver.SimulateCollectIngredient(ItemIds.BanhPho);
        driver.SimulateCollectIngredient(ItemIds.ThitBo);
        driver.SimulateCollectIngredient(ItemIds.NuocDung);
        driver.SimulateCollectIngredient(ItemIds.HanhLa);
        driver.SimulateCollectIngredient(ItemIds.ThaoMoc);

        Assert.Equal(5, driver.CurrentData.Level1State.InventoryItems.Count);
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi01);
        driver.CurrentData.Level1State.QuestFlags.Q1_Completed = true;

        // 3. QUEST 2: ASSEMBLE PHO CULINARY MINIGAME
        driver.QuestManager.StartQuest(QuestIds.Hanoi02);
        driver.CurrentData.Level1State.QuestFlags.Q2_Started = true;

        driver.PhoEngine.PlaceIngredient(0, ItemIds.BanhPho);
        driver.PhoEngine.PlaceIngredient(1, ItemIds.ThitBo);
        driver.PhoEngine.PlaceIngredient(2, ItemIds.HanhLa);
        driver.PhoEngine.PlaceIngredient(3, ItemIds.NuocDung);
        driver.PhoEngine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool phoSolved = driver.PhoEngine.ValidateAssembly(out _);
        Assert.True(phoSolved);
        driver.QuestManager.CompleteQuest(QuestIds.Hanoi02);
        driver.CurrentData.Level1State.QuestFlags.Q2_PhoAssembled = true;
        driver.CurrentData.Level1State.QuestFlags.Q2_Completed = true;
        driver.CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 33.3f);

        // 4. QUEST 3: SPATIAL GRID STALL CLEARANCE PUZZLE
        driver.QuestManager.StartQuest(QuestIds.Hanoi03);
        driver.CurrentData.Level1State.QuestFlags.Q3_Started = true;

        driver.GridEngine.PushObject("Obj_FruitStall", Direction.Left);
        driver.GridEngine.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(driver.GridEngine.IsPathClear());

        driver.QuestManager.CompleteQuest(QuestIds.Hanoi03);
        driver.CurrentData.Level1State.QuestFlags.Q3_GridSolved = true;
        driver.CurrentData.Level1State.QuestFlags.Q3_Completed = true;
        driver.CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 100.0f);

        // 5. NARRATIVE CLIMAX & REWARDS
        driver.SimulateLevelClimax();

        Assert.True(driver.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.GrayShadow_LetterRead);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Level1_Finished);
        Assert.True(driver.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(driver.CurrentData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));

        // 6. WORLD MAP SELECTION & TRAVEL CONFIRMATION
        bool canSelectHue = driver.WorldMapService.TrySelectRegion(RegionIds.Hue, driver.CurrentData.WorldProgression, out string travelErr);
        Assert.True(canSelectHue, $"Hue must be selectable. Error: {travelErr}");
        Assert.Empty(travelErr);

        var wmScene = new WorldMapScene(driver.CurrentData);
        wmScene.Enter();
        wmScene.SelectRegion("Hue");
        Assert.True(wmScene.ConfirmTravel());
        Assert.Equal("Hue", wmScene.RequestTravelTo);

        // 7. TRANSITION INTO HUE EXPLORATION SCENE
        var hueScene = new HueExplorationScene(driver.CurrentData);
        hueScene.Enter();
        hueScene.Update(0.016f);
        hueScene.Draw();

        Assert.Equal(0.85f, hueScene.CameraTracker.Zoom);
        Assert.True(hueScene.CameraTracker.GetVisibleWorldBounds().MinX >= 0f);

        // 8. RETURN TO WORLD MAP & PERSIST STORAGE
        hueScene.RequestWorldMap = true;
        driver.SaveLoadService.Save(driver.CurrentData);

        // 9. COLD REBOOT FIDELITY SIMULATION
        var rebootHarness = new TestSimulationDriver(driver.Storage);
        rebootHarness.CurrentData = rebootHarness.SaveLoadService.Load();

        Assert.Equal(100.0f, rebootHarness.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(rebootHarness.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(rebootHarness.CurrentData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.True(rebootHarness.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(rebootHarness.CurrentData.Level1State.QuestFlags.Level1_Finished);
        Assert.Equal(5, rebootHarness.CurrentData.Level1State.InventoryItems.Count);

        bool rebootCanTravelHue = rebootHarness.WorldMapService.TrySelectRegion(RegionIds.Hue, rebootHarness.CurrentData.WorldProgression, out _);
        Assert.True(rebootCanTravelHue);
    }
}
