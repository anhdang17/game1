using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Audio;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Graphics;
using GameProject.Stages;
using Xunit;
using NullAudioService = GameProject.Tests.Mocks.NullAudioService;
using MockInputService = GameProject.Tests.Mocks.MockInputService;
using MockSaveStorage = GameProject.Tests.Mocks.MockSaveStorage;

namespace GameProject.Tests;

/// <summary>
/// Challenger 2 (Adversarial Verifier) Stress Test Suite for Milestone 3 Iteration 2:
/// 1. Re-verify headless safety: 1000 frames of updates and draws without window context.
/// 2. Re-verify regression invariants (ProgressionAndSceneTransitionTests.cs:87-105): zoom 0.85f, Bút Chì welcome dialogue, constructor overloads.
/// 3. Stress test modal enter, solve, update, and exit lifecycles with the new flags (_bunBoWaveTriggered, _hoaDangWaveTriggered).
/// </summary>
public sealed class Challenger2M3R2HueExplorationStressTests
{
    // =========================================================================
    // SECTION 1: Headless Safety & 1000-Frame Stress Verification
    // =========================================================================

    [Fact]
    public void HeadlessSafety_1000Frames_CleanExploration_NeverThrows_AndParticlesStayBounded()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // 1000 consecutive frames of headless updates and draws
        for (int frame = 0; frame < 1000; frame++)
        {
            scene.Update(0.016f);
            scene.Draw();

            // Sanity invariants during every frame
            Assert.False(float.IsNaN(scene.Kinematics.Position.X));
            Assert.False(float.IsNaN(scene.Kinematics.Position.Y));
            Assert.False(float.IsInfinity(scene.Kinematics.Position.X));
            Assert.False(float.IsInfinity(scene.Kinematics.Position.Y));
            Assert.False(float.IsNaN(scene.CameraTracker.CurrentPosition.X));
            Assert.False(float.IsNaN(scene.CameraTracker.CurrentPosition.Y));
        }

        // Particle pool remains healthy and bounded
        Assert.True(scene.Particles.ParticleCount >= 0);
        Assert.True(scene.Particles.ParticleCount < 500, "Particle count must not grow unbounded without cap.");
        scene.Exit();
    }

    [Fact]
    public void HeadlessSafety_1000Frames_KinematicsAndBoundaryCollisions_NeverPenetrates()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // Directions cycling: North (into Citadel wall), South (into Sông Hương deep curb),
        // West (into map left bound), East (into Tràng Tiền bridge / barrier)
        Vector2[] testDirections =
        {
            new(0f, -1f),   // Up
            new(0f, 1f),    // Down
            new(-1f, 0f),   // Left
            new(1f, 0f),    // Right
            new(1f, 1f),    // Down-Right
            new(-1f, -1f)   // Up-Left
        };

        for (int frame = 0; frame < 1000; frame++)
        {
            var dir = testDirections[(frame / 50) % testDirections.Length];
            input.SimulateMove(dir.X, dir.Y);

            // Dash periodically
            input.IsDashJustPressed = (frame % 40 == 0);

            scene.Update(0.016f);
            scene.Draw();

            // Player must stay strictly within map boundaries [0..1600, 0..900]
            Assert.InRange(scene.Kinematics.Position.X, 0f, 1600f);
            Assert.InRange(scene.Kinematics.Position.Y, 0f, 900f);

            // Citadel wall obstacle check (AABB: 0, 0, 1600, 320)
            // Kinematics circle radius is ~12-16px, so center should never penetrate deep into wall
            Assert.True(scene.Kinematics.Position.Y >= 310f, $"Y ({scene.Kinematics.Position.Y}) penetrated Citadel wall!");

            // Deep river curb check (AABB: 0, 820, 1600, 900)
            Assert.True(scene.Kinematics.Position.Y <= 830f, $"Y ({scene.Kinematics.Position.Y}) penetrated deep river curb!");
        }

        scene.Exit();
    }

    [Fact]
    public void HeadlessSafety_VariableDeltaTime_AndExtremeTimers_StaysStable()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // 1. Extreme zero and negative deltaTimes must safely early-return
        scene.Update(0f);
        scene.Update(-0.5f);
        scene.Update(-100f);
        Assert.Equal(new Vector2(400f, 480f), scene.Kinematics.Position);

        // 2. Large delta times (e.g. lag spikes 0.5s, 1.0s, 5.0s)
        float[] lagSpikes = { 0.1f, 0.25f, 0.5f, 1.0f, 2.5f };
        foreach (float dt in lagSpikes)
        {
            scene.Update(dt);
            scene.Draw();
            Assert.False(float.IsNaN(scene.Kinematics.Position.X));
            Assert.False(float.IsNaN(scene.Kinematics.Position.Y));
        }

        // 3. Very tiny delta times (micro-frames)
        for (int i = 0; i < 200; i++)
        {
            scene.Update(0.0001f);
            scene.Draw();
        }

        Assert.False(float.IsNaN(scene.Kinematics.Position.X));
        Assert.False(float.IsNaN(scene.Kinematics.Position.Y));
        scene.Exit();
    }

    [Fact]
    public void HeadlessSafety_1000Frames_ActiveDialogue_FreezesKinematics()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();

        // Welcome dialogue is active by default
        Assert.True(scene.DialogueBox.IsActive);
        Vector2 initialPos = scene.Kinematics.Position;

        // Try moving during 1000 frames with dialogue active
        for (int frame = 0; frame < 1000; frame++)
        {
            input.SimulateMove(1f, 1f);
            input.IsDashJustPressed = true;
            scene.Update(0.016f);
            scene.Draw();

            // Kinematics must remain frozen
            Assert.Equal(initialPos, scene.Kinematics.Position);
        }

        scene.Exit();
    }

    // =========================================================================
    // SECTION 2: Regression Invariants & Constructor Overloads
    // =========================================================================

    [Fact]
    public void RegressionInvariant_Zoom_AndWelcomeDialogue_AlwaysPreserved()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        // Invariant 1: Zoom == 0.85f before Enter()
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);

        // Invariant 2: Enter() triggers Bút Chì welcome
        scene.Enter();
        Assert.False(scene.RequestWorldMap);
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);
        Assert.True(scene.DialogueBox.IsActive);
        Assert.Equal("Bút Chì", scene.DialogueBox.CurrentSpeaker);
        Assert.Contains("Cố Đô Huế", scene.DialogueBox.CurrentText);

        // Dismiss dialogue and verify re-entering does NOT re-trigger welcome dialogue
        scene.DialogueBox.Dismiss();
        Assert.False(scene.DialogueBox.IsActive);

        scene.Enter();
        Assert.False(scene.DialogueBox.IsActive);
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);

        // Invariant 3: Snap target and 100 frames update maintains Zoom == 0.85f
        scene.CameraTracker.SnapToTarget(new Vector2(800f, 600f));
        for (int i = 0; i < 100; i++)
        {
            scene.Update(0.016f);
        }
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);

        scene.Exit();
    }

    [Fact]
    public void ConstructorOverloads_All8Permutations_ConstructAndRunSafely()
    {
        var data = SaveData.CreateDefault();
        var audio = new GameProject.Core.Ports.NullAudioService();
        var input = new MockInputService();
        var bus = new EventBus();
        var inv = new Inventory();
        var qm = new QuestManager(bus);
        var diag = new DialogueService(bus);

        // Permutation 1: Parameterless / all default
        using (var s1 = new HueExplorationScene())
        {
            VerifySceneContract(s1);
        }

        // Permutation 2: (SaveData)
        using (var s2 = new HueExplorationScene(data))
        {
            VerifySceneContract(s2);
        }

        // Permutation 3: (SaveData, AudioService)
        using (var s3 = new HueExplorationScene(data, audio))
        {
            VerifySceneContract(s3);
        }

        // Permutation 4: (SaveData, AudioService, InputService)
        using (var s4 = new HueExplorationScene(data, audio, input))
        {
            VerifySceneContract(s4);
        }

        // Permutation 5: (SaveData, AudioService, InputService, EventBus)
        using (var s5 = new HueExplorationScene(data, audio, input, bus))
        {
            VerifySceneContract(s5);
        }

        // Permutation 6: (SaveData, AudioService, InputService, EventBus, Inventory)
        using (var s6 = new HueExplorationScene(data, audio, input, bus, inv))
        {
            VerifySceneContract(s6);
        }

        // Permutation 7: (SaveData, AudioService, InputService, EventBus, Inventory, QuestManager)
        using (var s7 = new HueExplorationScene(data, audio, input, bus, inv, qm))
        {
            VerifySceneContract(s7);
        }

        // Permutation 8: (SaveData, AudioService, InputService, EventBus, Inventory, QuestManager, DialogueService)
        using (var s8 = new HueExplorationScene(data, audio, input, bus, inv, qm, diag))
        {
            VerifySceneContract(s8);
        }
    }

    private static void VerifySceneContract(HueExplorationScene scene)
    {
        Assert.NotNull(scene.SaveData);
        Assert.NotNull(scene.AudioService);
        Assert.NotNull(scene.InputService);
        Assert.NotNull(scene.EventBus);
        Assert.NotNull(scene.Inventory);
        Assert.NotNull(scene.QuestManager);
        Assert.NotNull(scene.DialogueService);
        Assert.NotNull(scene.DialogueBox);
        Assert.NotNull(scene.CollisionWorld);
        Assert.NotNull(scene.Kinematics);
        Assert.NotNull(scene.CameraTracker);
        Assert.NotNull(scene.InteractionManager);
        Assert.NotNull(scene.BloomEffect);
        Assert.NotNull(scene.Particles);
        Assert.NotNull(scene.MeBich);
        Assert.NotNull(scene.Boatman);
        Assert.NotNull(scene.NgoMon);
        Assert.NotNull(scene.ThienMu);
        Assert.NotNull(scene.TrangTien);
        Assert.Equal(5, scene.Vendors.Count);
        Assert.Equal(0.85f, scene.CameraTracker.Zoom);

        scene.Enter();
        scene.Update(0.016f);
        scene.Draw();
        scene.Exit();
    }

    // =========================================================================
    // SECTION 3: Modal Lifecycles, Wave Trigger Flags & Idempotence
    // =========================================================================

    [Fact]
    public void BunBoModal_Lifecycle_Open_Update_Solve_WaveTriggeredOnce_Exit_Clean()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        int colorEventCount = 0;
        bus.Subscribe<RegionColorUpdatedEvent>(_ => colorEventCount++);

        using var scene = new HueExplorationScene(data, eventBus: bus);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // 1. Open modal
        scene.OpenBunBoAssemblyModal();
        Assert.NotNull(scene.BunBoAssemblyModal);
        Assert.False(scene.QuestFlags.Hue_Q2_Completed);

        // 2. Advance 60 frames without solving: no premature events
        for (int i = 0; i < 60; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }
        Assert.Equal(0, colorEventCount);
        Assert.False(scene.QuestFlags.Hue_Q2_Completed);

        // 3. Solve modal
        for (int i = 0; i < 5; i++)
        {
            scene.BunBoAssemblyModal!.PlaceCard(i, i);
        }
        scene.BunBoAssemblyModal!.Validate();
        Assert.True(scene.BunBoAssemblyModal.IsSolved);

        // 4. Update 100 frames during victory celebration animation (100 * 0.016f = 1.6s < 2.5s)
        for (int i = 0; i < 100; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }

        // Must trigger progression and wave EXACTLY ONCE
        Assert.Equal(1, colorEventCount);
        Assert.True(scene.QuestFlags.Hue_Q2_Completed);
        Assert.True(scene.QuestFlags.Hue_Q2_BunBoCooked);
        Assert.True(scene.BloomEffect.CurrentColorPercent >= 33.3f);

        // 5. Advance another 70 frames to pass 2.5s celebration timer, which sets RequestExit = true
        for (int i = 0; i < 70; i++)
        {
            scene.Update(0.016f);
        }

        // Modal reference cleared
        Assert.Null(scene.BunBoAssemblyModal);

        // Normal exploration continues cleanly for 100 frames
        for (int i = 0; i < 100; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }
        Assert.Equal(1, colorEventCount);

        scene.Exit();
    }

    [Fact]
    public void HoaDangModal_Lifecycle_Open_Update_Solve_WaveTriggeredOnce_Exit_ExitBarrierRemoved()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        int completedEventCount = 0;
        int gateEventCount = 0;
        bus.Subscribe<RegionCompletedEvent>(_ => completedEventCount++);
        bus.Subscribe<GateUnlockedEvent>(_ => gateEventCount++);

        using var scene = new HueExplorationScene(data, eventBus: bus);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // 1. Open modal
        scene.OpenHoaDangRiverModal();
        Assert.NotNull(scene.HoaDangModal);
        Assert.False(scene.QuestFlags.Hue_Q3_Completed);

        // 2. Advance 60 frames without solving
        for (int i = 0; i < 60; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }
        Assert.Equal(0, completedEventCount);
        Assert.Equal(0, gateEventCount);

        // 3. Solve puzzle: push obstacles to open channel
        scene.HoaDangModal!.Push("Obj_FisherSampan", Direction.Left);
        scene.HoaDangModal!.Push("Obj_DragonBoat_1", Direction.Down);
        Assert.True(scene.HoaDangModal.IsSolved);

        // 4. Update 100 frames during climax celebration (100 * 0.016f = 1.6s < 3.5s)
        for (int i = 0; i < 100; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }

        // Must trigger progression and wave EXACTLY ONCE
        Assert.Equal(1, completedEventCount);
        Assert.Equal(1, gateEventCount);
        Assert.True(scene.QuestFlags.Hue_Q3_Completed);
        Assert.True(scene.QuestFlags.Hue_Q3_PuzzleSolved);
        Assert.True(scene.QuestFlags.Gate_HoiAn_Unlocked);
        Assert.Equal(100f, scene.BloomEffect.CurrentColorPercent);

        // 5. Advance another 130 frames to pass 3.5s celebration timer, which sets RequestExit = true
        for (int i = 0; i < 130; i++)
        {
            scene.Update(0.016f);
        }
        Assert.Null(scene.HoaDangModal);

        // 6. Test that Cầu Tràng Tiền climax barrier is removed and walking past 1520 exits scene
        scene.Kinematics.Position = new Vector2(1525f, 530f);
        scene.Update(0.016f);
        Assert.True(scene.RequestWorldMap, "Walking past 1520f after Q3 solve must set RequestWorldMap to true.");

        scene.Exit();
    }

    [Fact]
    public void Modal_EarlyExitWithoutSolving_CleansUpProperly()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // 1. Bun Bo modal opened then closed immediately via cancel input
        scene.OpenBunBoAssemblyModal();
        Assert.NotNull(scene.BunBoAssemblyModal);
        input.IsCancelJustPressed = true;
        scene.Update(0.016f);
        input.IsCancelJustPressed = false;
        scene.Update(0.016f);
        Assert.Null(scene.BunBoAssemblyModal);
        Assert.False(scene.QuestFlags.Hue_Q2_Completed);

        // 2. Hoa Dang modal opened then closed immediately via cancel input
        scene.OpenHoaDangRiverModal();
        Assert.NotNull(scene.HoaDangModal);
        input.IsCancelJustPressed = true;
        scene.Update(0.016f);
        input.IsCancelJustPressed = false;
        scene.Update(0.016f);
        Assert.Null(scene.HoaDangModal);
        Assert.False(scene.QuestFlags.Hue_Q3_Completed);

        scene.Exit();
    }

    [Fact]
    public void Modal_ReplayAndReSolve_Stress_ProgressionNeverDuplicates()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        int colorEventCount = 0;
        int completedEventCount = 0;
        int gateEventCount = 0;

        bus.Subscribe<RegionColorUpdatedEvent>(_ => colorEventCount++);
        bus.Subscribe<RegionCompletedEvent>(_ => completedEventCount++);
        bus.Subscribe<GateUnlockedEvent>(_ => gateEventCount++);

        using var scene = new HueExplorationScene(data, eventBus: bus);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        // Pre-complete Q2 and Q3
        data.QuestFlags.Hue_Q2_Completed = true;
        data.QuestFlags.Hue_Q2_BunBoCooked = true;
        data.QuestFlags.Hue_Q3_Completed = true;
        data.QuestFlags.Hue_Q3_PuzzleSolved = true;
        data.QuestFlags.Gate_HoiAn_Unlocked = true;

        // Reset counts to 0 before replay
        colorEventCount = 0;
        completedEventCount = 0;
        gateEventCount = 0;

        // Re-open and re-solve Bun Bo minigame
        scene.OpenBunBoAssemblyModal();
        for (int i = 0; i < 5; i++)
        {
            scene.BunBoAssemblyModal!.PlaceCard(i, i);
        }
        scene.BunBoAssemblyModal!.Validate();
        Assert.True(scene.BunBoAssemblyModal.IsSolved);

        // Modal solve initiates 1 event from minigame Validate
        Assert.Equal(1, colorEventCount);

        // Advance 200 frames of scene.Update to run celebration and exit
        for (int i = 0; i < 200; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }

        // Crucial check: scene.Update across 200 frames MUST NOT increment colorEventCount beyond 1
        Assert.Equal(1, colorEventCount);
        Assert.Null(scene.BunBoAssemblyModal);

        // Re-open and re-solve Hoa Dang minigame
        scene.OpenHoaDangRiverModal();
        scene.HoaDangModal!.Push("Obj_FisherSampan", Direction.Left);
        scene.HoaDangModal!.Push("Obj_DragonBoat_1", Direction.Down);
        Assert.True(scene.HoaDangModal.IsSolved);

        // Modal solve initiates 1 event from minigame TriggerSolve
        Assert.Equal(1, completedEventCount);
        Assert.Equal(1, gateEventCount);

        // Advance 250 frames of scene.Update to run celebration and exit
        for (int i = 0; i < 250; i++)
        {
            scene.Update(0.016f);
            scene.Draw();
        }

        // Crucial check: scene.Update across 250 frames MUST NOT increment events beyond 1
        Assert.Equal(1, completedEventCount);
        Assert.Equal(1, gateEventCount);
        Assert.Null(scene.HoaDangModal);

        scene.Exit();
    }

    [Fact]
    public void RapidModalChurn_50Iterations_NeverLeakingOrThrowing()
    {
        var data = SaveData.CreateDefault();
        var input = new MockInputService();
        using var scene = new HueExplorationScene(data, inputService: input);
        scene.Enter();
        scene.DialogueBox.Dismiss();

        for (int iter = 0; iter < 50; iter++)
        {
            // Open, update 2 frames, exit Bun Bo via cancel
            scene.OpenBunBoAssemblyModal();
            scene.Update(0.016f);
            scene.Draw();
            input.IsCancelJustPressed = true;
            scene.Update(0.016f);
            input.IsCancelJustPressed = false;
            scene.Update(0.016f);
            Assert.Null(scene.BunBoAssemblyModal);

            // Open, update 2 frames, exit Hoa Dang via cancel
            scene.OpenHoaDangRiverModal();
            scene.Update(0.016f);
            scene.Draw();
            input.IsCancelJustPressed = true;
            scene.Update(0.016f);
            input.IsCancelJustPressed = false;
            scene.Update(0.016f);
            Assert.Null(scene.HoaDangModal);
        }

        scene.Exit();
    }

    [Fact]
    public void DisposeWhileModalsActive_NeverThrows()
    {
        // 1. Dispose while Bun Bo is active
        var data1 = SaveData.CreateDefault();
        var scene1 = new HueExplorationScene(data1);
        scene1.Enter();
        scene1.OpenBunBoAssemblyModal();
        scene1.Dispose(); // Should not throw

        // 2. Dispose while Hoa Dang is active
        var data2 = SaveData.CreateDefault();
        var scene2 = new HueExplorationScene(data2);
        scene2.Enter();
        scene2.OpenHoaDangRiverModal();
        scene2.Dispose(); // Should not throw
    }
}
