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
/// Mệ Bích (Bún Bò Huế artisan vendor) NPC — Primary quest giver and culinary host for Cố Đô Huế.
/// Guides Bé Mây to gather 5 authentic ingredients across the citadel market, then hosts the
/// royal porcelain culinary assembly minigame.
/// </summary>
public sealed class MeBichNpc : IInteractable, IYSortable
{
    public string Id => "Npc_MeBich";
    public string DisplayName => "Mệ Bích";
    public Vector2 Position { get; set; }
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 48.0f;
    public float VisualHeight => 48.0f;

    // IYSortable
    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public MeBichNpc(Vector2 position) => Position = position;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context)
    {
        if (context.QuestFlags.Hue_Q2_Completed) return "[E] Nói chuyện";
        if (context.QuestFlags.Hue_Q1_Completed) return "[E] Nấu Bún Bò Huế";
        if (context.QuestFlags.AreAllHueQ1IngredientsCollected()) return "[E] Trả nhiệm vụ";
        return context.QuestFlags.Hue_Q1_Started ? "[E] Hỏi thăm" : "[E] Tương tác";
    }

    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Ensure Hue quests are registered
        if (context.QuestManager.GetQuest(HueQuests.Quest1Id) == null)
        {
            HueQuests.RegisterAll(context.QuestManager);
        }

        // State 1: Quest 1 Not Started
        if (!context.QuestFlags.Hue_Q1_Started)
        {
            context.QuestManager.StartQuest(HueQuests.Quest1Id);
            context.QuestFlags.Hue_Q1_Started = true;
            context.AudioService.PlaySfx(SfxType.ButtonClick);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Chào hai cháu nhỏ... Cố Đô dạo ni ảm đạm quá, sông Hương lững lờ trôi mà vắng bóng tiếng đò ca. Mệ muốn nấu nồi Bún Bò Huế gia truyền để thết đãi bà con tìm lại niềm vui, ngặt nỗi thiếu mất nguyên liệu. Cháu giúp mệ tìm đủ 5 thức: bún sợi to làng Vân Cù, bắp bò hoa tươi, chả cua quết nhuyễn, nước dùng ruốc sả đậm đà, và dĩa rau sống hoa chuối nhé!",
                QuestStarted: true
            );
        }

        // State 2: Requirements Met -> Complete Quest 1
        if (context.QuestFlags.AreAllHueQ1IngredientsCollected() && !context.QuestFlags.Hue_Q1_Completed)
        {
            var q1 = context.QuestManager.GetQuest(HueQuests.Quest1Id);
            if (q1 != null)
            {
                if (context.QuestFlags.Hue_Q1_BunSoiTo_Collected) q1.GetObjective(ItemIds.BunSoiTo)?.SetProgress(1);
                if (context.QuestFlags.Hue_Q1_BapBo_Collected) q1.GetObjective(ItemIds.BapBo)?.SetProgress(1);
                if (context.QuestFlags.Hue_Q1_ChaCua_Collected) q1.GetObjective(ItemIds.ChaCua)?.SetProgress(1);
                if (context.QuestFlags.Hue_Q1_NuocDungHue_Collected) q1.GetObjective(ItemIds.NuocDungHue)?.SetProgress(1);
                if (context.QuestFlags.Hue_Q1_RauSongHue_Collected) q1.GetObjective(ItemIds.RauSongHue)?.SetProgress(1);
                q1.State = QuestState.RequirementsMet;
            }

            context.QuestManager.CompleteQuest(HueQuests.Quest1Id);
            context.QuestFlags.Hue_Q1_Completed = true;
            context.AudioService.PlaySfx(SfxType.PhoCookSuccess);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Ôi chao, đủ cả 5 nguyên liệu hảo hạng rồi! Bún sợi to Vân Cù, bắp bò hoa, chả cua thơm lừng, nước dùng ruốc sả và rổ rau sống tươi roi rói! Giờ mệ cháu mình cùng bắt tay vào nấu và bày biện tô Bún Bò Huế chuẩn vị Cố Đô nhé!",
                QuestCompleted: true
            );
        }

        // State 3: Quest 2 Completed
        if (context.QuestFlags.Hue_Q2_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Tô Bún Bò Huế thơm lừng, đỏ au màu ớt màu ruốc của mệ cháu mình đây rồi! Hương vị cố đô lan tỏa làm ấm lòng cả xóm nhỏ. Cháu hãy ra bến thuyền Sông Hương gặp bác lái đò nghen, đêm nay có hội thả đèn hoa đăng lung linh lắm đó!"
            );
        }

        // State 4: Quest 1 Completed, Quest 2 Incomplete
        if (context.QuestFlags.Hue_Q1_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Mệ cháu mình cùng vào bàn bếp nấu và bày biện tô Bún Bò Huế chuẩn vị Cố Đô nào cháu yêu!"
            );
        }

        // State 5: In Progress Reminder
        int count = context.QuestFlags.GetHueQ1CollectedCount();
        return new InteractionResult(
            Success: true,
            DialogueSpeaker: DisplayName,
            DialogueText: $"Cháu gom đủ 5 thứ chưa? Đã được {count}/5 món rồi: Bún Vân Cù, bắp bò, chả cua, nước dùng ruốc sả với rau sống bắp chuối... Thiếu một vị là không ra chất Bún Bò Huế mô nhen!"
        );
    }
}

/// <summary>
/// Specialized market merchant stall granting a specific Huế culinary ingredient upon interaction.
/// </summary>
public sealed class HueIngredientVendor : IInteractable, IYSortable
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

    public HueIngredientVendor(
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
        if (!context.QuestFlags.Hue_Q1_Started)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _preQuestDialogue
            );
        }

        // Ensure QuestManager has Hue quests registered
        if (context.QuestManager.GetQuest(HueQuests.Quest1Id) == null)
        {
            HueQuests.RegisterAll(context.QuestManager);
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
            context.QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemId, 1);

            switch (ItemId)
            {
                case ItemIds.BunSoiTo:
                    context.QuestFlags.Hue_Q1_BunSoiTo_Collected = true;
                    break;
                case ItemIds.BapBo:
                    context.QuestFlags.Hue_Q1_BapBo_Collected = true;
                    break;
                case ItemIds.ChaCua:
                    context.QuestFlags.Hue_Q1_ChaCua_Collected = true;
                    break;
                case ItemIds.NuocDungHue:
                    context.QuestFlags.Hue_Q1_NuocDungHue_Collected = true;
                    break;
                case ItemIds.RauSongHue:
                    context.QuestFlags.Hue_Q1_RauSongHue_Collected = true;
                    break;
            }

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
/// Sông Hương Boatman NPC at stone pier — Coordinates river lore, lantern festival, and Quest 3 puzzle modal.
/// </summary>
public sealed class SongHuongBoatmanNpc : IInteractable, IYSortable
{
    public string Id => "Npc_SongHuong";
    public string DisplayName => "Bác Lái Đò Sông Hương";
    public Vector2 Position { get; set; }
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 56.0f;
    public float VisualHeight => 48.0f;

    // IYSortable
    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public SongHuongBoatmanNpc(Vector2 position) => Position = position;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context)
    {
        if (context.QuestFlags.Hue_Q3_Completed) return "[E] Ngắm dòng Sông Hương";
        if (context.QuestFlags.Hue_Q2_Completed) return "[E] Thả Đèn Hoa Đăng";
        return "[E] Bến Sông Hương";
    }

    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.QuestFlags.Hue_Q3_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Kỳ diệu quá! Hàng trăm đóa hoa đăng bừng sáng rực rỡ trôi dọc dòng Hương Giang, phản chiếu lên Tháp Phước Duyên Chùa Thiên Mụ và nhịp cầu Tràng Tiền! Sắc màu Cố Đô đã hoàn toàn hồi sinh! Lối qua Cầu Tràng Tiền đã rộng mở để các cháu tiến vào Hội An rồi đó!"
            );
        }

        if (context.QuestFlags.Hue_Q2_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Chào cháu bé! Bến sông Hương đang chuẩn bị cho hội thả đèn hoa đăng cầu an và phục hồi sắc màu cho Cố Đô. Những đóa hoa đăng đang cần được thắp sáng theo đúng nhịp điệu ca Huế trên sông. Cháu hãy cùng bác dọn luồng nước thả đèn nhé!"
            );
        }

        return new InteractionResult(
            Success: true,
            DialogueSpeaker: DisplayName,
            DialogueText: "Chào hai cháu nhỏ! Bến Sông Hương đang đợi chuẩn bị cho hội đêm hoa đăng lung linh. Cháu hãy ghé thăm mệ Bích ở sân trước thưởng thức tô bún bò Huế đậm đà trước nghen!"
        );
    }
}

/// <summary>
/// Cultural inspection trigger for Ngọ Môn & Imperial Citadel.
/// </summary>
public sealed class NgoMonGateInteractable : IInteractable, IYSortable
{
    public string Id => "PoI_NgoMon";
    public string DisplayName => "Cổng Ngọ Môn";
    public Vector2 Position { get; set; } = new(770f, 340f);
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 70.0f;
    public float VisualHeight => 50.0f;

    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) => "[E] Chiêm ngưỡng Ngọ Môn";

    public InteractionResult Interact(InteractionContext context)
    {
        return new InteractionResult(
            Success: true,
            DialogueSpeaker: "Bút Chì",
            DialogueText: "Ngọ Môn - Cổng chính phía Nam của Hoàng Thành Huế xây dựng thời vua Minh Mạng, với lầu Ngũ Phụng mái lợp ngói hoàng lưu ly và thanh lưu ly uy nghiêm."
        );
    }
}

/// <summary>
/// Cultural inspection trigger for Chùa Thiên Mụ & Tháp Phước Duyên.
/// </summary>
public sealed class ThienMuPagodaInteractable : IInteractable, IYSortable
{
    public string Id => "PoI_ThienMu";
    public string DisplayName => "Chùa Thiên Mụ";
    public Vector2 Position { get; set; } = new(240f, 330f);
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 70.0f;
    public float VisualHeight => 50.0f;

    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) => "[E] Viếng Chùa Thiên Mụ";

    public InteractionResult Interact(InteractionContext context)
    {
        return new InteractionResult(
            Success: true,
            DialogueSpeaker: "Bút Chì",
            DialogueText: "Chùa Thiên Mụ cổ kính soi bóng bên dòng sông Hương, tiếng chuông chùa ngân vang sớm chiều hòa quyện cùng tháp Phước Duyên trầm mặc."
        );
    }
}

/// <summary>
/// Cầu Tràng Tiền bridge gate interactable at eastern border.
/// Unlocks after Sông Hương lantern festival climax (Q3); leads to Hội An and World Map.
/// </summary>
public sealed class TrangTienBridgeInteractable : IInteractable, IYSortable
{
    public string Id => "Gate_TrangTien";
    public string DisplayName => "Cầu Tràng Tiền";
    public Vector2 Position { get; set; } = new(1460f, 530f);
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 80.0f;
    public float VisualHeight => 60.0f;

    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context)
    {
        if (context.QuestFlags.Gate_HoiAn_Unlocked || context.QuestFlags.Hue_Q3_Completed)
            return "[E] Vượt Cầu Tràng Tiền (Tiến vào Hội An)";
        return "[E] Cầu Tràng Tiền (Đang chuẩn bị Dạ hội Hoa Đăng)";
    }

    public InteractionResult Interact(InteractionContext context)
    {
        if (context.QuestFlags.Gate_HoiAn_Unlocked || context.QuestFlags.Hue_Q3_Completed)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: "Bút Chì",
                DialogueText: "Cầu Tràng Tiền lung linh soi bóng dòng sông Hương! Lối đi đã thông suốt, sẵn sàng vượt dãy Bạch Mã để tiến vào Di sản Phố cổ Hội An!"
            );
        }

        return new InteractionResult(
            Success: true,
            DialogueSpeaker: "Bút Chì",
            DialogueText: "Cầu Tràng Tiền sáu vài mười hai nhịp đang chuẩn bị cho Dạ hội Hoa Đăng. Hãy hoàn thành các hoạt động văn hóa tại Cố Đô để mở lối vượt cầu!"
        );
    }
}

/// <summary>
/// Factory generating the complete set of Cố Đô Huế merchants, landmarks, and interactables.
/// </summary>
public static class HueInteractableFactory
{
    public static (
        MeBichNpc MeBich,
        List<HueIngredientVendor> Vendors,
        SongHuongBoatmanNpc Boatman,
        NgoMonGateInteractable NgoMon,
        ThienMuPagodaInteractable ThienMu,
        TrangTienBridgeInteractable TrangTien
    ) CreateHueInteractables()
    {
        var meBich = new MeBichNpc(new Vector2(860f, 430f));
        var boatman = new SongHuongBoatmanNpc(new Vector2(600f, 610f));
        var ngoMon = new NgoMonGateInteractable();
        var thienMu = new ThienMuPagodaInteractable();
        var trangTien = new TrangTienBridgeInteractable();

        var vendors = new List<HueIngredientVendor>
        {
            new(
                id: "Stall_BunSoiTo",
                displayName: "O Sen Làng Vân Cù",
                itemId: ItemIds.BunSoiTo,
                itemName: "Bún sợi to làng Vân Cù",
                position: new Vector2(340f, 400f),
                grantDialogue: "Bún sợi to tráng thủ công của làng bún Vân Cù đây cháu! Sợi bún trắng ngần, dai mềm thấm đượm phong vị quê hương. Cháu mang về cho mệ Bích nghen!",
                reminderDialogue: "O đã trao cho cháu phần bún sợi to tươi ngon nhất rồi đó! Mau mang về cho mệ Bích nhé!",
                preQuestDialogue: "Bún sợi to làng Vân Cù tráng sớm nay trắng ngần dẻo thơm. Cháu qua chào mệ Bích ở sân trước chưa?"
            ),
            new(
                id: "Stall_BapBo",
                displayName: "Chú Năm Thịt Bò",
                itemId: ItemIds.BapBo,
                itemName: "Bắp bò hoa tươi ngon",
                position: new Vector2(500f, 380f),
                grantDialogue: "Bắp bò hoa tươi roi rói, luộc lên giòn sần sật ngọt lịm của chú Năm đây! Nấu bún bò là phải có bắp bò ni mới đúng điệu!",
                reminderDialogue: "Chú Năm đã chọn cho cháu phần bắp bò hoa ngon nhất rồi đó! Mau mang về cho mệ Bích nhé!",
                preQuestDialogue: "Thịt bắp bò hoa sáng nay tươi roi rói thơm ngọt, nấu bún bò là ngon tuyệt đỉnh!"
            ),
            new(
                id: "Stall_ChaCua",
                displayName: "Dì Ba Chả Cua",
                itemId: ItemIds.ChaCua,
                itemName: "Chả cua quết nhuyễn đậm đà",
                position: new Vector2(680f, 380f),
                grantDialogue: "Chả cua quết nhuyễn trộn gạch cua đồng đỏ au thơm phức của Dì Ba đây! Thả vào nồi bún bò nổi bồng bềnh béo ngậy!",
                reminderDialogue: "Dì Ba đã gửi cháu phần chả cua tươi ngon nhất rồi đó! Mang về cho mệ Bích sớm nhé!",
                preQuestDialogue: "Chả cua xứ Huế quết tay nhuyễn mịn đỏ au màu gạch đồng quê."
            ),
            new(
                id: "Stall_NuocDungHue",
                displayName: "Mệ Năm Nồi Nước Lèo",
                itemId: ItemIds.NuocDungHue,
                itemName: "Nước dùng ruốc sả thơm nồng",
                position: new Vector2(980f, 400f),
                grantDialogue: "Nồi nước dùng hầm xương bò dậy mùi mắm ruốc sả ớt gia truyền đây cháu! Mệ đã chắt sẵn vào liễn sứ, cẩn thận kẻo nóng nghen!",
                reminderDialogue: "Liễn nước dùng ruốc sả thơm nồng mệ đã chuẩn bị chu đáo cho cháu rồi đó!",
                preQuestDialogue: "Nồi nước lèo ninh xương bò cùng mắm ruốc và sả thơm ngạt ngào góc phố."
            ),
            new(
                id: "Stall_RauSongHue",
                displayName: "Cô Thảo Gánh Rau",
                itemId: ItemIds.RauSongHue,
                itemName: "Rau sống bắp chuối bào thanh mát",
                position: new Vector2(1120f, 420f),
                grantDialogue: "Rau thơm, bắp chuối bào mỏng tang và giá đỗ tươi non của Cô Thảo đây! Ăn kèm bún bò Huế là thanh mát tròn vị!",
                reminderDialogue: "Cô Thảo đã trao tặng cháu rổ rau ghém bắp chuối tươi mát nhất rồi nhé!",
                preQuestDialogue: "Gánh rau ghém, hoa chuối xứ Huế thanh mát gánh từ bến sông lên sớm mai."
            )
        };

        return (meBich, vendors, boatman, ngoMon, thienMu, trangTien);
    }
}
