using GameProject.Core.Hints;
using GameProject.Core.Models;
using Xunit;

namespace GameProject.Tests;

public class HintTests
{
    [Fact]
    public void OnDemandHint_Quest1_MissingThaoMoc_PointsToLanOngStreet()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = true;
        state.QuestFlags.Q1_BanhPho_Collected = true;
        state.QuestFlags.Q1_ThitBo_Collected = true;
        state.QuestFlags.Q1_NuocDung_Collected = true;
        state.QuestFlags.Q1_HanhLa_Collected = true;
        // Q1_ThaoMoc_Collected is false

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("Lãn Ông", hint);
        Assert.Contains("hồi", hint);
    }

    [Fact]
    public void OnDemandHint_Quest1_AllIngredientsCollected_PromptsToReturnToBaAn()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = true;
        state.QuestFlags.Q1_BanhPho_Collected = true;
        state.QuestFlags.Q1_ThitBo_Collected = true;
        state.QuestFlags.Q1_NuocDung_Collected = true;
        state.QuestFlags.Q1_HanhLa_Collected = true;
        state.QuestFlags.Q1_ThaoMoc_Collected = true;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("Bà Cụ Bán Phở", hint);
        Assert.Contains("Đủ cả 5 nguyên liệu", hint);
    }

    [Fact]
    public void OnDemandHint_Quest2_ExplainsPhoLayeringRule()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi02;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi02, state);

        Assert.Contains("Bánh phở ở đáy", hint);
        Assert.Contains("nước sôi", hint);
    }

    [Fact]
    public void OnDemandHint_Quest3_MentionsResetKeyWhenStuck()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi03;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi03, state);

        Assert.Contains("[R]", hint);
        Assert.Contains("sắp xếp lại", hint);
    }

    [Fact]
    public void IdlePrompt_ActivatesAfter45SecondsOfInactivity()
    {
        var hintManager = new HintManager();

        // 30 seconds idle -> should NOT trigger
        bool triggered30s = hintManager.CheckIdlePrompt(30.0f, 0, out string prompt30s);
        Assert.False(triggered30s);
        Assert.Empty(prompt30s);

        // 45 seconds idle -> MUST trigger
        bool triggered45s = hintManager.CheckIdlePrompt(45.0f, 0, out string prompt45s);
        Assert.True(triggered45s);
        Assert.Contains("Bút Chì", prompt45s);
        Assert.Contains("[H]", prompt45s);
    }

    [Fact]
    public void IdlePrompt_ActivatesAfter3FailedPuzzleAttempts()
    {
        var hintManager = new HintManager();

        bool triggered = hintManager.CheckIdlePrompt(10.0f, 3, out string prompt);

        Assert.True(triggered);
        Assert.Contains("Bút Chì", prompt);
    }
}
