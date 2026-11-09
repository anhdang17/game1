using System.Collections.Generic;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Quests;
using GameProject.Tests.Mocks;
using Xunit;

namespace GameProject.Tests;

public class QuestFlowTests
{
    [Fact]
    public void Quest1_InitialState_IsRegisteredAndNotStarted()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(QuestIds.Hanoi01));
        Assert.True(qm.CanStartQuest(QuestIds.Hanoi01));
    }

    [Fact]
    public void Quest1_StartQuest_FiresEventAndSetsInProgress()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        QuestStartedEvent? receivedEvent = null;
        using var token = bus.Subscribe<QuestStartedEvent>(e => receivedEvent = e);

        bool started = qm.StartQuest(QuestIds.Hanoi01);

        Assert.True(started);
        Assert.Equal(QuestState.InProgress, qm.GetQuestState(QuestIds.Hanoi01));
        Assert.NotNull(receivedEvent);
        Assert.Equal(QuestIds.Hanoi01, receivedEvent.QuestId);
    }

    [Fact]
    public void Quest1_PartialIngredientCollection_DoesNotCompleteQuest()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        qm.StartQuest(QuestIds.Hanoi01);

        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.BanhPho, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThitBo, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.NuocDung, 1);

        // Only 3 of 5 collected
        Assert.Equal(QuestState.InProgress, qm.GetQuestState(QuestIds.Hanoi01));
    }

    [Fact]
    public void Quest1_CollectingAll5Ingredients_TransitionsToRequirementsMet()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        qm.StartQuest(QuestIds.Hanoi01);

        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.BanhPho, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThitBo, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.NuocDung, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.HanhLa, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThaoMoc, 1);

        Assert.Equal(QuestState.RequirementsMet, qm.GetQuestState(QuestIds.Hanoi01));
    }

    [Fact]
    public void Quest1_TurnInToBaAn_CompletesQuest1_AndEnablesQuest2()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        qm.StartQuest(QuestIds.Hanoi01);

        // Collect all 5
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.BanhPho, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThitBo, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.NuocDung, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.HanhLa, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThaoMoc, 1);

        QuestCompletedEvent? completedEvent = null;
        using var token = bus.Subscribe<QuestCompletedEvent>(e => completedEvent = e);

        // Turn in to Bà An
        bool completed = qm.CompleteQuest(QuestIds.Hanoi01);

        Assert.True(completed);
        Assert.Equal(QuestState.Completed, qm.GetQuestState(QuestIds.Hanoi01));
        Assert.NotNull(completedEvent);
        Assert.Equal(QuestIds.Hanoi01, completedEvent.QuestId);

        // Quest 2 is now unlockable
        Assert.True(qm.CanStartQuest(QuestIds.Hanoi02));
    }

    [Fact]
    public void Quest2_CannotStartBeforeQuest1IsComplete()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        // Attempting to start Quest 2 when Quest 1 is not done
        Assert.False(qm.CanStartQuest(QuestIds.Hanoi02));
        bool started = qm.StartQuest(QuestIds.Hanoi02);
        Assert.False(started);
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(QuestIds.Hanoi02));
    }

    [Fact]
    public void DuplicateIngredientCollection_IsIdempotent()
    {
        var driver = new TestSimulationDriver();
        driver.QuestManager.StartQuest(QuestIds.Hanoi01);

        // Collect Item_BanhPho twice
        driver.SimulateCollectIngredient(ItemIds.BanhPho);
        int count1 = driver.CurrentData.Level1State.InventoryItems.FindAll(x => x == ItemIds.BanhPho).Count;

        driver.SimulateCollectIngredient(ItemIds.BanhPho);
        int count2 = driver.CurrentData.Level1State.InventoryItems.FindAll(x => x == ItemIds.BanhPho).Count;

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void LevelClimax_GrantsGateUnlock_Page1_LotusStamp_AndUnlocksHue()
    {
        var driver = new TestSimulationDriver();

        // Simulate Q1 -> Q2 -> Q3 -> Climax
        driver.SimulateGatherAllPhoIngredients();
        driver.SimulateSolvePhoCooking();
        driver.SimulateClearStallPath();
        driver.SimulateLevelClimax();

        // Assert all climax rewards
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.GrayShadow_LetterRead);
        Assert.True(driver.CurrentData.Level1State.QuestFlags.Level1_Finished);

        Assert.Contains("JournalPage_Hanoi_01", driver.CurrentData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains("Stamp_HoaSen_Hanoi", driver.CurrentData.CollectionAlbum.CollectedStamps);

        Assert.True(driver.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(driver.CurrentData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.Equal(100.0f, driver.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
    }
}
