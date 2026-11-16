using System;
using System.Numerics;
using GameProject.Audio;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Graphics;
using GameProject.Stages;
using Xunit;

namespace GameProject.Tests;

public class HueExplorationTests
{

    [Fact]
    public void HueExplorationScene_Constructs_WithCorrectDefaults_AndEntities()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        Assert.Equal(0.85f, scene.CameraTracker.Zoom);
        Assert.Equal(1600f, scene.CollisionWorld.MapBounds.MaxX);
        Assert.Equal(900f, scene.CollisionWorld.MapBounds.MaxY);
        Assert.Equal(new Vector2(400f, 480f), scene.Kinematics.Position);

        Assert.NotNull(scene.MeBich);
        Assert.Equal(5, scene.Vendors.Count);
        Assert.NotNull(scene.Boatman);
        Assert.NotNull(scene.NgoMon);
        Assert.NotNull(scene.ThienMu);
        Assert.NotNull(scene.TrangTien);
        Assert.NotNull(scene.BloomEffect);
        Assert.NotNull(scene.Particles);
    }

    [Fact]
    public void HueExplorationScene_Enter_PreservesWelcomeDialogue_AndHardRegressionInvariant()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        scene.Enter();

        Assert.False(scene.RequestWorldMap);
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);
        Assert.True(scene.DialogueBox.IsActive);
        Assert.Equal("Bút Chì", scene.DialogueBox.CurrentSpeaker);
    }

    [Fact]
    public void HueExplorationScene_Update_AndDraw_HeadlessSafe()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        scene.Enter();
        scene.Update(0.016f);
        scene.Draw();
        scene.Exit();
    }

    [Fact]
    public void HueExplorationScene_Interaction_MeBich_StartsQuest1()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();

        // Dismiss welcome dialogue
        scene.DialogueBox.Dismiss();
        scene.Update(0.016f);

        // Position player near Mệ Bích
        scene.Kinematics.Position = scene.MeBich.Position + new Vector2(10f, 0);
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);

        Assert.True(scene.QuestFlags.Hue_Q1_Started);
        Assert.True(scene.DialogueBox.IsActive);
        Assert.Equal("Mệ Bích", scene.DialogueBox.CurrentSpeaker);
    }

    [Fact]
    public void HueExplorationScene_Interaction_Vendors_Gather5Ingredients()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();
        scene.QuestFlags.Hue_Q1_Started = true;

        // Dismiss welcome dialogue
        scene.DialogueBox.Dismiss();
        scene.Update(0.016f);

        for (int i = 0; i < scene.Vendors.Count; i++)
        {
            var vendor = scene.Vendors[i];
            scene.Kinematics.Position = vendor.Position + new Vector2(10f, 0);
            input.IsInteractJustPressed = true;
            scene.Update(0.016f);

            Assert.True(scene.Inventory.HasItem(vendor.ItemId), $"Player must have collected {vendor.ItemId}");
            scene.DialogueBox.Dismiss();
            input.IsInteractJustPressed = false;
            scene.Update(0.016f);
        }

        Assert.True(scene.QuestFlags.AreAllHueQ1IngredientsCollected());
        Assert.Equal(5, scene.QuestFlags.GetHueQ1CollectedCount());
    }

    [Fact]
    public void HueExplorationScene_Interaction_MeBich_CompletesQuest1()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();

        // Set all 5 ingredients collected
        scene.QuestFlags.Hue_Q1_Started = true;
        scene.QuestFlags.Hue_Q1_BunSoiTo_Collected = true;
        scene.QuestFlags.Hue_Q1_BapBo_Collected = true;
        scene.QuestFlags.Hue_Q1_ChaCua_Collected = true;
        scene.QuestFlags.Hue_Q1_NuocDungHue_Collected = true;
        scene.QuestFlags.Hue_Q1_RauSongHue_Collected = true;
        scene.Inventory.AddItem(ItemIds.BunSoiTo);
        scene.Inventory.AddItem(ItemIds.BapBo);
        scene.Inventory.AddItem(ItemIds.ChaCua);
        scene.Inventory.AddItem(ItemIds.NuocDungHue);
        scene.Inventory.AddItem(ItemIds.RauSongHue);

        // Dismiss welcome dialogue
        scene.DialogueBox.Dismiss();
        scene.Update(0.016f);

        // Interact with Mệ Bích to turn in
        scene.Kinematics.Position = scene.MeBich.Position + new Vector2(10f, 0);
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);

        Assert.True(scene.QuestFlags.Hue_Q1_Completed);
    }

    [Fact]
    public void HueExplorationScene_MeBich_LaunchesBunBoHueAssemblyScene_Modal()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();

        scene.QuestFlags.Hue_Q1_Started = true;
        scene.QuestFlags.Hue_Q1_Completed = true;
        scene.QuestFlags.Hue_Q2_Completed = false;

        // Dismiss welcome dialogue
        scene.DialogueBox.Dismiss();
        scene.Update(0.016f);

        // Interact with Mệ Bích to launch cooking assembly modal
        scene.Kinematics.Position = scene.MeBich.Position + new Vector2(10f, 0);
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);

        Assert.NotNull(scene.BunBoAssemblyModal);

        // Update modal headlessly
        scene.Update(0.016f);
        scene.Draw();
    }

    [Fact]
    public void HueExplorationScene_Boatman_LaunchesHoaDangRiverScene_Modal()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();

        scene.QuestFlags.Hue_Q1_Completed = true;
        scene.QuestFlags.Hue_Q2_Completed = true;
        scene.QuestFlags.Hue_Q3_Completed = false;

        // Dismiss welcome dialogue
        scene.DialogueBox.Dismiss();
        scene.Update(0.016f);

        // Interact with Boatman at pier to launch HoaDangRiverScene modal
        scene.Kinematics.Position = scene.Boatman.Position + new Vector2(10f, 0);
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);

        Assert.NotNull(scene.HoaDangModal);

        // Update modal headlessly
        scene.Update(0.016f);
        scene.Draw();
    }

    [Fact]
    public void HueExplorationScene_TrangTienBridge_ExitTransition_WhenUnlocked()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // 1. When locked, stepping past 1520 does not trigger exit
        scene.Kinematics.Position = new Vector2(1525f, 530f);
        scene.Update(0.016f);
        Assert.False(scene.RequestWorldMap);

        // 2. When unlocked, stepping past 1520 triggers transition
        scene.QuestFlags.Gate_HoiAn_Unlocked = true;
        scene.Kinematics.Position = new Vector2(1525f, 530f);
        scene.Update(0.016f);
        Assert.True(scene.RequestWorldMap);
    }

    [Fact]
    public void HueExplorationScene_Landmarks_InspectionTriggers_ReturnAuthenticCulturalLore()
    {
        var data = SaveData.CreateDefault();
        var context = new InteractionContext(Vector2.Zero, new Inventory(), new QuestManager(new EventBus()), data.QuestFlags, new EventBus(), new NullAudioService());

        var (meBich, vendors, boatman, ngoMon, thienMu, trangTien) = HueInteractableFactory.CreateHueInteractables();

        var resNgoMon = ngoMon.Interact(context);
        Assert.True(resNgoMon.Success);
        Assert.Contains("Ngọ Môn", resNgoMon.DialogueText);
        Assert.Contains("Hoàng Thành Huế", resNgoMon.DialogueText);

        var resThienMu = thienMu.Interact(context);
        Assert.True(resThienMu.Success);
        Assert.Contains("Chùa Thiên Mụ", resThienMu.DialogueText);
        Assert.Contains("tháp Phước Duyên", resThienMu.DialogueText);

        var resTrangTien = trangTien.Interact(context);
        Assert.True(resTrangTien.Success);
        Assert.Contains("Cầu Tràng Tiền", resTrangTien.DialogueText);
    }

    [Fact]
    public void HuePalette_ImperialAndRiverColors_AreDefinedWithAuthenticHueValues()
    {
        Assert.Equal(155, HuePalette.ImperialCrimson.R);
        Assert.Equal(43, HuePalette.ImperialCrimson.G);
        Assert.Equal(32, HuePalette.ImperialCrimson.B);

        Assert.Equal(245, HuePalette.RoyalGold.R);
        Assert.Equal(195, HuePalette.RoyalGold.G);
        Assert.Equal(45, HuePalette.RoyalGold.B);

        Assert.Equal(46, HuePalette.PerfumeRiverTeal.R);
        Assert.Equal(125, HuePalette.PerfumeRiverTeal.G);
        Assert.Equal(138, HuePalette.PerfumeRiverTeal.B);

        Assert.Equal(106, HuePalette.TwilightPurple.R);
        Assert.Equal(61, HuePalette.TwilightPurple.G);
        Assert.Equal(104, HuePalette.TwilightPurple.B);

        Assert.Equal(196, HuePalette.TrangTienSteel.R);
        Assert.Equal(203, HuePalette.TrangTienSteel.G);
        Assert.Equal(212, HuePalette.TrangTienSteel.B);
    }

    [Fact]
    public void HueExplorationScene_ModalSolve_UpdateIsIdempotent_TriggersProgressionOnlyOnce()
    {
        var data = SaveData.CreateDefault();
        var eventBus = new EventBus();
        using var scene = new HueExplorationScene(data, eventBus: eventBus);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        int regionColorUpdatedCount = 0;
        int regionCompletedCount = 0;
        int gateUnlockedCount = 0;

        eventBus.Subscribe<RegionColorUpdatedEvent>(_ => regionColorUpdatedCount++);
        eventBus.Subscribe<RegionCompletedEvent>(_ => regionCompletedCount++);
        eventBus.Subscribe<GateUnlockedEvent>(_ => gateUnlockedCount++);

        // 1. Verify Bun Bo Assembly modal solve idempotence
        scene.OpenBunBoAssemblyModal();
        Assert.NotNull(scene.BunBoAssemblyModal);

        for (int i = 0; i < 5; i++)
        {
            scene.BunBoAssemblyModal!.PlaceCard(i, i);
        }
        scene.BunBoAssemblyModal!.Validate();
        Assert.True(scene.BunBoAssemblyModal.IsSolved);

        // Update scene across 30 consecutive frames (simulating solve celebration loop)
        for (int frame = 0; frame < 30; frame++)
        {
            scene.Update(0.016f);
        }

        // RegionColorUpdatedEvent must be published exactly once for Q2 solve
        Assert.Equal(1, regionColorUpdatedCount);
        Assert.True(scene.QuestFlags.Hue_Q2_Completed);
        Assert.True(scene.QuestFlags.Hue_Q2_BunBoCooked);

        // 2. Verify Hoa Dang River modal solve idempotence
        scene.OpenHoaDangRiverModal();
        Assert.NotNull(scene.HoaDangModal);

        scene.HoaDangModal!.Push("Obj_FisherSampan", Direction.Left);
        scene.HoaDangModal!.Push("Obj_DragonBoat_1", Direction.Down);
        Assert.True(scene.HoaDangModal.IsSolved);

        // Update scene across 30 consecutive frames (simulating solve celebration loop)
        for (int frame = 0; frame < 30; frame++)
        {
            scene.Update(0.016f);
        }

        // RegionCompletedEvent and GateUnlockedEvent must be published exactly once
        Assert.Equal(1, regionCompletedCount);
        Assert.Equal(1, gateUnlockedCount);
        Assert.True(scene.QuestFlags.Hue_Q3_Completed);
        Assert.True(scene.QuestFlags.Hue_Q3_PuzzleSolved);
        Assert.True(scene.QuestFlags.Gate_HoiAn_Unlocked);

        // Verify color bloom ripple wave animated properly
        Assert.True(scene.BloomEffect.CurrentColorPercent > 0f);
    }
}
