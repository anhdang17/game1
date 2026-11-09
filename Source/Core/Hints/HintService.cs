using System;
using GameProject.Core.Models;
using GameProject.Core.Quests;

namespace GameProject.Core.Hints;

public interface IHintService
{
    string GetCurrentHint(string activeQuestId, Level1State state);
    bool CheckIdlePrompt(float idleSeconds, int failedAttemptsCount, out string idlePrompt);
}

public sealed class HintManager : IHintService
{
    public const float IdleThresholdSeconds = 45.0f;

    public string GetCurrentHint(string activeQuestId, Level1State state)
    {
        if (state == null) return "Bút Chì luôn ở đây cùng Bé Mây! Hãy khám phá phố cổ Hà Nội nhé!";

        switch (activeQuestId)
        {
            case QuestIds.Hanoi01:
                int count = state.QuestFlags.GetQ1CollectedCount();
                if (count == 5)
                {
                    return "Đủ cả 5 nguyên liệu rồi! Mau mang về cho Bà Cụ Bán Phở kẻo nước hầm nguội mất Mây ơi!";
                }
                if (!state.QuestFlags.Q1_ThaoMoc_Collected)
                {
                    return "Mùi hoa hồi và quế chi thơm lừng từ hiệu thuốc Lãn Ông kia kìa Mây! Vào hỏi cô bán hàng thử đi!";
                }
                if (!state.QuestFlags.Q1_HanhLa_Collected)
                {
                    return "Phở ngon không thể thiếu hành hoa và rau mùi tươi đâu! Thử lại gần gánh rau kia xem sao!";
                }
                return $"Đã tìm được {count}/5 nguyên liệu. Tiếp tục tìm các quầy hàng xung quanh nhé!";

            case QuestIds.Hanoi02:
                return "Hãy nhớ quy tắc nấu phở: Bánh phở ở đáy, thịt bò trải đều, rắc hành hoa, chan nước sôi rồi mới thêm gia vị!";

            case QuestIds.Hanoi03:
                if (state.QuestFlags.Gate_LongBien_Unlocked)
                {
                    return "Lối đi ra Cầu Long Biên đã mở rồi! Mau tới xem vẻ đẹp của cây cầu lịch sử thôi!";
                }
                return "Chiếc xe xích lô dài quá chắn ngang đường rồi. Thử đẩy sạp hoa và xe hoa sang bên để mở đường nhé! Nếu bị kẹt, bấm phím [R] để sắp xếp lại từ đầu!";

            default:
                if (state.QuestFlags.Gate_LongBien_Unlocked)
                {
                    return "Lối đi ra Cầu Long Biên đã mở rồi! Mau tới xem vẻ đẹp của cây cầu lịch sử thôi!";
                }
                return "Bé Mây hãy tiếp tục hành trình khôi phục màu sắc cho thế giới nhé!";
        }
    }

    public bool CheckIdlePrompt(float idleSeconds, int failedAttemptsCount, out string idlePrompt)
    {
        if (idleSeconds >= IdleThresholdSeconds || failedAttemptsCount >= 3)
        {
            idlePrompt = "Mây ơi, cần Bút Chì giúp một tay không? Hãy bấm [H] để nghe gợi ý nhé!";
            return true;
        }

        idlePrompt = string.Empty;
        return false;
    }
}
