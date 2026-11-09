using System;
using System.Collections.Generic;
using GameProject.Core.Models;
using GameProject.Core.Quests;

namespace GameProject.Core.Hints;

/// <summary>
/// Domain contract for companion hint generation, quest contextual advice, and idle progression detection.
/// Completely decoupled from display and windowing libraries (zero Raylib dependencies) for headless testability.
/// </summary>
public interface IHintService
{
    /// <summary>
    /// Generates actionable contextual hint for the current active quest and game state.
    /// </summary>
    string GetCurrentHint(string activeQuestId, Level1State state);

    /// <summary>
    /// Evaluates whether an idle nudge or assistance prompt should be offered to the player.
    /// Triggers when player has spent >= 45s without quest progression or failed a puzzle >= 3 times.
    /// </summary>
    bool CheckIdlePrompt(float idleSeconds, int failedAttemptsCount, out string idlePrompt);

    /// <summary>
    /// Provides step-by-step guidance for Quest 2 (Phở Assembly) mini-game.
    /// </summary>
    string GetPhoAssemblyHint(int emptySlotIndex, IReadOnlyList<string?> currentSlots);

    /// <summary>
    /// Provides spatial guidance for Quest 3 (Stall Grid BFS) sliding clearance puzzle.
    /// </summary>
    string GetStallPuzzleHint(bool isPathClear, int moveCount);
}

/// <summary>
/// Domain hint rules engine mapping player progression, quest flags, and environmental obstacles to Bút Chì companion advice.
/// </summary>
public sealed class HintManager : IHintService
{
    public const float IdleThresholdSeconds = 45.0f;
    public const int FailedAttemptsThreshold = 3;

    public string GetCurrentHint(string activeQuestId, Level1State state)
    {
        if (state == null)
        {
            return "Bút Chì luôn ở đây cùng Bé Mây! Hãy khám phá phố cổ Hà Nội nhé!";
        }

        switch (activeQuestId)
        {
            case QuestIds.Hanoi01:
                return GetQuest1Hint(state);

            case QuestIds.Hanoi02:
                return GetQuest2Hint(state);

            case QuestIds.Hanoi03:
                return GetQuest3Hint(state);

            default:
                if (state.QuestFlags.Gate_LongBien_Unlocked)
                {
                    return "Lối đi ra Cầu Long Biên đã mở rồi! Mau tới xem vẻ đẹp của cây cầu lịch sử thôi!";
                }
                return "Bé Mây hãy tiếp tục hành trình khôi phục màu sắc cho thế giới nhé!";
        }
    }

    private static string GetQuest1Hint(Level1State state)
    {
        if (state.QuestFlags.Q1_Completed)
        {
            return "Nhiệm vụ thu thập nguyên liệu đã xong! Hãy vào bàn ăn cùng Bà An để bắt đầu ghép bát phở nhé!";
        }

        if (!state.QuestFlags.Q1_Started)
        {
            return "Bé Mây hãy đến chào hỏi Bà An ở quán phở đầu phố để bắt đầu hành trình nhé!";
        }

        int count = state.QuestFlags.GetQ1CollectedCount();
        if (count == 5 || state.QuestFlags.AreAllQ1IngredientsCollected())
        {
            return "Đủ cả 5 nguyên liệu rồi! Mau mang về cho Bà Cụ Bán Phở kẻo nước hầm nguội mất Mây ơi!";
        }

        // Contextual guidance for missing ingredients.
        // NOTE: Q1_ThaoMoc_Collected is checked first to maintain backward compatibility with
        // existing unit tests and E2E playthrough tests asserting "Lãn Ông" on initial state.
        if (!state.QuestFlags.Q1_ThaoMoc_Collected)
        {
            return "Mùi hoa hồi và quế chi thơm lừng từ hiệu thuốc Lãn Ông kia kìa Mây! Vào hỏi cô bán hàng thử đi!";
        }

        if (!state.QuestFlags.Q1_HanhLa_Collected)
        {
            return "Phở ngon không thể thiếu hành hoa và rau mùi tươi đâu! Thử lại gần gánh rau kia xem sao!";
        }

        if (!state.QuestFlags.Q1_NuocDung_Collected)
        {
            return "Nồi nước dùng ninh xương gia truyền đang sôi sùng sục ở bếp nước dùng! Ghé qua nhận bình nước dùng nóng hổi nhé!";
        }

        if (!state.QuestFlags.Q1_ThitBo_Collected)
        {
            return "Thịt bò tươi thái mỏng ở sạp Bác Tư đang thơm nức! Mau lại chào Bác Tư để lấy nạm bò tươi ngon nhé!";
        }

        if (!state.QuestFlags.Q1_BanhPho_Collected)
        {
            return "Quầy Bánh Phở tươi tráng mỏng của Bác Hàng Buồm ở gần đó! Mau qua xin bánh phở dai mềm nhé!";
        }

        return $"Đã tìm được {count}/5 nguyên liệu. Tiếp tục tìm các quầy hàng xung quanh nhé!";
    }

    private static string GetQuest2Hint(Level1State state)
    {
        if (state.QuestFlags.Q2_Completed)
        {
            return "Bát phở thơm ngon chuẩn vị đã hoàn thành! Tiếp theo hãy tiến về phía đông để mở lối ra Cầu Long Biên!";
        }

        return "Hãy nhớ quy tắc nấu phở: Bánh phở ở đáy, thịt bò trải đều, rắc hành hoa, chan nước sôi rồi mới thêm gia vị!";
    }

    private static string GetQuest3Hint(Level1State state)
    {
        if (state.QuestFlags.Gate_LongBien_Unlocked || state.QuestFlags.Q3_Completed)
        {
            return "Lối đi ra Cầu Long Biên đã mở rồi! Mau tới xem vẻ đẹp của cây cầu lịch sử thôi!";
        }

        return "Chiếc xe xích lô dài quá chắn ngang đường rồi. Thử đẩy sạp hoa và xe hoa sang bên để mở đường nhé! Nếu bị kẹt, bấm phím [R] để sắp xếp lại từ đầu!";
    }

    public bool CheckIdlePrompt(float idleSeconds, int failedAttemptsCount, out string idlePrompt)
    {
        if (idleSeconds >= IdleThresholdSeconds || failedAttemptsCount >= FailedAttemptsThreshold)
        {
            idlePrompt = "Mây ơi, cần Bút Chì giúp một tay không? Hãy bấm [H] để nghe gợi ý nhé!";
            return true;
        }

        idlePrompt = string.Empty;
        return false;
    }

    public string GetPhoAssemblyHint(int emptySlotIndex, IReadOnlyList<string?> currentSlots)
    {
        return emptySlotIndex switch
        {
            0 => "Bước 1: Đặt bánh phở tươi tráng mỏng sợi dai dẻo xuống đáy bát trước tiên nhé Mây!",
            1 => "Bước 2: Xếp các lát thịt bò tươi thái mỏng trải đều ngay ngắn trên lớp bánh phở!",
            2 => "Bước 3: Rắc một nắm hành hoa chẻ và rau mùi thơm ngát lên trên lớp thịt bò tươi!",
            3 => "Bước 4: Chan muôi nước dùng hầm xương gia truyền đang sôi sùng sục để làm chín tái thịt và dậy mùi!",
            4 => "Bước 5: Thêm chút gia vị chanh ớt hoặc thảo mộc hoa hồi quế để hoàn thiện hương vị bát phở!",
            _ => "Quy tắc nấu phở: Bánh phở ở đáy, thịt bò trải đều, rắc hành hoa, chan nước sôi rồi mới thêm gia vị!"
        };
    }

    public string GetStallPuzzleHint(bool isPathClear, int moveCount)
    {
        if (isPathClear)
        {
            return "Lối đi ra Cầu Long Biên đã thông suốt rồi! Mau tiến về cổng phía bên phải thôi!";
        }

        if (moveCount == 0)
        {
            return "Gợi ý: Đẩy sạp hoa quả sang trái, rồi kéo xe hoa xuống dưới để mở thông hàng giữa ra Cầu Long Biên!";
        }

        return "Đẩy sạp hoa và xe hoa sang hai bên để mở đường từ trái sang phải hướng ra Cầu Long Biên! Nếu bị kẹt, bấm [R] để sắp xếp lại.";
    }
}
