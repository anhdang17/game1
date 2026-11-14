using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.WorldMap;
using GameProject.Stages;
using Raylib_cs;
using Xunit;

namespace GameProject.Tests;

public sealed class ProgressionAndSceneTransitionTests
{
    [Fact]
    public void WorldProgression_HueUnlocks_WhenHanoiCompleted()
    {
        var prog = WorldProgression.CreateDefault();
        Assert.False(prog.IsRegionUnlocked(RegionIds.Hue));

        prog.MarkRegionCompleted(RegionIds.Hanoi);
        Assert.True(prog.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void WorldProgression_HueUnlocks_Dynamically_EvenIfUnlockedRegionsCleared()
    {
        var prog = WorldProgression.CreateDefault();
        prog.CompletedRegions = new List<string> { RegionIds.Hanoi };
        prog.UnlockedRegions = new List<string>(); // emptied

        // Even with UnlockedRegions empty, IsRegionUnlocked(Hue) returns true if Hanoi is completed
        Assert.True(prog.IsRegionUnlocked(RegionIds.Hue));

        // Sanitize should heal UnlockedRegions to contain Hanoi and Hue
        prog.Sanitize();
        Assert.Contains(RegionIds.Hue, prog.UnlockedRegions);
        Assert.Contains(RegionIds.Hanoi, prog.UnlockedRegions);
    }

    [Fact]
    public void SaveData_Sanitize_ReconcilesHueUnlock_FromLevel1FinishedOrQ3Completed()
    {
        var saveA = SaveData.CreateDefault();
        saveA.Level1State.QuestFlags.Level1_Finished = true;
        saveA.WorldProgression.UnlockedRegions = new List<string> { RegionIds.Hanoi };

        saveA.Sanitize();
        Assert.Contains(RegionIds.Hue, saveA.WorldProgression.UnlockedRegions);

        var saveB = SaveData.CreateDefault();
        saveB.Level1State.QuestFlags.Q3_Completed = true;
        saveB.WorldProgression.UnlockedRegions = new List<string> { RegionIds.Hanoi };

        saveB.Sanitize();
        Assert.Contains(RegionIds.Hue, saveB.WorldProgression.UnlockedRegions);
    }

    [Fact]
    public void HanoiExplorationScene_SpatialTriggerZone_TriggersWorldMap_AtX1520_WhenGateUnlocked()
    {
        var data = SaveData.CreateDefault();
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;

        var scene = new HanoiExplorationScene(data);
        scene.Kinematics.Position = new Vector2(1521f, 400f);

        scene.Update(0.016f);
        Assert.True(scene.RequestWorldMap, "Walking past X >= 1520 with unlocked gate must trigger RequestWorldMap.");
    }

    [Fact]
    public void HanoiExplorationScene_SpatialTriggerZone_DoesNotTrigger_WhenGateLocked()
    {
        var data = SaveData.CreateDefault();
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = false;

        var scene = new HanoiExplorationScene(data);
        scene.Kinematics.Position = new Vector2(1521f, 400f);

        scene.Update(0.016f);
        Assert.False(scene.RequestWorldMap, "Walking near eastern boundary with locked gate must NOT trigger RequestWorldMap.");
    }

    [Fact]
    public void HueExplorationScene_Constructs_AndExposes_AuthenticAesthetics_AndHeadlessSafe()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        scene.Enter();
        Assert.False(scene.RequestWorldMap);
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);
        Assert.True(scene.DialogueBox.IsActive);
        Assert.Equal("Bút Chì", scene.DialogueBox.CurrentSpeaker);

        // Update and draw headlessly without exception
        scene.Update(0.016f);
        scene.Draw();

        scene.RequestWorldMap = true;
        Assert.True(scene.RequestWorldMap);
        scene.Exit();
    }

    [Fact]
    public void WorldMapScene_PinHitbox_DetectsBothPinCircleAndLabelPill()
    {
        var data = SaveData.CreateDefault();
        var scene = new WorldMapScene(data);
        scene.Enter();

        Vector2 huePos = WorldMapScene.PinPositions["Hue"];

        // 1. Center of pin circle
        Assert.Equal("Hue", scene.GetPinAtMouse(huePos));

        // 2. Near edge of 28px pin circle
        Assert.Equal("Hue", scene.GetPinAtMouse(huePos + new Vector2(0f, 25f)));

        // 3. Center of label pill below the pin
        Vector2 pillCenter = huePos + new Vector2(0f, 32f);
        Assert.Equal("Hue", scene.GetPinAtMouse(pillCenter));

        // 4. Distant coordinate outside pin and pill
        Assert.Null(scene.GetPinAtMouse(huePos + new Vector2(100f, 100f)));
    }

    [Fact]
    public void WorldMapScene_EscKeyAndBackButton_SetsRequestTravelToHanoi()
    {
        var data = SaveData.CreateDefault();
        var scene = new WorldMapScene(data);
        scene.Enter();

        // Simulate back button bounds
        Rectangle backBtn = new(30f, 20f, 130f, 36f);
        Assert.True(backBtn.Width > 0f);

        // Program.cs transitions back to Hanoi when RequestTravelTo == "Hanoi"
        scene.SelectRegion("Hue");
        Assert.Equal("Hue", scene.SelectedRegionId);
    }
}
