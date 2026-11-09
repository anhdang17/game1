using System;
using System.Linq;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;
using Xunit;

namespace GameProject.Tests;

public class InteractionAndQuest1Tests
{
    private sealed class TestInteractable : IInteractable
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Vector2 Position { get; init; }
        public Vector2 InteractionCenter => Position;
        public float InteractionRadius { get; init; } = 48.0f;
        public float VisualHeight { get; init; } = 40.0f;
        public bool CanInteractValue { get; set; } = true;

        public bool CanInteract(InteractionContext context) => CanInteractValue;

        public InteractionResult Interact(InteractionContext context)
        {
            return new InteractionResult(true, DisplayName, "Interacted with " + DisplayName);
        }

        public string GetPromptText(InteractionContext context) => "[E] Tương tác";
    }

    private static (InteractionContext Context, EventBus EventBus, QuestManager QuestManager, Inventory Inventory, QuestFlags Flags) CreateTestContext()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var inv = new Inventory();
        var flags = QuestFlags.CreateDefault();
        var audio = new NullAudioService();

        var ctx = new InteractionContext(
            PlayerPosition: Vector2.Zero,
            Inventory: inv,
            QuestManager: qm,
            QuestFlags: flags,
            EventBus: bus,
            AudioService: audio
        );

        return (ctx, bus, qm, inv, flags);
    }

    // =========================================================================
    // 1. INTERACTION PROXIMITY & PRIORITY DETECTION TESTS
    // =========================================================================

    [Fact]
    public void Interaction_OutOfRange_ReturnsNull()
    {
        var (ctx, _, _, _, _) = CreateTestContext();
        var mgr = new InteractionManager();
        mgr.Register(new TestInteractable { Id = "Entity1", Position = new Vector2(100f, 0f), InteractionRadius = 48f });

        var nearest = mgr.FindNearestInteractable(Vector2.Zero, ctx);
        Assert.Null(nearest);
    }

    [Fact]
    public void Interaction_WithinRange_ReturnsEntity()
    {
        var (ctx, _, _, _, _) = CreateTestContext();
        var mgr = new InteractionManager();
        mgr.Register(new TestInteractable { Id = "Entity1", Position = new Vector2(30f, 0f), InteractionRadius = 48f });

        var nearest = mgr.FindNearestInteractable(Vector2.Zero, ctx);
        Assert.NotNull(nearest);
        Assert.Equal("Entity1", nearest.Id);
    }

    [Fact]
    public void Interaction_MultipleEntities_SelectsNearest()
    {
        var (ctx, _, _, _, _) = CreateTestContext();
        var mgr = new InteractionManager();
        mgr.Register(new TestInteractable { Id = "FarEntity", Position = new Vector2(40f, 0f) });
        mgr.Register(new TestInteractable { Id = "NearEntity", Position = new Vector2(20f, 0f) });

        var nearest = mgr.FindNearestInteractable(Vector2.Zero, ctx);
        Assert.NotNull(nearest);
        Assert.Equal("NearEntity", nearest.Id);
    }

    [Fact]
    public void Interaction_EquidistantEntities_DeterministicTieBreak()
    {
        var (ctx, _, _, _, _) = CreateTestContext();
        var mgr = new InteractionManager();
        // Two entities at exactly distance 30: (30, 0) and (-30, 0)
        mgr.Register(new TestInteractable { Id = "Stall_Z", Position = new Vector2(30f, 0f) });
        mgr.Register(new TestInteractable { Id = "Stall_A", Position = new Vector2(-30f, 0f) });

        var nearest = mgr.FindNearestInteractable(Vector2.Zero, ctx);
        Assert.NotNull(nearest);
        Assert.Equal("Stall_A", nearest.Id); // Deterministic ordinal sort: 'Stall_A' < 'Stall_Z'
    }

    [Fact]
    public void InteractionBubble_BobTimerAndAlpha_SmoothInterpolation()
    {
        var (ctx, _, _, _, _) = CreateTestContext();
        var mgr = new InteractionManager();
        var entity = new TestInteractable { Id = "Target", Position = new Vector2(20f, 0f) };
        mgr.Register(entity);

        // Player is at (0, 0), within 48px radius
        mgr.Update(0.1f, Vector2.Zero, ctx);
        Assert.NotNull(mgr.CurrentTarget);
        Assert.True(mgr.BubbleAlpha > 0f);
        Assert.True(mgr.BubbleBobTimer > 0f);

        // Multiple frames -> alpha approaches 1.0
        for (int i = 0; i < 20; i++)
        {
            mgr.Update(0.05f, Vector2.Zero, ctx);
        }
        Assert.Equal(1.0f, mgr.BubbleAlpha);

        // Move player far away
        Vector2 farAway = new(500f, 500f);
        mgr.Update(0.1f, farAway, ctx);
        Assert.Null(mgr.CurrentTarget);
        Assert.True(mgr.BubbleAlpha < 1.0f);

        // Several frames away -> alpha drops to 0.0
        for (int i = 0; i < 20; i++)
        {
            mgr.Update(0.05f, farAway, ctx);
        }
        Assert.Equal(0.0f, mgr.BubbleAlpha);
    }

    // =========================================================================
    // 2. QUEST 1 GATHERING LOOP & MERCHANT IDEMPOTENCY TESTS
    // =========================================================================

    [Fact]
    public void Merchant_PreQuestInteraction_ReturnsGuidanceWithoutGivingItem()
    {
        var (ctx, _, qm, inv, flags) = CreateTestContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        var phoMerchant = merchants.First(m => m.ItemId == ItemIds.BanhPho);

        // Quest 1 is NOT started
        Assert.False(flags.Q1_Started);

        var result = phoMerchant.Interact(ctx);
        Assert.True(result.Success);
        Assert.Null(result.GrantedItemId);
        Assert.False(inv.HasItem(ItemIds.BanhPho));
        Assert.Equal(0, qm.GetQuest(HanoiQuests.Quest1Id)?.GetObjective(ItemIds.BanhPho)?.CurrentCount);
        Assert.False(flags.Q1_BanhPho_Collected);
    }

    [Fact]
    public void BaAn_InitialInteraction_StartsQuest1()
    {
        var (ctx, bus, qm, _, flags) = CreateTestContext();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();

        bool questStartedFired = false;
        bus.Subscribe<QuestStartedEvent>(ev =>
        {
            if (ev.QuestId == HanoiQuests.Quest1Id) questStartedFired = true;
        });

        var result = baAn.Interact(ctx);
        Assert.True(result.Success);
        Assert.True(result.QuestStarted);
        Assert.True(flags.Q1_Started);
        Assert.True(qm.IsQuestInProgress(HanoiQuests.Quest1Id));
        Assert.True(questStartedFired);
    }

    [Fact]
    public void Merchant_ActiveQuestInteraction_GrantsItemAndRecordsProgress()
    {
        var (ctx, bus, qm, inv, flags) = CreateTestContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();

        // 1. Start Quest 1
        baAn.Interact(ctx);
        Assert.True(flags.Q1_Started);

        var phoMerchant = merchants.First(m => m.ItemId == ItemIds.BanhPho);
        bool itemCollectedFired = false;
        bus.Subscribe<ItemCollectedEvent>(ev =>
        {
            if (ev.ItemId == ItemIds.BanhPho) itemCollectedFired = true;
        });

        // 2. Interact with merchant
        var result = phoMerchant.Interact(ctx);
        Assert.True(result.Success);
        Assert.Equal(ItemIds.BanhPho, result.GrantedItemId);
        Assert.True(inv.HasItem(ItemIds.BanhPho));
        Assert.True(flags.Q1_BanhPho_Collected);
        Assert.Equal(1, qm.GetQuest(HanoiQuests.Quest1Id)?.GetObjective(ItemIds.BanhPho)?.CurrentCount);
        Assert.True(itemCollectedFired);
    }

    [Fact]
    public void Merchant_DuplicateInteractions_IsStrictlyIdempotent()
    {
        var (ctx, _, qm, inv, flags) = CreateTestContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        baAn.Interact(ctx);

        var beefMerchant = merchants.First(m => m.ItemId == ItemIds.ThitBo);

        // First grant
        var r1 = beefMerchant.Interact(ctx);
        Assert.Equal(ItemIds.ThitBo, r1.GrantedItemId);
        Assert.Equal(1, inv.Count);

        // Repeated interactions (2nd, 3rd, 4th, 5th)
        for (int i = 0; i < 4; i++)
        {
            var rNext = beefMerchant.Interact(ctx);
            Assert.True(rNext.Success);
            Assert.Null(rNext.GrantedItemId); // No duplicate grant
            Assert.Equal(1, inv.Count);       // Still exactly 1 item
            Assert.Equal(1, qm.GetQuest(HanoiQuests.Quest1Id)?.GetObjective(ItemIds.ThitBo)?.CurrentCount);
        }
    }

    [Fact]
    public void GatherAll5_BaAnTurnsIn_CompletesQuest1_EnablesQuest2()
    {
        var (ctx, bus, qm, inv, flags) = CreateTestContext();
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();

        // 1. Start Quest 1
        baAn.Interact(ctx);

        // 2. Gather from all 5 merchants
        for (int i = 0; i < merchants.Count; i++)
        {
            var r = merchants[i].Interact(ctx);
            Assert.NotNull(r.GrantedItemId);
        }

        Assert.Equal(5, inv.Count);
        Assert.True(flags.AreAllQ1IngredientsCollected());
        Assert.Equal(5, flags.GetQ1CollectedCount());
        Assert.False(flags.Q1_Completed);

        // 3. Turn in to Bà An
        bool questCompletedFired = false;
        bus.Subscribe<QuestCompletedEvent>(ev =>
        {
            if (ev.QuestId == HanoiQuests.Quest1Id) questCompletedFired = true;
        });

        var turnInResult = baAn.Interact(ctx);
        Assert.True(turnInResult.Success);
        Assert.True(turnInResult.QuestCompleted);
        Assert.True(flags.Q1_Completed);
        Assert.True(qm.IsQuestCompleted(HanoiQuests.Quest1Id));
        Assert.True(questCompletedFired);

        // 4. Verify Quest 2 (Phở Assembly) is unlocked
        Assert.True(qm.CanStartQuest(HanoiQuests.Quest2Id));
    }

    // =========================================================================
    // 3. UI HUD VIEW MODEL TESTS
    // =========================================================================

    [Fact]
    public void UI_InventoryBar_ReflectsCollectedItems()
    {
        var bus = new EventBus();
        var bar = new InventoryBar(bus);
        var inv = new Inventory();

        bar.Update(0.016f, inv, Vector2.Zero, 1280);
        Assert.All(bar.Slots, s => Assert.False(s.IsCollected));

        // Add 2 items
        inv.AddItem(ItemIds.BanhPho);
        inv.AddItem(ItemIds.HanhLa);

        bar.Update(0.016f, inv, Vector2.Zero, 1280);

        Assert.True(bar.Slots.First(s => s.ItemId == ItemIds.BanhPho).IsCollected);
        Assert.True(bar.Slots.First(s => s.ItemId == ItemIds.HanhLa).IsCollected);
        Assert.False(bar.Slots.First(s => s.ItemId == ItemIds.ThitBo).IsCollected);
    }

    [Theory]
    [InlineData(0, "Thu thập nguyên liệu phở: (0/5)")]
    [InlineData(3, "Thu thập nguyên liệu phở: (3/5)")]
    [InlineData(5, "Thu thập nguyên liệu phở: (5/5)")]
    public void UI_QuestHUD_ProgressFormatting_ShowsCorrectFraction(int count, string expected)
    {
        string formatted = QuestHUD.FormatObjectiveProgress(count, 5);
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void UI_QuestHUD_ToastLifecycle_SlidesAndFades()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        var flags = QuestFlags.CreateDefault();
        var hud = new QuestHUD(bus);

        hud.ShowToast("+ Đã nhận: Bánh Phở", ItemIds.BanhPho);
        Assert.Single(hud.ActiveToasts);
        Assert.Equal(1.0f, hud.ActiveToasts[0].Alpha);

        // Advance 2.4 seconds -> fading
        hud.Update(2.4f, qm, flags);
        Assert.Single(hud.ActiveToasts);
        Assert.True(hud.ActiveToasts[0].Alpha < 1.0f);

        // Advance past 2.8 seconds -> removed
        hud.Update(0.5f, qm, flags);
        Assert.Empty(hud.ActiveToasts);
    }

    // =========================================================================
    // 4. GRAPHICS MATHEMATICAL PURITY & CODEPOINT TESTS
    // =========================================================================

    [Fact]
    public void Graphics_HanoiPalette_ApplyDesaturation_MathematicalPurity()
    {
        Color vibrantRed = new(255, 0, 0, 255);

        // 0% color -> purely grayscale luminance: 0.2126 * 255 = 54.213 -> 54
        Color gray = HanoiPalette.ApplyDesaturation(vibrantRed, 0.0f);
        Assert.Equal(gray.R, gray.G);
        Assert.Equal(gray.G, gray.B);
        Assert.Equal(54, gray.R);

        // 100% color -> exact original color
        Color full = HanoiPalette.ApplyDesaturation(vibrantRed, 100.0f);
        Assert.Equal(255, full.R);
        Assert.Equal(0, full.G);
        Assert.Equal(0, full.B);
        Assert.Equal(255, full.A);
    }

    [Fact]
    public void Graphics_FontManager_GetVietnameseCodepoints_Covers762Glyphs()
    {
        int[] codepoints = FontManager.GetVietnameseCodepoints();
        Assert.Equal(762, codepoints.Length);

        // Verify Basic Latin
        Assert.Contains((int)'A', codepoints);
        Assert.Contains((int)'Z', codepoints);
        Assert.Contains((int)' ', codepoints);

        // Verify Latin Extended-A: 'Đ' (0x0110), 'đ' (0x0111)
        Assert.Contains(0x0110, codepoints);
        Assert.Contains(0x0111, codepoints);

        // Verify Latin Extended Additional: 'ạ' (0x1EA1), 'ố' (0x1ED1), 'ỹ' (0x1EF9)
        Assert.Contains(0x1EA1, codepoints);
        Assert.Contains(0x1ED1, codepoints);
        Assert.Contains(0x1EF9, codepoints);

        // Verify Currency symbol ₫ (0x20AB)
        Assert.Contains(0x20AB, codepoints);
    }

    [Fact]
    public void AllFiveMerchants_DistinctItemIdsAndLocations()
    {
        var (_, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        Assert.Equal(5, merchants.Count);

        var itemIds = merchants.Select(m => m.ItemId).Distinct().ToList();
        Assert.Equal(5, itemIds.Count);

        var positions = merchants.Select(m => m.Position).Distinct().ToList();
        Assert.Equal(5, positions.Count);
    }

    [Fact]
    public void BaAn_TurnIn_WhenFewerThanFiveItems_RemindsPlayer()
    {
        var (ctx, _, _, inv, flags) = CreateTestContext();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();

        baAn.Interact(ctx); // Starts quest

        // Player only has 2 items
        inv.AddItem(ItemIds.BanhPho);
        flags.Q1_BanhPho_Collected = true;
        inv.AddItem(ItemIds.ThitBo);
        flags.Q1_ThitBo_Collected = true;

        var result = baAn.Interact(ctx);
        Assert.True(result.Success);
        Assert.False(result.QuestCompleted);
        Assert.Contains("2/5", result.DialogueText);
        Assert.False(flags.Q1_Completed);
    }

    [Fact]
    public void InventoryBar_ItemCollectedEvent_TriggersPulseAnimation()
    {
        var bus = new EventBus();
        var bar = new InventoryBar(bus);

        var slot = bar.Slots.First(s => s.ItemId == ItemIds.BanhPho);
        Assert.Equal(1.0f, slot.ScalePulse);

        // Fire event
        bus.Publish(new ItemCollectedEvent(ItemIds.BanhPho, "Bánh Phở", 1, 1));

        Assert.True(slot.ScalePulse > 1.0f);
        Assert.True(slot.IsCollected);
        Assert.True(slot.RippleAlpha > 0f);
    }
}
