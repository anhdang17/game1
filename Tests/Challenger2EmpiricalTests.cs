using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Graphics;
using GameProject.UI;
using Xunit;

namespace GameProject.Tests;

/// <summary>
/// Adversarial Empirical Challenger Suite for Milestone 2:
/// 1. Interaction FSM & Race Conditions (Spamming, consecutive frames, re-entrancy)
/// 2. Proximity Boundary Oscillations (48px boundary crossing, jitter, audio chattering)
/// 3. Inventory State Integrity (Idempotency, aliases, out-of-order gathering, missing item gating)
/// 4. Quest Progression Gating (Premature state changes, prerequisites, objective clamps)
/// 5. Vietnamese Unicode Font Codepoint Completeness (All 134 vowels with tones, phrases, NFD vs NFC)
/// </summary>
public class Challenger2EmpiricalTests
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

    // =========================================================================
    // 1. INTERACTION FSM & SPAMMING STRESS TESTS
    // =========================================================================

    [Fact]
    public void InteractionSpam_BaAnNpc_10000RapidCalls_NeverCorruptsState()
    {
        var (ctx, bus, qm, _, flags, audio) = CreateHarnessContext();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();

        int questStartedEvents = 0;
        bus.Subscribe<QuestStartedEvent>(_ => questStartedEvents++);

        // Spam 10,000 interactions on Bà An
        for (int i = 0; i < 10_000; i++)
        {
            var res = baAn.Interact(ctx);
            Assert.True(res.Success);

            if (i == 0)
            {
                Assert.True(res.QuestStarted);
                Assert.True(flags.Q1_Started);
                Assert.True(qm.IsQuestInProgress(HanoiQuests.Quest1Id));
            }
            else
            {
                // Subsequent calls must be safe in-progress reminders
                Assert.False(res.QuestStarted);
                Assert.False(res.QuestCompleted);
                Assert.Contains("0/5", res.DialogueText);
            }
        }

        // Exactly one quest start event published
        Assert.Equal(1, questStartedEvents);
        Assert.True(flags.Q1_Started);
        Assert.False(flags.Q1_Completed);
    }

    [Fact]
    public void InteractionSpam_Merchant_10000RapidCalls_GrantsExactlyOnce()
    {
        var (ctx, bus, qm, inv, flags, audio) = CreateHarnessContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        baAn.Interact(ctx); // Start Q1

        var merchant = merchants.First(m => m.ItemId == ItemIds.BanhPho);
        int itemCollectedEvents = 0;
        bus.Subscribe<ItemCollectedEvent>(_ => itemCollectedEvents++);

        // 10,000 rapid interactions
        for (int i = 0; i < 10_000; i++)
        {
            var res = merchant.Interact(ctx);
            Assert.True(res.Success);

            if (i == 0)
            {
                Assert.Equal(ItemIds.BanhPho, res.GrantedItemId);
                Assert.Equal(1, inv.Count);
                Assert.True(inv.HasItem(ItemIds.BanhPho));
            }
            else
            {
                Assert.Null(res.GrantedItemId);
                Assert.Equal(1, inv.Count);
            }
        }

        Assert.Equal(1, itemCollectedEvents);
        Assert.Equal(1, qm.GetQuest(HanoiQuests.Quest1Id)?.GetObjective(ItemIds.BanhPho)?.CurrentCount);
        Assert.True(flags.Q1_BanhPho_Collected);
    }

    [Fact]
    public void InteractionSpam_PreQuest_10000Calls_NeverGrantsOrProgresses()
    {
        var (ctx, bus, qm, inv, flags, _) = CreateHarnessContext();
        var (_, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        var merchant = merchants.First(m => m.ItemId == ItemIds.NuocDung);

        int itemCollectedEvents = 0;
        bus.Subscribe<ItemCollectedEvent>(_ => itemCollectedEvents++);

        for (int i = 0; i < 10_000; i++)
        {
            var res = merchant.Interact(ctx);
            Assert.True(res.Success);
            Assert.Null(res.GrantedItemId);
            Assert.Equal(0, inv.Count);
        }

        Assert.Equal(0, itemCollectedEvents);
        Assert.False(flags.Q1_Started);
        Assert.False(flags.Q1_NuocDung_Collected);
        Assert.Equal(0, qm.GetQuest(HanoiQuests.Quest1Id)?.GetObjective(ItemIds.NuocDung)?.CurrentCount);
    }

    [Fact]
    public void InteractionSpam_TurnIn_10000Calls_CompletesOnce_IdempotentAfterwards()
    {
        var (ctx, bus, qm, inv, flags, audio) = CreateHarnessContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        baAn.Interact(ctx); // Start Q1

        // Collect all 5
        foreach (var m in merchants)
        {
            m.Interact(ctx);
        }

        int questCompletedEvents = 0;
        bus.Subscribe<QuestCompletedEvent>(_ => questCompletedEvents++);

        for (int i = 0; i < 10_000; i++)
        {
            var res = baAn.Interact(ctx);
            Assert.True(res.Success);

            if (i == 0)
            {
                Assert.True(res.QuestCompleted);
                Assert.True(flags.Q1_Completed);
                Assert.True(qm.IsQuestCompleted(HanoiQuests.Quest1Id));
            }
            else
            {
                Assert.False(res.QuestCompleted);
                Assert.True(flags.Q1_Completed);
                Assert.Contains("ghép bát phở chuẩn vị", res.DialogueText);
            }
        }

        Assert.Equal(1, questCompletedEvents);
        Assert.True(qm.CanStartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void InteractionFuzz_RandomActionLoop_PreservesInvariants()
    {
        var (ctx, bus, qm, inv, flags, _) = CreateHarnessContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        var rnd = new Random(1337);

        // Run 2,000 randomized steps
        for (int step = 0; step < 2_000; step++)
        {
            int action = rnd.Next(7);
            if (action == 0)
            {
                baAn.Interact(ctx);
            }
            else if (action <= 5)
            {
                merchants[action - 1].Interact(ctx);
            }
            else
            {
                // Query state
                _ = baAn.GetPromptText(ctx);
                _ = merchants[rnd.Next(merchants.Count)].GetPromptText(ctx);
            }

            // Invariants:
            // 1. Inventory count never exceeds 5
            Assert.InRange(inv.Count, 0, 5);

            // 2. If Q1 not started, inventory is empty
            if (!flags.Q1_Started)
            {
                Assert.Equal(0, inv.Count);
            }

            // 3. If Q1 completed, inventory must have all 5
            if (flags.Q1_Completed)
            {
                Assert.Equal(5, inv.Count);
                Assert.True(flags.AreAllQ1IngredientsCollected());
            }
        }
    }

    // =========================================================================
    // 2. PROXIMITY BOUNDARY OSCILLATIONS & JITTER
    // =========================================================================

    [Fact]
    public void ProximityOscillation_RapidBoundaryCrossing_CausesAudioChatteringVulnerability()
    {
        // Verified Fix for Proximity Flutter & Audio Chattering:
        // When player hovers on the 48.0px boundary (e.g. 47.99px vs 48.01px),
        // dual-threshold hysteresis (enter at 48px, exit at 54px) retains CurrentTarget.
        // Prompt SFX triggers exactly once instead of 30 times.
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();
        mgr.Register(baAn);

        Vector2 inside = baAn.InteractionCenter + new Vector2(47.99f, 0f);
        Vector2 outside = baAn.InteractionCenter + new Vector2(48.01f, 0f);

        audio.Clear();

        // Simulate 60 frames oscillating in/out
        for (int frame = 0; frame < 60; frame++)
        {
            Vector2 pos = (frame % 2 == 0) ? inside : outside;
            mgr.Update(0.016f, pos, ctx);
        }

        int promptSfxCount = audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt);

        // Verification of fix: prompt triggered only once on initial entry
        Assert.Equal(1, promptSfxCount);
    }

    [Fact]
    public void ProximityOscillation_OuterBoundaryCrossing_AudioCooldownDebouncesToMaxRate()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();
        mgr.Register(baAn);

        // Oscillate across the outer 54px exit threshold (47.99px inside enter, 54.01px outside exit)
        Vector2 inside = baAn.InteractionCenter + new Vector2(47.99f, 0f);
        Vector2 outside = baAn.InteractionCenter + new Vector2(54.01f, 0f);

        audio.Clear();

        // 60 frames at 0.016s (~0.96s total)
        for (int frame = 0; frame < 60; frame++)
        {
            Vector2 pos = (frame % 2 == 0) ? inside : outside;
            mgr.Update(0.016f, pos, ctx);
        }

        int promptSfxCount = audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt);

        // With cooldown of 0.25s, in 0.96s at 60Hz oscillation:
        // Triggers at frame 0 (t=0.016), frame 16 (t=0.272), frame 32 (t=0.528), frame 48 (t=0.784)
        // Exactly 4 chimes instead of 30 chattering chimes.
        Assert.Equal(4, promptSfxCount);
    }

    [Fact]
    public void ProximityHysteresis_EnterAt48px_ExitAt54px_DeadbandBehavior()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();
        mgr.Register(baAn);

        // 1. Outside enter threshold (50px > 48px) -> Target null
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(50.0f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);

        // 2. Cross enter threshold (48.0px <= 48px) -> Target acquired
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(48.0f, 0f), ctx);
        Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);

        // 3. Step back to 50.0px (inside 54px exit threshold) -> Target RETAINED
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(50.0f, 0f), ctx);
        Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);

        // 4. Step back to 53.9px (still inside 54px exit threshold) -> Target RETAINED
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(53.9f, 0f), ctx);
        Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);

        // 5. Cross exit threshold (54.1px > 54px) -> Target DROPPED
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(54.1f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);

        // 6. Step back in to 50.0px (inside 54px exit, but outside 48px enter) -> Target NOT re-acquired
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(50.0f, 0f), ctx);
        Assert.Null(mgr.CurrentTarget);

        // 7. Step back in to 47.9px (inside 48px enter) -> Target re-acquired
        mgr.Update(0.016f, baAn.InteractionCenter + new Vector2(47.9f, 0f), ctx);
        Assert.Equal(baAn.Id, mgr.CurrentTarget?.Id);
    }

    [Fact]
    public void ProximityOscillation_EquidistantEntities_RapidTargetFlipping()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();

        var entityLeft = new PhoIngredientMerchant(
            "Stall_Left", "Left", ItemIds.BanhPho, "Bánh phở",
            new Vector2(-30f, 0f), "g", "r", "p"
        );
        var entityRight = new PhoIngredientMerchant(
            "Stall_Right", "Right", ItemIds.ThitBo, "Thịt bò",
            new Vector2(30f, 0f), "g", "r", "p"
        );

        mgr.Register(entityLeft);
        mgr.Register(entityRight);

        // Oscillate player slightly left and right of midpoint
        for (int frame = 0; frame < 20; frame++)
        {
            Vector2 playerPos = (frame % 2 == 0) ? new Vector2(-0.01f, 0f) : new Vector2(0.01f, 0f);
            mgr.Update(0.016f, playerPos, ctx);

            if (frame % 2 == 0)
            {
                Assert.Equal("Stall_Left", mgr.CurrentTarget?.Id);
            }
            else
            {
                Assert.Equal("Stall_Right", mgr.CurrentTarget?.Id);
            }
        }
    }

    [Fact]
    public void Proximity_ExactBoundaryFloatPrecision_EvaluatesWithinRadius()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();
        mgr.Register(baAn);

        // Distance squared comparison: exactly 48.0f
        Vector2 exactBoundary = baAn.InteractionCenter + new Vector2(48.0000f, 0f);
        var targetExact = mgr.FindNearestInteractable(exactBoundary, ctx);
        Assert.NotNull(targetExact);
        Assert.Equal(baAn.Id, targetExact.Id);

        // Just outside: 48.001f
        Vector2 outsideBoundary = baAn.InteractionCenter + new Vector2(48.001f, 0f);
        var targetOutside = mgr.FindNearestInteractable(outsideBoundary, ctx);
        Assert.Null(targetOutside);
    }

    [Fact]
    public void Proximity_ZeroOrNegativeRadius_DefaultsSafelyTo48px()
    {
        var (ctx, _, _, _, _, _) = CreateHarnessContext();
        var mgr = new InteractionManager();

        var zeroRadiusEntity = new TestRadiusInteractable("ZeroRadius", Vector2.Zero, radius: 0f);
        var negRadiusEntity = new TestRadiusInteractable("NegRadius", new Vector2(100f, 0f), radius: -10f);

        mgr.Register(zeroRadiusEntity);
        mgr.Register(negRadiusEntity);

        // Player at (40, 0) is within 48px default radius of ZeroRadius at (0, 0)
        var target1 = mgr.FindNearestInteractable(new Vector2(40f, 0f), ctx);
        Assert.NotNull(target1);
        Assert.Equal("ZeroRadius", target1.Id);

        // Player at (140, 0) is within 48px default radius of NegRadius at (100, 0)
        var target2 = mgr.FindNearestInteractable(new Vector2(140f, 0f), ctx);
        Assert.NotNull(target2);
        Assert.Equal("NegRadius", target2.Id);
    }

    private sealed class TestRadiusInteractable : IInteractable
    {
        public string Id { get; }
        public string DisplayName => Id;
        public Vector2 Position { get; }
        public Vector2 InteractionCenter => Position;
        public float InteractionRadius { get; }
        public float VisualHeight => 40f;

        public TestRadiusInteractable(string id, Vector2 pos, float radius)
        {
            Id = id;
            Position = pos;
            InteractionRadius = radius;
        }

        public bool CanInteract(InteractionContext context) => true;
        public InteractionResult Interact(InteractionContext context) => new(true, DisplayName, "Interacted");
        public string GetPromptText(InteractionContext context) => "[E] Test";
    }

    // =========================================================================
    // 3. INVENTORY STATE INTEGRITY & IDEMPOTENCY
    // =========================================================================

    [Fact]
    public void Inventory_DuplicateItemAdd_StrictlyRejected()
    {
        var inv = new Inventory();
        foreach (var item in ItemIds.AllPhoIngredients)
        {
            Assert.True(inv.AddItem(item));
            Assert.False(inv.AddItem(item)); // 2nd attempt rejected
            Assert.False(inv.AddItem(item.ToLowerInvariant())); // Case-insensitive duplicate rejected
            Assert.False(inv.AddItem(item.ToUpperInvariant())); // Case-insensitive duplicate rejected
        }

        Assert.Equal(5, inv.Count);
    }

    [Fact]
    public void Inventory_OutOfOrderGathering_AllFiveItems_Succeeds()
    {
        var (ctx, _, qm, inv, flags, _) = CreateHarnessContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        baAn.Interact(ctx); // Start Q1

        // Gather in reverse order: ThaoMoc (idx 4), HanhLa (idx 3), NuocDung (idx 2), ThitBo (idx 1), BanhPho (idx 0)
        merchants[4].Interact(ctx);
        Assert.Equal(1, inv.Count);
        Assert.True(flags.Q1_ThaoMoc_Collected);

        merchants[2].Interact(ctx);
        Assert.Equal(2, inv.Count);
        Assert.True(flags.Q1_NuocDung_Collected);

        merchants[0].Interact(ctx);
        Assert.Equal(3, inv.Count);
        Assert.True(flags.Q1_BanhPho_Collected);

        merchants[3].Interact(ctx);
        Assert.Equal(4, inv.Count);
        Assert.True(flags.Q1_HanhLa_Collected);

        merchants[1].Interact(ctx);
        Assert.Equal(5, inv.Count);
        Assert.True(flags.Q1_ThitBo_Collected);

        Assert.True(flags.AreAllQ1IngredientsCollected());

        // Bà An turn in succeeds
        var turnIn = baAn.Interact(ctx);
        Assert.True(turnIn.Success);
        Assert.True(turnIn.QuestCompleted);
        Assert.True(flags.Q1_Completed);
    }

    [Theory]
    [InlineData(ItemIds.BanhPho)]
    [InlineData(ItemIds.ThitBo)]
    [InlineData(ItemIds.NuocDung)]
    [InlineData(ItemIds.HanhLa)]
    [InlineData(ItemIds.ThaoMoc)]
    public void Inventory_MissingOneItem_BaAnCannotCompleteQuest_AllFiveVariations(string missingItemId)
    {
        var (ctx, _, qm, inv, flags, _) = CreateHarnessContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        baAn.Interact(ctx); // Start Q1

        // Gather 4 items, skipping missingItemId
        foreach (var m in merchants)
        {
            if (m.ItemId != missingItemId)
            {
                m.Interact(ctx);
            }
        }

        Assert.Equal(4, inv.Count);
        Assert.False(flags.AreAllQ1IngredientsCollected());
        Assert.Equal(4, flags.GetQ1CollectedCount());

        // Attempt turn in
        var turnIn = baAn.Interact(ctx);
        Assert.True(turnIn.Success);
        Assert.False(turnIn.QuestCompleted);
        Assert.False(flags.Q1_Completed);
        Assert.Contains("4/5", turnIn.DialogueText);
        Assert.False(qm.CanStartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void Inventory_ThaoMocAndGiaVi_AliasSymmetry_Verified()
    {
        var inv = new Inventory();

        inv.AddItem(ItemIds.ThaoMoc);
        Assert.True(inv.HasItem(ItemIds.ThaoMoc));
        Assert.True(inv.HasItem(ItemIds.GiaVi)); // HasItem returns true due to alias check

        // Idempotent: adding GiaVi when ThaoMoc is present is rejected
        bool addedGiaVi = inv.AddItem(ItemIds.GiaVi);
        Assert.False(addedGiaVi);
        Assert.Equal(1, inv.Count);

        // Symmetric: RemoveItem("Item_GiaVi") removes the alias (ThaoMoc)
        bool removed = inv.RemoveItem(ItemIds.GiaVi);
        Assert.True(removed);
        Assert.Equal(0, inv.Count);
        Assert.False(inv.HasItem(ItemIds.ThaoMoc));
        Assert.False(inv.HasItem(ItemIds.GiaVi));

        // Symmetric reverse order: Add GiaVi, reject ThaoMoc, remove ThaoMoc
        inv.AddItem(ItemIds.GiaVi);
        Assert.False(inv.AddItem(ItemIds.ThaoMoc));
        Assert.Equal(1, inv.Count);
        Assert.True(inv.RemoveItem(ItemIds.ThaoMoc));
        Assert.Equal(0, inv.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Inventory_InvalidItemStrings_SafelyHandled(string? invalidItem)
    {
        var inv = new Inventory();
        Assert.False(inv.AddItem(invalidItem!));
        Assert.False(inv.HasItem(invalidItem!));
        Assert.False(inv.RemoveItem(invalidItem!));
        Assert.Equal(0, inv.Count);
    }

    // =========================================================================
    // 4. QUEST PROGRESSION GATING & STATE INTEGRITY
    // =========================================================================

    [Fact]
    public void QuestProgression_PrematureQuest1Complete_Rejected()
    {
        var qm = new QuestManager();
        HanoiQuests.RegisterAll(qm);

        // Q1 not started yet
        Assert.False(qm.CompleteQuest(HanoiQuests.Quest1Id));
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(HanoiQuests.Quest1Id));

        // Q1 started but 0 items
        qm.StartQuest(HanoiQuests.Quest1Id);
        Assert.False(qm.CompleteQuest(HanoiQuests.Quest1Id));
        Assert.Equal(QuestState.InProgress, qm.GetQuestState(HanoiQuests.Quest1Id));

        // Q1 with only 4 items
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThitBo);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.NuocDung);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.HanhLa);

        Assert.False(qm.CompleteQuest(HanoiQuests.Quest1Id));
        Assert.Equal(QuestState.InProgress, qm.GetQuestState(HanoiQuests.Quest1Id));
    }

    [Fact]
    public void QuestProgression_PrematureQuest2Start_RejectedWhenQ1RequirementsMetBeforeTurnIn()
    {
        var qm = new QuestManager();
        HanoiQuests.RegisterAll(qm);

        qm.StartQuest(HanoiQuests.Quest1Id);

        // Record all 5 objectives -> state becomes RequirementsMet
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThitBo);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.NuocDung);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.HanhLa);
        qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThaoMoc);

        Assert.Equal(QuestState.RequirementsMet, qm.GetQuestState(HanoiQuests.Quest1Id));

        // Quest 2 CANNOT start yet because Quest 1 is RequirementsMet, not Completed
        Assert.False(qm.CanStartQuest(HanoiQuests.Quest2Id));
        Assert.False(qm.StartQuest(HanoiQuests.Quest2Id));
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(HanoiQuests.Quest2Id));

        // Only after completing Quest 1 does Quest 2 become startable
        Assert.True(qm.CompleteQuest(HanoiQuests.Quest1Id));
        Assert.True(qm.CanStartQuest(HanoiQuests.Quest2Id));
        Assert.True(qm.StartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void QuestProgression_PrematureQuest3Start_RejectedWhenQ2NotCompleted()
    {
        var qm = new QuestManager();
        HanoiQuests.RegisterAll(qm);

        // Even if Q1 completed, Q3 cannot start without Q2
        qm.StartQuest(HanoiQuests.Quest1Id);
        foreach (var item in ItemIds.AllPhoIngredients)
        {
            qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, item);
        }
        qm.CompleteQuest(HanoiQuests.Quest1Id);

        Assert.False(qm.CanStartQuest(HanoiQuests.Quest3Id));
        Assert.False(qm.StartQuest(HanoiQuests.Quest3Id));
    }

    [Fact]
    public void QuestProgression_ObjectiveIncrement_ClampsAtTarget_DoesNotOverflow()
    {
        var qm = new QuestManager();
        HanoiQuests.RegisterAll(qm);
        qm.StartQuest(HanoiQuests.Quest1Id);

        var obj = qm.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.BanhPho)!;

        // Try massive increment
        bool changed1 = qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho, int.MaxValue);
        Assert.True(changed1);
        Assert.Equal(1, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Further increments return false
        bool changed2 = qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho, 1);
        Assert.False(changed2);
        Assert.Equal(1, obj.CurrentCount);

        // Negative increments return false
        bool changed3 = qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThitBo, -5);
        Assert.False(changed3);
        Assert.Equal(0, qm.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.ThitBo)!.CurrentCount);
    }

    [Fact]
    public void QuestProgression_HydrateFromFlags_RoundtripIntegrity()
    {
        var qm1 = new QuestManager();
        HanoiQuests.RegisterAll(qm1);
        qm1.StartQuest(HanoiQuests.Quest1Id);
        qm1.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho);
        qm1.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThitBo);

        var flags = QuestFlags.CreateDefault();
        qm1.ExportToFlags(flags);

        Assert.True(flags.Q1_Started);
        Assert.True(flags.Q1_BanhPho_Collected);
        Assert.True(flags.Q1_ThitBo_Collected);
        Assert.False(flags.Q1_NuocDung_Collected);
        Assert.False(flags.Q1_Completed);

        // Rehydrate in fresh QuestManager
        var qm2 = new QuestManager();
        HanoiQuests.RegisterAll(qm2);
        qm2.HydrateFromFlags(flags);

        Assert.True(qm2.IsQuestInProgress(HanoiQuests.Quest1Id));
        Assert.Equal(1, qm2.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.BanhPho)!.CurrentCount);
        Assert.Equal(1, qm2.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.ThitBo)!.CurrentCount);
        Assert.Equal(0, qm2.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.NuocDung)!.CurrentCount);
    }

    [Fact]
    public void Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock()
    {
        // Verified Fix for Desynchronization Bug:
        // When flags are set directly (e.g. from save state or dialogue branch) without prior QuestManager.RecordObjectiveProgress,
        // BaAnNpc.Interact synchronizes QuestManager via HydrateFromFlags before CompleteQuest.
        // Both QuestFlags and QuestManager transition to Completed, and Quest 2 is unlocked without soft-lock.
        var (ctx, _, qm, _, flags, _) = CreateHarnessContext();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();

        flags.Q1_Started = true;
        flags.Q1_BanhPho_Collected = true;
        flags.Q1_ThitBo_Collected = true;
        flags.Q1_NuocDung_Collected = true;
        flags.Q1_HanhLa_Collected = true;
        flags.Q1_ThaoMoc_Collected = true;

        var result = baAn.Interact(ctx);

        Assert.True(result.Success);
        Assert.True(result.QuestCompleted);
        Assert.True(flags.Q1_Completed);

        // Desync resolved: QuestManager is completed and Quest 2 can now start!
        Assert.True(qm.IsQuestCompleted(HanoiQuests.Quest1Id));
        Assert.True(qm.CanStartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData()
    {
        // Verified Fix: HanoiExplorationScene constructor hydrates QuestManager
        // from SaveData.Level1State.QuestFlags upon initialization.
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Started = true;
        saveData.Level1State.QuestFlags.Q1_BanhPho_Collected = true;

        var scene = new GameProject.Stages.HanoiExplorationScene(saveData);

        // QuestManager is properly hydrated:
        Assert.Equal(QuestState.InProgress, scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id));
        Assert.Equal(1, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.BanhPho)!.CurrentCount);
    }

    // =========================================================================
    // 5. VIETNAMESE UNICODE FONT CODEPOINT COMPLETENESS
    // =========================================================================

    [Fact]
    public void VietnameseUnicode_All134VowelsWithTones_ContainedInCodepointsTable()
    {
        // Full canonical inventory of Vietnamese accented vowels and consonants (Đ, đ)
        // 12 vowel groups: (a, ă, â, e, ê, i, o, ô, ơ, u, ư, y)
        // 5 tones: huyền (`), sắc (´), hỏi (?), ngã (~), nặng (.)
        // Both Lowercase and Uppercase
        string vietnameseCharacters =
            // a and derivatives
            "aàáảãạ" + "AÀÁẢÃẠ" +
            "ăằắẳẵặ" + "ĂẰẮẲẴẶ" +
            "âầấẩẫậ" + "ÂẦẤẨẪẬ" +
            // e and derivatives
            "eèéẻẽẹ" + "EÈÉẺẼẸ" +
            "êềếểễệ" + "ÊỀẾỂỄỆ" +
            // i
            "iìíỉĩị" + "IÌÍỈĨỊ" +
            // o and derivatives
            "oòóỏõọ" + "OÒÓỎÕỌ" +
            "ôồốổỗộ" + "ÔỒỐỔỖỘ" +
            "ơờớởỡợ" + "ƠỜỚỞỠỢ" +
            // u and derivatives
            "uùúủũụ" + "UÙÚỦŨỤ" +
            "ưừứửữự" + "ƯỪỨỬỮỰ" +
            // y
            "yỳýỷỹỵ" + "YỲÝỶỸỴ" +
            // d with stroke
            "đĐ";

        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);

        var missingCharacters = new List<string>();

        foreach (char c in vietnameseCharacters)
        {
            int cp = (int)c;
            if (!codepointSet.Contains(cp))
            {
                missingCharacters.Add($"'{c}' (U+{cp:X4})");
            }
        }

        // Must be completely empty: all 134+ Vietnamese accented letters must be supported!
        Assert.Empty(missingCharacters);
    }

    [Fact]
    public void VietnameseUnicode_AllMilestone2TextPhrases_ContainedInCodepointsTable()
    {
        // All strings actually used in dialogues, quest HUD, prompts, and item names
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        var textCorpus = new List<string>
        {
            baAn.DisplayName,
            "Chào bé Mây! Hôm nay bà chuẩn bị nấu nồi phở gia truyền nhưng thiếu mất mấy nguyên liệu ngon nhất. Cháu giúp bà ghé các quầy hàng trong phố cổ xin đủ 5 món: Bánh phở tươi, Thịt bò mềm, Nước dùng ninh xương, Hành lá rau mùi và Gói thảo mộc thơm lừng nhé!",
            "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông. Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!",
            "Bà cháu mình cùng vào bàn ghép bát phở chuẩn vị Hà Nội nào cháu yêu!",
            "[E] Nói chuyện",
            "[E] Trả nhiệm vụ",
            "[E] Hỏi thăm",
            "[E] Tương tác",
            "[E] Nhận nguyên liệu",
            "[Phím Trắng / Enter] Tiếp tục",
            "Thu thập nguyên liệu nấu Phở",
            "Gặp gỡ các tiểu thương trên phố Hàng Buồm và Lãn Ông để thu thập đủ 5 nguyên liệu nấu phở cho Bà An.",
            "Ghép đúng Bát Phở hoàn chỉnh",
            "Sắp xếp gian hàng mở lối ra Cầu Long Biên",
            "Cầu Long Biên - Dải lụa thép qua sông Hồng",
            "Quán Phở Bà An",
            "Tem Hoa Sen Hà Nội",
            "Bút Chì",
            "Bé Mây",
            "Bóng Xám",
            "₫ 100.000",
            "« Hà Nội 36 phố phường »",
            "“Phở Hà Nội” – Tinh hoa ẩm thực Việt",
            "… và câu chuyện lịch sử."
        };

        foreach (var m in merchants)
        {
            textCorpus.Add(m.DisplayName);
            textCorpus.Add(m.ItemName);
        }

        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);

        var missingCharacters = new HashSet<string>();

        foreach (string phrase in textCorpus)
        {
            foreach (char c in phrase)
            {
                int cp = (int)c;
                if (!codepointSet.Contains(cp))
                {
                    missingCharacters.Add($"'{c}' (U+{cp:X4})");
                }
            }
        }

        Assert.Empty(missingCharacters);
    }

    [Fact]
    public void VietnameseUnicode_DecomposedNFDDiacritics_NotInCodepointsTable_RevealsNFCRequirement()
    {
        // Vulnerability / Constraint Investigation:
        // Combining diacritical marks used in NFD (U+0300..U+036F)
        // e.g. U+0300 (grave `), U+0301 (acute ´), U+0303 (tilde ~), U+0309 (hook ?), U+0323 (dot below .)
        // This test proves empirically that FontManager does NOT include combining marks,
        // which means decomposed NFD strings from macOS APFS/Telex would produce missing glyphs unless normalized to Form C!
        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);

        int[] combiningMarks = new[]
        {
            0x0300, // Combining Grave Accent
            0x0301, // Combining Acute Accent
            0x0303, // Combining Tilde
            0x0309, // Combining Hook Above
            0x0323  // Combining Dot Below
        };

        foreach (int cp in combiningMarks)
        {
            // Confirmed: NFD combining marks are NOT in the font codepoint table.
            Assert.DoesNotContain(cp, codepointSet);
        }

        // Verify that decomposing a Vietnamese word produces missing characters:
        string nfcString = "Phở";
        string nfdString = nfcString.Normalize(NormalizationForm.FormD);

        // In NFC: 'ở' is U+1EDF, which IS in the codepoint table
        Assert.All(nfcString, c => Assert.Contains((int)c, codepointSet));

        // In NFD: 'ơ' + U+0309 (Combining Hook Above). U+0309 is NOT in the codepoint table!
        bool hasMissingInNfd = nfdString.Any(c => !codepointSet.Contains((int)c));
        Assert.True(hasMissingInNfd);
    }

    [Fact]
    public void VietnameseUnicode_TypographicSymbols_QuotesDashesDong_ContainedInCodepointsTable()
    {
        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);

        // Typographic symbols commonly used in Vietnamese literature & game narrative
        int[] symbols = new[]
        {
            0x20AB, // ₫ (Dong sign)
            0x00AB, // « (Left-pointing double angle quotation mark)
            0x00BB, // » (Right-pointing double angle quotation mark)
            0x201C, // “ (Left double quotation mark)
            0x201D, // ” (Right double quotation mark)
            0x2018, // ‘ (Left single quotation mark)
            0x2019, // ’ (Right single quotation mark)
            0x2013, // – (En dash)
            0x2014, // — (Em dash)
            0x2026, // … (Horizontal ellipsis)
            0x2022  // • (Bullet)
        };

        foreach (int cp in symbols)
        {
            Assert.Contains(cp, codepointSet);
        }
    }

    [Fact]
    public void FontManager_NormalizeText_NormalizesNfdToPrecomposedFormC()
    {
        // Decomposed Vietnamese strings (common on macOS APFS clipboard)
        string nfdPho = "Ph\u01A1\u0309"; // Ph + ơ + Combining Hook Above
        string normalized = FontManager.NormalizeText(nfdPho);

        Assert.Equal("Phở", normalized);
        Assert.Equal("\u1EDF", normalized.Substring(2)); // U+1EDF is precomposed 'ở'

        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);
        Assert.All(normalized, c => Assert.Contains((int)c, codepointSet));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("Hà Nội", "Hà Nội")]
    public void FontManager_NormalizeText_HandlesEdgeCasesSafely(string? input, string expected)
    {
        Assert.Equal(expected, FontManager.NormalizeText(input));
    }

    // =========================================================================
    // 6. ADVERSARIAL REMEDIATION INTEGRITY & STRESS HARNESS (CHALLENGER 2 IT2)
    // =========================================================================

    [Fact]
    public void Remediation_QuestManager_MidQuestHydration_ThenCollectRemainingAndTurnIn_UnlocksQuest2()
    {
        // Simulate savegame where player collected 3 items (BanhPho, ThitBo, NuocDung) and saved
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Started = true;
        saveData.Level1State.QuestFlags.Q1_BanhPho_Collected = true;
        saveData.Level1State.QuestFlags.Q1_ThitBo_Collected = true;
        saveData.Level1State.QuestFlags.Q1_NuocDung_Collected = true;
        saveData.Level1State.InventoryItems.Add(ItemIds.BanhPho);
        saveData.Level1State.InventoryItems.Add(ItemIds.ThitBo);
        saveData.Level1State.InventoryItems.Add(ItemIds.NuocDung);

        var scene = new GameProject.Stages.HanoiExplorationScene(saveData);

        // Verify hydrated state
        Assert.Equal(QuestState.InProgress, scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id));
        Assert.Equal(1, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.BanhPho)!.CurrentCount);
        Assert.Equal(1, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.ThitBo)!.CurrentCount);
        Assert.Equal(1, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.NuocDung)!.CurrentCount);
        Assert.Equal(0, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.HanhLa)!.CurrentCount);
        Assert.Equal(0, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.ThaoMoc)!.CurrentCount);

        // Exploration scene context
        var ctx = new InteractionContext(
            PlayerPosition: Vector2.Zero,
            Inventory: scene.Inventory,
            QuestManager: scene.QuestManager,
            QuestFlags: scene.QuestFlags,
            EventBus: scene.EventBus,
            AudioService: scene.AudioService
        );

        // Find merchants for HanhLa and ThaoMoc
        var merchantHanhLa = scene.Merchants.First(m => m.ItemId == ItemIds.HanhLa);
        var merchantThaoMoc = scene.Merchants.First(m => m.ItemId == ItemIds.ThaoMoc);

        // Collect remaining 2 items
        var res1 = merchantHanhLa.Interact(ctx);
        Assert.True(res1.Success);
        Assert.Equal(ItemIds.HanhLa, res1.GrantedItemId);

        var res2 = merchantThaoMoc.Interact(ctx);
        Assert.True(res2.Success);
        Assert.Equal(ItemIds.ThaoMoc, res2.GrantedItemId);

        Assert.True(scene.QuestFlags.AreAllQ1IngredientsCollected());
        Assert.Equal(5, scene.Inventory.Count);

        // Turn in to Bà An
        var turnInRes = scene.BaAn.Interact(ctx);
        Assert.True(turnInRes.Success);
        Assert.True(turnInRes.QuestCompleted);
        Assert.True(scene.QuestFlags.Q1_Completed);
        Assert.Equal(QuestState.Completed, scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id));

        // Quest 2 is startable
        Assert.True(scene.QuestManager.CanStartQuest(HanoiQuests.Quest2Id));
        Assert.True(scene.QuestManager.StartQuest(HanoiQuests.Quest2Id));
        Assert.Equal(QuestState.InProgress, scene.QuestManager.GetQuestState(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void Remediation_QuestManager_SaveWithCompletedQuest1_HydratesAndAllowsQuest2Immediately()
    {
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Started = true;
        saveData.Level1State.QuestFlags.Q1_BanhPho_Collected = true;
        saveData.Level1State.QuestFlags.Q1_ThitBo_Collected = true;
        saveData.Level1State.QuestFlags.Q1_NuocDung_Collected = true;
        saveData.Level1State.QuestFlags.Q1_HanhLa_Collected = true;
        saveData.Level1State.QuestFlags.Q1_ThaoMoc_Collected = true;
        saveData.Level1State.QuestFlags.Q1_Completed = true;

        var scene = new GameProject.Stages.HanoiExplorationScene(saveData);

        Assert.Equal(QuestState.Completed, scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id));
        Assert.True(scene.QuestManager.CanStartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void Remediation_Proximity_TargetSwitchingBetweenTwoEntities_PreservesHysteresisAndAudioThrottle()
    {
        var (ctx, _, _, _, _, audio) = CreateHarnessContext();
        var mgr = new InteractionManager();

        var entityA = new TestRadiusInteractable("EntityA", new Vector2(0f, 0f), radius: 48f);
        var entityB = new TestRadiusInteractable("EntityB", new Vector2(100f, 0f), radius: 48f);

        mgr.Register(entityA);
        mgr.Register(entityB);

        // Player starts near EntityA (30, 0) -> EntityA acquired
        mgr.Update(0.016f, new Vector2(30f, 0f), ctx);
        Assert.Equal("EntityA", mgr.CurrentTarget?.Id);
        Assert.Equal(1, audio.PlayedSfx.Count(s => s == SfxType.InteractPrompt));

        // Player moves to (50, 0)
        // Distance to A is 50px (inside exit radius 54px, outside enter radius 48px)
        // Distance to B is 50px (outside enter radius 48px)
        // EntityA should be RETAINED due to hysteresis
        mgr.Update(0.016f, new Vector2(50f, 0f), ctx);
        Assert.Equal("EntityA", mgr.CurrentTarget?.Id);

        // Player moves to (53f, 0)
        // Distance to A is 53px <= 54px (exit radius)
        // Distance to B is 47px <= 48px (enter radius)
        // EntityB is inside enter radius AND closer (47px < 53px) -> Switches to EntityB!
        mgr.Update(0.016f, new Vector2(53f, 0f), ctx);
        Assert.Equal("EntityB", mgr.CurrentTarget?.Id);
    }

    [Fact]
    public void Remediation_Inventory_ExhaustiveAliasSymmetryAndIdempotence()
    {
        var inv = new Inventory();

        // 1. Initial collection of GiaVi
        Assert.True(inv.AddItem(ItemIds.GiaVi));
        Assert.Equal(1, inv.Count);
        Assert.True(inv.HasItem(ItemIds.GiaVi));
        Assert.True(inv.HasItem(ItemIds.ThaoMoc));
        Assert.True(inv.HasItem("ITEM_THAOMOC")); // case insensitive

        // 2. Reject duplicate alias
        Assert.False(inv.AddItem(ItemIds.ThaoMoc));
        Assert.False(inv.AddItem(ItemIds.GiaVi));
        Assert.Equal(1, inv.Count);

        // 3. Remove by alias (ThaoMoc removes GiaVi)
        Assert.True(inv.RemoveItem(ItemIds.ThaoMoc));
        Assert.Equal(0, inv.Count);
        Assert.False(inv.HasItem(ItemIds.GiaVi));
        Assert.False(inv.HasItem(ItemIds.ThaoMoc));

        // 4. Multiple remove returns false
        Assert.False(inv.RemoveItem(ItemIds.ThaoMoc));
        Assert.False(inv.RemoveItem(ItemIds.GiaVi));

        // 5. Constructor deduplication of aliases
        var inv2 = new Inventory(new[] { ItemIds.BanhPho, ItemIds.ThaoMoc, ItemIds.GiaVi });
        Assert.Equal(2, inv2.Count);
        Assert.True(inv2.HasItem(ItemIds.BanhPho));
        Assert.True(inv2.HasItem(ItemIds.ThaoMoc));
        Assert.True(inv2.HasItem(ItemIds.GiaVi));
    }

    [Fact]
    public void Remediation_FontManager_ComplexVietnameseNfdStrings_AllRecomposeToPrecomposedFormC()
    {
        string[] complexVietnamese = new[]
        {
            "Bát phở bò nóng hổi: 50.000₫!\n«Hàng Buồm & Lãn Ông»",
            "Màu sắc rực rỡ của Cầu Long Biên và hoa sen hồ Tây.",
            "thuở, nguệch, khoẻ, xoong, toé, ngoằn ngoèo, sượt, khuỷu, hoẵng, lũy",
            "Đoạn thư của Bóng Xám gửi Bé Mây và Bút Chì tại Hà Nội."
        };

        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);

        foreach (string original in complexVietnamese)
        {
            // Decompose to NFD (simulating macOS APFS / clipboard / external IME input)
            string nfd = original.Normalize(NormalizationForm.FormD);

            // Recompose using FontManager.NormalizeText
            string normalized = FontManager.NormalizeText(nfd);

            // Verify normalized matches canonical Form C
            Assert.Equal(original.Normalize(NormalizationForm.FormC), normalized);

            // Verify all characters are in the 762 codepoints table or standard whitespace/symbols
            foreach (char c in normalized)
            {
                if (char.IsWhiteSpace(c) || c == '\n' || c == '\r') continue;
                Assert.Contains((int)c, codepointSet);
            }
        }
    }
}
