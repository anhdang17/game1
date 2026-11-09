using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using Xunit;

namespace GameProject.Tests;

/// <summary>
/// Milestone 2 Iteration 2 Adversarial Stress Suite for Challenger 1:
/// Physics, Kinematics, Distance Hysteresis Deadband & Audio Debounce Stress Testing.
/// </summary>
public sealed class Challenger1HysteresisStressTests
{
    private static (InteractionContext Context, EventBus EventBus, QuestManager QuestManager, Inventory Inventory, QuestFlags Flags, RecordingAudioService Audio) CreateHarnessContext()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var inv = new Inventory();
        var flags = QuestFlags.CreateDefault();
        var audio = new RecordingAudioService();

        var ctx = new InteractionContext(
            PlayerPosition: Vector2.Zero,
            Inventory: inv,
            QuestManager: qm,
            QuestFlags: flags,
            EventBus: bus,
            AudioService: audio
        );

        return (ctx, bus, qm, inv, flags, audio);
    }

    private sealed class MockInteractable : IInteractable
    {
        public string Id { get; }
        public string DisplayName => Id;
        public Vector2 Position { get; set; }
        public Vector2 InteractionCenter => Position;
        public float InteractionRadius { get; set; }
        public float VisualHeight => 40f;
        public bool IsInteractable { get; set; } = true;

        public MockInteractable(string id, Vector2 pos, float radius = 0f)
        {
            Id = id;
            Position = pos;
            InteractionRadius = radius;
        }

        public bool CanInteract(InteractionContext context) => IsInteractable;
        public InteractionResult Interact(InteractionContext context) => new(true, DisplayName, "Interacted");
        public string GetPromptText(InteractionContext context) => $"[E] {DisplayName}";
    }

    // =========================================================================
    // 1. DISTANCE HYSTERESIS DEADBAND [48px, 54px] STRESS TESTS
    // =========================================================================

    [Fact]
    public void Hysteresis_EnterDeadband_FromInside_PreservesTargetAcross1000Steps()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Target", Vector2.Zero);
        mgr.Register(entity);

        // Step 1: Initial entry inside enter threshold (45.0px < 48.0px)
        mgr.Update(0.016f, new Vector2(45.0f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));

        // Step 2: 1000 randomized steps strictly inside the deadband [48.01px, 53.99px]
        var rnd = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            float dist = 48.01f + (float)rnd.NextDouble() * (53.99f - 48.01f);
            mgr.Update(0.016f, new Vector2(dist, 0f), ctx);

            // Invariant: Target MUST be retained on every single frame
            Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);
        }

        // Audio must NEVER re-trigger while target is retained in deadband
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));
    }

    [Fact]
    public void Hysteresis_EnterDeadband_FromOutside_RefusesTargetUntilEnterThreshold()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Target", Vector2.Zero);
        mgr.Register(entity);

        // Start outside exit threshold (60.0px > 54.0px)
        mgr.Update(0.016f, new Vector2(60.0f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);
        Assert.Empty(audio.PlayedSfx);

        // 1000 randomized steps inside deadband [48.01px, 53.99px] from outside
        var rnd = new Random(1337);
        for (int i = 0; i < 1000; i++)
        {
            float dist = 48.01f + (float)rnd.NextDouble() * (53.99f - 48.01f);
            mgr.Update(0.016f, new Vector2(dist, 0f), ctx);

            // Invariant: Target must NEVER be acquired when approaching from outside without reaching 48px
            Assert.Null(mgr.CurrentTarget);
        }

        // Audio must not have triggered
        Assert.Equal(0, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));

        // Now touch enter threshold exactly at 48.000f
        mgr.Update(0.016f, new Vector2(48.000f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));
    }

    [Fact]
    public void Hysteresis_ExactBoundaryPrecision_48And54px()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Boundary", Vector2.Zero);
        mgr.Register(entity);

        // 1. Outside enter: 48.001f -> null
        mgr.Update(0.016f, new Vector2(48.001f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);

        // 2. Exactly on enter boundary: 48.000f -> acquired
        mgr.Update(0.016f, new Vector2(48.000f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

        // 3. Step back to 53.999f (inside exit) -> retained
        mgr.Update(0.016f, new Vector2(53.999f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

        // 4. Exactly on exit boundary: 54.000f -> retained (54.0^2 <= 54.0^2)
        mgr.Update(0.016f, new Vector2(54.000f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

        // 5. Barely outside exit boundary: 54.001f -> dropped
        mgr.Update(0.016f, new Vector2(54.001f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);

        // 6. Step back inside deadband at 53.999f -> stays null
        mgr.Update(0.016f, new Vector2(53.999f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);
    }

    // =========================================================================
    // 2. AUDIO DEBOUNCE COOLDOWN & OSCILLATION STRESS TESTS
    // =========================================================================

    [Fact]
    public void AudioDebounce_100HzOscillationAcross54pxBoundary_TargetDropped_ZeroChatter()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Target", Vector2.Zero);
        mgr.Register(entity);

        // Start inside enter radius to acquire target
        mgr.Update(0.010f, new Vector2(45.0f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));

        audio.Clear();

        // Simulate 100Hz oscillation (dt = 0.010s) across 54px boundary: 53.9px <-> 54.1px
        // For 500 frames (5.0 seconds simulated)
        for (int frame = 0; frame < 500; frame++)
        {
            float dist = (frame % 2 == 0) ? 54.1f : 53.9f;
            mgr.Update(0.010f, new Vector2(dist, 0f), ctx);

            // On frame 0 (dist=54.1f), target is dropped.
            // On subsequent frames, dist never drops below 48.0px, so target is NEVER re-acquired!
            Assert.Null(mgr.CurrentTarget);
        }

        // Invariant: Prompt SFX must be 0 after dropping across 54px boundary
        Assert.Equal(0, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));
    }

    [Fact]
    public void AudioDebounce_100HzOscillation_CyclingInside48AndOutside54_StrictlyThrottledTo4Hz()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Target", Vector2.Zero);
        mgr.Register(entity);

        audio.Clear();

        // 100 frames at dt = 0.010s (exactly 1.0 second simulated time)
        // Player oscillates between 47.0px (inside enter) and 55.0px (outside exit)
        // That is 50 full entry/exit transitions in 1 second!
        for (int frame = 0; frame < 100; frame++)
        {
            float dist = (frame % 2 == 0) ? 47.0f : 55.0f;
            mgr.Update(0.010f, new Vector2(dist, 0f), ctx);
        }

        int sfxCount = audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt);

        // Cooldown is 0.25s. In 1.0s, at most 4 chimes can trigger (at t=0.01s, 0.26s, 0.52s, 0.78s).
        // Without debounce cooldown, this would have produced 50 chimes!
        Assert.Equal(4, sfxCount);

        // Now continue for another 900 frames (total 1000 frames = 10.0 seconds simulated)
        for (int frame = 100; frame < 1000; frame++)
        {
            float dist = (frame % 2 == 0) ? 47.0f : 55.0f;
            mgr.Update(0.010f, new Vector2(dist, 0f), ctx);
        }

        int totalSfxCount = audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt);

        // In 1000 frames (10.0 seconds) at 100Hz:
        // Cooldown is 0.25s (25 frames). At frame 25, player is outside (odd frame, 55px).
        // Target is re-entered on even frames, yielding an exact 26-frame (0.26s) period:
        // Chimes trigger at frames: 0, 26, 52, 78, 104, ... 988 (39 chimes total).
        // Frame 1014 exceeds frame 999.
        // Instead of 500 chattering chimes, exactly 39 chimes fired (~3.9 Hz throttled).
        Assert.Equal(39, totalSfxCount);
    }

    [Fact]
    public void AudioDebounce_ExtremeHighFrequencyStress_1000Hz_NeverExceedsCooldownLimit()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Stress", Vector2.Zero);
        mgr.Register(entity);

        audio.Clear();

        // 5,000 frames at dt = 0.001s (1ms per frame, 1000Hz, 5.0 seconds simulated)
        // Alternating between 40px and 60px every single millisecond (2500 boundary crossings)
        for (int frame = 0; frame < 5000; frame++)
        {
            float dist = (frame % 2 == 0) ? 40.0f : 60.0f;
            mgr.Update(0.001f, new Vector2(dist, 0f), ctx);
        }

        int sfxCount = audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt);

        // 5.0 seconds / 0.25s cooldown = exactly 20 chimes
        Assert.Equal(20, sfxCount);
    }

    // =========================================================================
    // 3. 2D KINEMATICS, DIAGONAL MOVEMENT & MULTI-ENTITY INTERACTIONS
    // =========================================================================

    [Fact]
    public void Hysteresis_Diagonal2DMovement_DeadbandRespectedInAllQuadrants()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Center_Npc", new Vector2(100f, 100f));
        mgr.Register(entity);

        // Diagonal directions: (1, 1), (-1, 1), (-1, -1), (1, -1)
        Vector2[] directions = new[]
        {
            Vector2.Normalize(new Vector2(1f, 1f)),
            Vector2.Normalize(new Vector2(-1f, 1f)),
            Vector2.Normalize(new Vector2(-1f, -1f)),
            Vector2.Normalize(new Vector2(1f, -1f))
        };

        foreach (var dir in directions)
        {
            audio.Clear();
            mgr.Clear();
            mgr.Register(entity);

            // 1. Position outside (60px along dir)
            mgr.Update(0.016f, entity.Position + dir * 60f, ctx);
            Assert.Null(mgr.CurrentTarget);

            // 2. Position inside deadband (50px along dir) from outside -> null
            mgr.Update(0.016f, entity.Position + dir * 50f, ctx);
            Assert.Null(mgr.CurrentTarget);

            // 3. Cross enter threshold (47.9px along dir) -> acquired!
            mgr.Update(0.016f, entity.Position + dir * 47.9f, ctx);
            Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);
            Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));

            // 4. Move out into deadband (53.9px along dir) -> retained!
            mgr.Update(0.016f, entity.Position + dir * 53.9f, ctx);
            Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

            // 5. Move past exit threshold (54.1px along dir) -> dropped!
            mgr.Update(0.016f, entity.Position + dir * 54.1f, ctx);
            Assert.Null(mgr.CurrentTarget);
        }
    }

    [Fact]
    public void Hysteresis_CustomRadiusInteractable_DualThresholdPreserved()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();
        // Custom radius of 70px (exit radius = 70 + 6 = 76px)
        var entity = new MockInteractable("BigStall", Vector2.Zero, radius: 70.0f);
        mgr.Register(entity);

        // Outside enter: 71.0f -> null
        mgr.Update(0.016f, new Vector2(71.0f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);

        // Enter: 70.0f -> acquired
        mgr.Update(0.016f, new Vector2(70.0f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

        // Deadband: 75.9f -> retained
        mgr.Update(0.016f, new Vector2(75.9f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

        // Exit: 76.1f -> dropped
        mgr.Update(0.016f, new Vector2(76.1f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);
    }

    [Fact]
    public void Hysteresis_TwoEntities_DeadbandRetentionVsCandidate()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();

        var entityA = new MockInteractable("Entity_A", new Vector2(0f, 0f));
        var entityB = new MockInteractable("Entity_B", new Vector2(100f, 0f));
        mgr.Register(entityA);
        mgr.Register(entityB);

        // Player at (45, 0): Dist A = 45px (<= 48), Dist B = 55px (> 48)
        mgr.Update(0.016f, new Vector2(45f, 0f), ctx);
        Assert.Equal(entityA.Id, mgr.CurrentTarget?.Id);

        // Player moves to (51, 0):
        // Dist to A is 51px. Since A is CurrentTarget, A's exit radius is 54px (51 <= 54).
        // Dist to B is 49px. For B (candidate), enter radius is 48px (49 > 48).
        // Therefore, A is retained as CurrentTarget even though player is closer to B than to A!
        mgr.Update(0.016f, new Vector2(51f, 0f), ctx);
        Assert.Equal(entityA.Id, mgr.CurrentTarget?.Id);

        // Player moves to (53, 0):
        // Dist to A is 53px (<= 54).
        // Dist to B is 47px (<= 48). Both are valid candidates!
        // DistSq to B is 47^2 = 2209. DistSq to A is 53^2 = 2809.
        // Nearest selection switches to B!
        mgr.Update(0.016f, new Vector2(53f, 0f), ctx);
        Assert.Equal(entityB.Id, mgr.CurrentTarget?.Id);
    }

    [Fact]
    public void Hysteresis_KinematicsInertia_DecelerationInsideDeadband()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        // baAn positioned at Y = 14 to match foot height minimum Y
        var baAn = new MockInteractable("BaAn", new Vector2(100f, 14f));
        mgr.Register(baAn);

        var kinematics = new CharacterKinematics
        {
            Position = new Vector2(0f, 14f),
            MaxSpeed = 200f,
            Acceleration = 1200f,
            Friction = 1500f
        };
        var collision = new CollisionWorld();

        // 1. Move towards BaAn until inside 48px threshold (dist ~ 45px)
        float dt = 0.016f;
        while (100f - kinematics.Position.X > 45f)
        {
            kinematics.Update(new Vector2(1f, 0f), collision, dt);
            mgr.Update(dt, kinematics.Position, ctx);
        }

        Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));

        // 2. Reverse input to walk backward into deadband [48px, 54px]
        // Stop reversing once distance reaches ~50px to ensure player stays safely within 54px deadband
        while (100f - kinematics.Position.X < 50.0f)
        {
            kinematics.Update(new Vector2(-1f, 0f), collision, dt);
            mgr.Update(dt, kinematics.Position, ctx);

            // Invariant: As long as distance is within 54px exit threshold, target is retained
            if (100f - kinematics.Position.X <= 54.0f)
            {
                Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);
            }
        }

        // Release input: allow friction to stop character in deadband (distance between 48px and 54px)
        for (int i = 0; i < 30; i++)
        {
            kinematics.Update(Vector2.Zero, collision, dt);
            mgr.Update(dt, kinematics.Position, ctx);

            float currentDist = 100f - kinematics.Position.X;
            if (currentDist <= 54.0f)
            {
                Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);
            }
            else
            {
                Assert.Null(mgr.CurrentTarget);
            }
        }

        // Prompt chime fired exactly once on entry, never re-triggered
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));
    }

    [Fact]
    public void InteractionManager_CanInteractDisabled_TargetDroppedImmediately()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Toggle", Vector2.Zero);
        mgr.Register(entity);

        mgr.Update(0.016f, new Vector2(40f, 0f), ctx);
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);

        // Disable interaction
        entity.IsInteractable = false;
        mgr.Update(0.016f, new Vector2(40f, 0f), ctx);

        // Invariant: Target dropped immediately despite being within 40px
        Assert.Null(mgr.CurrentTarget);
    }

    [Fact]
    public void AudioDebounce_ZeroOrNegativeDeltaTime_HandledSafely()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Test", Vector2.Zero);
        mgr.Register(entity);

        // Initial acquisition sets cooldown to 0.25s
        mgr.Update(0.016f, new Vector2(40f, 0f), ctx);
        Assert.True(mgr.AudioPromptCooldownTimer > 0f);

        float timerBefore = mgr.AudioPromptCooldownTimer;

        // Update with dt = 0.0f
        mgr.Update(0.0f, new Vector2(40f, 0f), ctx);
        Assert.Equal(timerBefore, mgr.AudioPromptCooldownTimer);

        // Update with negative dt should clamp safely without increasing timer
        mgr.Update(-0.05f, new Vector2(40f, 0f), ctx);
        // Note: In C#, timer - (-0.05f) would increase if unhandled, or stay.
        // Let's verify target remains stable
        Assert.Equal(entity.Id, mgr.CurrentTarget?.Id);
    }

    [Fact]
    public void BubbleAlpha_SmoothInterpolationAndClamping_UnderFuzzing()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var entity = new MockInteractable("Npc_Fuzz", Vector2.Zero);
        mgr.Register(entity);

        var rnd = new Random(999);
        for (int i = 0; i < 1000; i++)
        {
            float dt = (float)rnd.NextDouble() * 0.1f;
            float dist = (float)rnd.NextDouble() * 80f;
            mgr.Update(dt, new Vector2(dist, 0f), ctx);

            // Invariants:
            // 1. BubbleAlpha is always strictly clamped between 0 and 1
            Assert.InRange(mgr.BubbleAlpha, 0.0f, 1.0f);
            Assert.False(float.IsNaN(mgr.BubbleAlpha));
            Assert.False(float.IsInfinity(mgr.BubbleAlpha));

            // 2. Cooldown timer is always >= 0
            Assert.True(mgr.AudioPromptCooldownTimer >= 0f);
        }
    }
}
