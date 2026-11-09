using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Quests;

namespace GameProject.Core.Interactions;

/// <summary>
/// Bà An (Bà cụ bán phở) NPC - Primary quest giver and turn-in receiver for Quest 1.
/// </summary>
public sealed class BaAnNpc : IInteractable, IYSortable
{
    public string Id => "Npc_BaAn";
    public string DisplayName => "Bà An";
    public Vector2 Position { get; set; }
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 48.0f;
    public float VisualHeight => 48.0f;

    // IYSortable
    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public BaAnNpc(Vector2 position) => Position = position;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context)
    {
        if (context.QuestFlags.Q2_Completed) return "[E] Nói chuyện";
        if (context.QuestFlags.Q1_Completed) return "[E] Ghép Bát Phở";
        if (context.QuestFlags.AreAllQ1IngredientsCollected()) return "[E] Trả nhiệm vụ";
        return context.QuestFlags.Q1_Started ? "[E] Hỏi thăm" : "[E] Tương tác";
    }

    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Ensure Hanoi quests are registered
        if (context.QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(context.QuestManager);
        }

        // State 1: Quest 1 Not Started
        if (!context.QuestFlags.Q1_Started)
        {
            context.QuestManager.StartQuest(HanoiQuests.Quest1Id);
            context.QuestFlags.Q1_Started = true;
            context.AudioService.PlaySfx(SfxType.ButtonClick);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Chào bé Mây! Hôm nay bà chuẩn bị nấu nồi phở gia truyền nhưng thiếu mất mấy nguyên liệu ngon nhất. Cháu giúp bà ghé các quầy hàng trong phố cổ xin đủ 5 món: Bánh phở tươi, Thịt bò mềm, Nước dùng ninh xương, Hành lá rau mùi và Gói thảo mộc thơm lừng nhé!",
                QuestStarted: true
            );
        }

        // State 2: Requirements Met -> Complete Quest 1
        if (context.QuestFlags.AreAllQ1IngredientsCollected() && !context.QuestFlags.Q1_Completed)
        {
            // Rehydrate QuestManager from QuestFlags to guarantee internal synchronization
            context.QuestManager.HydrateFromFlags(context.QuestFlags);

            if (context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id))
            {
                context.QuestFlags.Q1_Completed = true;
                context.AudioService.PlaySfx(SfxType.PhoCookSuccess);

                return new InteractionResult(
                    Success: true,
                    DialogueSpeaker: DisplayName,
                    DialogueText: "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông. Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!",
                    QuestCompleted: true
                );
            }
        }

        // State 3: Quest 2 Completed
        if (context.QuestFlags.Q2_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Bát phở thơm ngon chuẩn vị Hà Nội của bà cháu mình đây rồi! Cảm ơn bé Mây nhiều lắm! Giờ cháu hãy qua chỗ cổng ra Cầu Long Biên xem sao nhé, nghe nói mấy gian hàng đang che mất lối đi rồi đấy!"
            );
        }

        // State 4: Quest 1 Completed, Quest 2 Incomplete
        if (context.QuestFlags.Q1_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Bà cháu mình cùng vào bàn ghép bát phở chuẩn vị Hà Nội nào cháu yêu!"
            );
        }

        // State 5: In Progress Reminder
        int count = context.QuestFlags.GetQ1CollectedCount();
        return new InteractionResult(
            Success: true,
            DialogueSpeaker: DisplayName,
            DialogueText: $"Cháu đã tìm được {count}/5 món rồi! Cố gắng ghé nốt các quầy còn lại nhé bé Mây: Bánh phở Hàng Buồm, Thịt bò Bác Tư, Nồi nước dùng, Rau tươi Cô Lan và Thuốc bắc Lãn Ông."
        );
    }
}

/// <summary>
/// Specialized market merchant stall granting a specific Phở ingredient upon interaction.
/// </summary>
public sealed class PhoIngredientMerchant : IInteractable, IYSortable
{
    public string Id { get; }
    public string DisplayName { get; }
    public string ItemId { get; }
    public string ItemName { get; }
    public Vector2 Position { get; set; }
    public Vector2 InteractionCenter => Position + new Vector2(0, -18);
    public float InteractionRadius => 48.0f;
    public float VisualHeight => 42.0f;

    // IYSortable
    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    private readonly string _grantDialogue;
    private readonly string _reminderDialogue;
    private readonly string _preQuestDialogue;

    public PhoIngredientMerchant(
        string id,
        string displayName,
        string itemId,
        string itemName,
        Vector2 position,
        string grantDialogue,
        string reminderDialogue,
        string preQuestDialogue)
    {
        Id = id;
        DisplayName = displayName;
        ItemId = itemId;
        ItemName = itemName;
        Position = position;
        _grantDialogue = grantDialogue;
        _reminderDialogue = reminderDialogue;
        _preQuestDialogue = preQuestDialogue;
    }

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) =>
        context.Inventory.HasItem(ItemId) ? "[E] Nói chuyện" : "[E] Nhận nguyên liệu";

    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Not started yet
        if (!context.QuestFlags.Q1_Started)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _preQuestDialogue
            );
        }

        // Ensure QuestManager is hydrated into InProgress before recording progress
        if (context.QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(context.QuestManager);
        }
        if (context.QuestManager.GetQuestState(HanoiQuests.Quest1Id) == QuestState.NotStarted)
        {
            context.QuestManager.HydrateFromFlags(context.QuestFlags);
        }

        // 2. Already collected -> Idempotent reminder
        if (context.Inventory.HasItem(ItemId))
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _reminderDialogue
            );
        }

        // 3. Grant ingredient
        bool added = context.Inventory.AddItem(ItemId);
        if (added)
        {
            context.QuestManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemId, 1);
            string flagName = $"Q1_{ItemId.Replace("Item_", "")}_Collected";
            context.QuestFlags.SetFlag(flagName, true);
            context.EventBus.Publish(new ItemCollectedEvent(ItemId, ItemName, 1, 1));
            context.AudioService.PlaySfx(SfxType.ItemCollected);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _grantDialogue,
                GrantedItemId: ItemId
            );
        }

        return new InteractionResult(
            Success: true,
            DialogueSpeaker: DisplayName,
            DialogueText: _reminderDialogue
        );
    }
}

/// <summary>
/// Factory generating the complete set of Hanoi Old Quarter merchants and interactables.
/// </summary>
public static class HanoiInteractableFactory
{
    public static (BaAnNpc BaAn, List<PhoIngredientMerchant> Merchants) CreateHanoiInteractables()
    {
        var baAn = new BaAnNpc(new Vector2(200f, 300f));

        var merchants = new List<PhoIngredientMerchant>
        {
            new(
                id: "Stall_BanhPho",
                displayName: "Quầy Bánh Phở & Mì Sợi",
                itemId: ItemIds.BanhPho,
                itemName: "Bánh Phở tươi chần kỹ",
                position: new Vector2(380f, 220f),
                grantDialogue: "Bánh phở tươi tráng mỏng sợi dai mềm của bác đây! Mang về cho Bà An nấu phở nhé bé Mây!",
                reminderDialogue: "Bác đã đưa phần bánh phở tươi ngon nhất cho cháu rồi đó! Mang về cho Bà An sớm nhé!",
                preQuestDialogue: "Bánh phở tráng mỏng sợi dai dẻo vừa ra lò sớm nay, cháu qua chào Bà An chưa?"
            ),
            new(
                id: "Stall_ThitBo",
                displayName: "Quầy Thịt Bò Bác Tư",
                itemId: ItemIds.ThitBo,
                itemName: "Thịt Bò tươi thái mỏng",
                position: new Vector2(560f, 240f),
                grantDialogue: "Thịt bò tươi thái mỏng bản hoa của Bác Tư đây! Nấu phở là phải có thịt tươi thế này mới ngọt nước!",
                reminderDialogue: "Bác Tư đã chọn cho cháu phần nạm gầu ngon nhất rồi đấy! Mau mang về cho Bà An nhé!",
                preQuestDialogue: "Bác Tư chào bé Mây! Thịt bò sáng nay đỏ au tươi rói, cháu có mua gì cho gia đình không?"
            ),
            new(
                id: "Stall_NuocDung",
                displayName: "Bếp Nước Dùng Nóng",
                itemId: ItemIds.NuocDung,
                itemName: "Nước dùng hầm xương gia truyền",
                position: new Vector2(740f, 220f),
                grantDialogue: "Nồi nước dùng ninh xương ống thơm lừng gia truyền đây! Bác đã chắt sẵn vào bình ấm, cẩn thận kẻo nóng nhé bé Mây!",
                reminderDialogue: "Nước dùng ninh xương đã được đóng bi đông cẩn thận cho cháu rồi đó!",
                preQuestDialogue: "Nồi nước dùng đang liu riu sôi sùng sục, hương thơm ngào ngạt cả góc phố!"
            ),
            new(
                id: "Stall_HanhLa",
                displayName: "Gánh Rau Tươi Cô Lan",
                itemId: ItemIds.HanhLa,
                itemName: "Hành lá & Rau mùi tươi",
                position: new Vector2(440f, 420f),
                grantDialogue: "Hành hoa củ chẻ và rau mùi tươi mơn mởn của Cô Lan đây! Rắc lên bát phở là thơm ngát cả góc phố!",
                reminderDialogue: "Cô Lan đã tặng cháu bó hành mùi tươi ngon nhất sớm nay rồi nhé!",
                preQuestDialogue: "Rau thơm vườn bãi sông Hồng Cô Lan mới hái sớm nay, xanh non mởn!"
            ),
            new(
                id: "Stall_ThaoMoc",
                displayName: "Hiệu Thuốc Bắc Lãn Ông",
                itemId: ItemIds.ThaoMoc,
                itemName: "Túi Thảo mộc hồi quế Lãn Ông",
                position: new Vector2(660f, 430f),
                grantDialogue: "Gói hoa hồi, quế chi, thảo quả sao vàng thơm lựng của tiệm thuốc bắc Lãn Ông đây! Linh hồn của nồi phở Hà Nội đấy cháu!",
                reminderDialogue: "Gói quế hồi thảo quả gia truyền chú đã gói ghém cẩn thận cho cháu rồi đấy!",
                preQuestDialogue: "Mùi hoa hồi và quế chi ngào ngạt khắp tiệm Lãn Ông trăm năm tuổi."
            )
        };

        return (baAn, merchants);
    }
}

/// <summary>
/// Long Biên Bridge Gate Interactable entity at eastern border of Hanoi exploration.
/// Blocks passage until stalls are cleared in Quest 3; then opens portal to World Map.
/// </summary>
public sealed class LongBienGateInteractable : IInteractable, IYSortable
{
    public string Id => "Gate_LongBien";
    public string DisplayName => "Cổng Cầu Long Biên";
    public Vector2 Position { get; set; } = new(1480f, 480f);
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 64.0f;
    public float VisualHeight => 60.0f;

    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) =>
        context.QuestFlags.Gate_LongBien_Unlocked
            ? "[E] Đi qua Cầu Long Biên (Bản Đồ Thế Giới)"
            : (context.QuestFlags.Q2_Completed
                ? "[E] Dọn dẹp sạp hàng mở lối"
                : "[E] Cổng Cầu Long Biên (Bị chặn bởi sạp hàng)");

    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.QuestFlags.Gate_LongBien_Unlocked)
        {
            if (context.QuestFlags.Q2_Completed)
            {
                return new InteractionResult(
                    Success: true,
                    DialogueSpeaker: "Bút Chì",
                    DialogueText: "Lối lên Cầu Long Biên hiện đang bị các sạp hàng và xe cộ chặn kín. Hãy giúp các tiểu thương sắp xếp lại đường đi nào!"
                );
            }

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: "Bút Chì",
                DialogueText: "Đường ra Cầu Long Biên đang bị các sạp hàng hoa và xe xích lô chặn kín rồi! Mây hãy giúp Bà An nấu phở trước đã!"
            );
        }

        return new InteractionResult(
            Success: true,
            DialogueSpeaker: "Bút Chì",
            DialogueText: "Lối đi đã thông suốt! Chúng ta cùng đi qua Cầu Long Biên để ngắm nhìn toàn cảnh sông Hồng và tiến vào Cố Đô Huế nào!",
            QuestCompleted: false
        );
    }
}


