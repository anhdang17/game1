using System.Collections.Generic;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.UI;
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
        state.QuestFlags.Q1_ThaoMoc_Collected = false;

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
    public void OnDemandHint_Quest1_MissingHanhLa_PointsToVegetableStall()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = true;
        state.QuestFlags.Q1_ThaoMoc_Collected = true;
        state.QuestFlags.Q1_HanhLa_Collected = false;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("hành hoa", hint);
        Assert.Contains("gánh rau", hint);
    }

    [Fact]
    public void OnDemandHint_Quest1_MissingNuocDung_PointsToBrothStall()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = true;
        state.QuestFlags.Q1_ThaoMoc_Collected = true;
        state.QuestFlags.Q1_HanhLa_Collected = true;
        state.QuestFlags.Q1_NuocDung_Collected = false;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("nước dùng", hint);
        Assert.Contains("bếp", hint);
    }

    [Fact]
    public void OnDemandHint_Quest1_MissingThitBo_PointsToBacTuBeefStall()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = true;
        state.QuestFlags.Q1_ThaoMoc_Collected = true;
        state.QuestFlags.Q1_HanhLa_Collected = true;
        state.QuestFlags.Q1_NuocDung_Collected = true;
        state.QuestFlags.Q1_ThitBo_Collected = false;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("Thịt bò", hint);
        Assert.Contains("Bác Tư", hint);
    }

    [Fact]
    public void OnDemandHint_Quest1_MissingBanhPho_PointsToHangBuomNoodles()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = true;
        state.QuestFlags.Q1_ThaoMoc_Collected = true;
        state.QuestFlags.Q1_HanhLa_Collected = true;
        state.QuestFlags.Q1_NuocDung_Collected = true;
        state.QuestFlags.Q1_ThitBo_Collected = true;
        state.QuestFlags.Q1_BanhPho_Collected = false;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("Bánh Phở", hint);
        Assert.Contains("Hàng Buồm", hint);
    }

    [Fact]
    public void OnDemandHint_Quest1_NotStarted_PromptsToMeetBaAn()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Started = false;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("Bà An", hint);
    }

    [Fact]
    public void OnDemandHint_Quest1_Completed_PromptsToAssemblePho()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi01;
        state.QuestFlags.Q1_Completed = true;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi01, state);

        Assert.Contains("bát phở", hint);
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
    public void OnDemandHint_Quest2_Completed_DirectsToEastLongBien()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi02;
        state.QuestFlags.Q2_Completed = true;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi02, state);

        Assert.Contains("Long Biên", hint);
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
    public void OnDemandHint_Quest3_CompletedOrGateUnlocked_PointsToBridgeGate()
    {
        var hintManager = new HintManager();
        var state = Level1State.CreateDefault();
        state.ActiveQuestId = QuestIds.Hanoi03;
        state.QuestFlags.Gate_LongBien_Unlocked = true;

        string hint = hintManager.GetCurrentHint(QuestIds.Hanoi03, state);

        Assert.Contains("Cầu Long Biên đã mở", hint);
    }

    [Fact]
    public void IdlePrompt_ActivatesAfter45SecondsOfInactivity()
    {
        var hintManager = new HintManager();

        // 30 seconds idle -> should NOT trigger
        bool triggered30s = hintManager.CheckIdlePrompt(30.0f, 0, out string prompt30s);
        Assert.False(triggered30s);
        Assert.Empty(prompt30s);

        // 44.9 seconds idle -> should NOT trigger
        bool triggered449 = hintManager.CheckIdlePrompt(44.9f, 0, out string prompt449);
        Assert.False(triggered449);

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

        // 2 failures -> should NOT trigger
        bool triggered2 = hintManager.CheckIdlePrompt(10.0f, 2, out string prompt2);
        Assert.False(triggered2);

        // 3 failures -> MUST trigger
        bool triggered3 = hintManager.CheckIdlePrompt(10.0f, 3, out string prompt3);
        Assert.True(triggered3);
        Assert.Contains("Bút Chì", prompt3);
    }

    [Fact]
    public void PhoAssembly_StepByStepHints_CoverAll5Slots()
    {
        var hintManager = new HintManager();
        var slots = new string?[5];

        string hint0 = hintManager.GetPhoAssemblyHint(0, slots);
        Assert.Contains("bánh phở", hint0.ToLower());

        string hint1 = hintManager.GetPhoAssemblyHint(1, slots);
        Assert.Contains("thịt bò", hint1.ToLower());

        string hint2 = hintManager.GetPhoAssemblyHint(2, slots);
        Assert.Contains("hành hoa", hint2.ToLower());

        string hint3 = hintManager.GetPhoAssemblyHint(3, slots);
        Assert.Contains("nước dùng", hint3.ToLower());

        string hint4 = hintManager.GetPhoAssemblyHint(4, slots);
        Assert.Contains("gia vị", hint4.ToLower());
    }

    [Fact]
    public void StallPuzzle_ClearAndMoveHints()
    {
        var hintManager = new HintManager();

        string initialHint = hintManager.GetStallPuzzleHint(isPathClear: false, moveCount: 0);
        Assert.Contains("sạp hoa quả", initialHint);
        Assert.Contains("xe hoa", initialHint);

        string clearHint = hintManager.GetStallPuzzleHint(isPathClear: true, moveCount: 2);
        Assert.Contains("thông suốt", clearHint);
    }

    [Fact]
    public void ButChiGuideUI_Headless_IdleTimerAccumulatesAndAlerts()
    {
        var hintManager = new HintManager();
        var guide = new ButChiGuideUI(hintManager);
        var state = Level1State.CreateDefault();

        Assert.Equal(0f, guide.IdleSeconds);
        Assert.False(guide.IsIdleAlertActive);

        // Advance 20 seconds
        guide.Update(20.0f, QuestIds.Hanoi01, state);
        Assert.Equal(20.0f, guide.IdleSeconds);
        Assert.False(guide.IsIdleAlertActive);

        // Advance 25 more seconds -> Total 45.0s
        guide.Update(25.0f, QuestIds.Hanoi01, state);
        Assert.True(guide.IdleSeconds >= 45.0f);
        Assert.True(guide.IsIdleAlertActive);
    }

    [Fact]
    public void ButChiGuideUI_Headless_TriggerHintClearsIdleAndPopulatesHint()
    {
        var hintManager = new HintManager();
        var guide = new ButChiGuideUI(hintManager);
        var state = Level1State.CreateDefault();

        // Put into idle state
        guide.Update(46.0f, QuestIds.Hanoi01, state);
        Assert.True(guide.IsIdleAlertActive);

        // Trigger hint
        guide.TriggerHint(QuestIds.Hanoi01, state);

        Assert.False(guide.IsIdleAlertActive);
        Assert.Equal(0f, guide.IdleSeconds);
        Assert.True(guide.IsHintActive);
        Assert.NotEmpty(guide.CurrentHintText);
    }

    [Fact]
    public void ButChiGuideUI_Headless_NotifyProgressResetsIdleTimer()
    {
        var hintManager = new HintManager();
        var guide = new ButChiGuideUI(hintManager);
        var state = Level1State.CreateDefault();

        guide.Update(40.0f, QuestIds.Hanoi01, state);
        Assert.Equal(40.0f, guide.IdleSeconds);

        // Player collects an ingredient
        guide.NotifyProgress();

        Assert.Equal(0f, guide.IdleSeconds);
        Assert.False(guide.IsIdleAlertActive);
    }

    [Fact]
    public void ButChiGuideUI_Headless_NotifyFailureTriggersAlertAfter3Attempts()
    {
        var hintManager = new HintManager();
        var guide = new ButChiGuideUI(hintManager);

        guide.NotifyFailure();
        Assert.Equal(1, guide.FailedAttemptsCount);
        Assert.False(guide.IsIdleAlertActive);

        guide.NotifyFailure();
        Assert.Equal(2, guide.FailedAttemptsCount);
        Assert.False(guide.IsIdleAlertActive);

        guide.NotifyFailure();
        Assert.Equal(3, guide.FailedAttemptsCount);
        Assert.True(guide.IsIdleAlertActive);
    }
}
