extern alias CoreApp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Xunit;

using CoreSimulationDriver = CoreApp::GameProject.Core.Simulation.SimulationDriver;
using CoreGameStateManager = CoreApp::GameProject.Core.State.GameStateManager;
using CoreGameState = CoreApp::GameProject.Core.State.GameState;
using CoreQuestManager = CoreApp::GameProject.Core.Quests.QuestManager;
using CoreHanoiQuests = CoreApp::GameProject.Core.Quests.HanoiQuests;
using CoreQuestState = CoreApp::GameProject.Core.Quests.QuestState;
using CoreQuestObjective = CoreApp::GameProject.Core.Quests.QuestObjective;
using CoreItemIds = CoreApp::GameProject.Core.Models.ItemIds;
using CoreQuestFlags = CoreApp::GameProject.Core.Models.QuestFlags;
using CoreSaveData = CoreApp::GameProject.Core.Models.SaveData;
using CoreEventBus = CoreApp::GameProject.Core.Events.EventBus;
using CoreIEventBus = CoreApp::GameProject.Core.Events.IEventBus;
using CoreQuestStartedEvent = CoreApp::GameProject.Core.Events.QuestStartedEvent;
using CoreQuestProgressUpdatedEvent = CoreApp::GameProject.Core.Events.QuestProgressUpdatedEvent;
using CoreQuestRequirementsMetEvent = CoreApp::GameProject.Core.Events.QuestRequirementsMetEvent;
using CoreQuestCompletedEvent = CoreApp::GameProject.Core.Events.QuestCompletedEvent;
using CoreSaveGameJsonContext = CoreApp::GameProject.Core.Persistence.SaveGameJsonContext;

namespace GameProject.Tests;

/// <summary>
/// Adversarial stress tests and empirical verification suite for Milestone 1:
/// Quest State Machine, Modal State Stack, and Headless Simulation Driver.
/// </summary>
public class AdversarialCoreEngineStressTests
{
    // =========================================================================
    // SECTION 1: QUEST STATE TRANSITIONS & ADVERSARIAL GENERATORS
    // =========================================================================

    [Fact]
    public void Quest_OutOfOrder_Q2_Before_Q1_Complete_IsRejected()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        // Verify initial state
        Assert.False(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));
        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest2Id));

        // Attempt out-of-order start of Quest 2
        bool startedQ2 = qm.StartQuest(CoreHanoiQuests.Quest2Id);
        Assert.False(startedQ2);
        Assert.False(qm.IsQuestInProgress(CoreHanoiQuests.Quest2Id));
        Assert.False(qm.IsQuestCompleted(CoreHanoiQuests.Quest2Id));

        // Verify zero QuestStartedEvents for Q2
        Assert.False(driver.HasEventFired<CoreQuestStartedEvent>(e => e.QuestId == CoreHanoiQuests.Quest2Id));
    }

    [Fact]
    public void Quest_OutOfOrder_Q3_Before_Q2_Complete_IsRejected()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest3Id));
        bool startedQ3 = qm.StartQuest(CoreHanoiQuests.Quest3Id);
        Assert.False(startedQ3);
        Assert.False(qm.IsQuestInProgress(CoreHanoiQuests.Quest3Id));

        // Complete Q1 only, then try Q3
        qm.StartQuest(CoreHanoiQuests.Quest1Id);
        foreach (var item in CoreItemIds.AllPhoIngredients)
        {
            qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, item, 1);
        }
        qm.CompleteQuest(CoreHanoiQuests.Quest1Id);

        // Q1 is complete, but Q2 is still not completed
        Assert.True(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));
        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest3Id));
        Assert.False(qm.StartQuest(CoreHanoiQuests.Quest3Id));
    }

    [Fact]
    public void Quest_StartQ2_WhenQ1_IsRequirementsMet_ButNotTurnedIn_IsRejected()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        qm.StartQuest(CoreHanoiQuests.Quest1Id);

        // Collect all 5 ingredients
        foreach (var item in CoreItemIds.AllPhoIngredients)
        {
            qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, item, 1);
        }

        var q1 = qm.GetQuest(CoreHanoiQuests.Quest1Id);
        Assert.NotNull(q1);
        Assert.Equal(CoreQuestState.RequirementsMet, q1.State);
        Assert.False(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));

        // Even though requirements are met, Q1 is not completed (not turned in to Ba An)
        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest2Id));
        Assert.False(qm.StartQuest(CoreHanoiQuests.Quest2Id));

        // Now complete Q1
        bool completed = qm.CompleteQuest(CoreHanoiQuests.Quest1Id);
        Assert.True(completed);
        Assert.True(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));

        // Q2 must now be startable
        Assert.True(qm.CanStartQuest(CoreHanoiQuests.Quest2Id));
        Assert.True(qm.StartQuest(CoreHanoiQuests.Quest2Id));
        Assert.True(qm.IsQuestInProgress(CoreHanoiQuests.Quest2Id));
    }

    [Theory]
    [InlineData("Quest_NonExistent")]
    [InlineData("Quest_Hanoi_999")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!@#$%^&*()")]
    public void Quest_NonExistentQuestId_SafelyFailsWithoutCrashing(string invalidQuestId)
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        Assert.False(qm.CanStartQuest(invalidQuestId));
        Assert.False(qm.StartQuest(invalidQuestId));
        Assert.False(qm.CompleteQuest(invalidQuestId));
        Assert.False(qm.IsQuestCompleted(invalidQuestId));
        Assert.False(qm.IsQuestInProgress(invalidQuestId));
        Assert.Null(qm.GetQuest(invalidQuestId));
        Assert.False(qm.RecordObjectiveProgress(invalidQuestId, "any_obj", 1));
    }

    [Fact]
    public void Quest_RestartingActiveOrCompletedQuest_IsRejected()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        // 1. Start Q1
        Assert.True(qm.StartQuest(CoreHanoiQuests.Quest1Id));
        Assert.True(qm.IsQuestInProgress(CoreHanoiQuests.Quest1Id));

        // Try starting Q1 again while InProgress
        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest1Id));
        Assert.False(qm.StartQuest(CoreHanoiQuests.Quest1Id));

        // 2. Complete Q1
        foreach (var item in CoreItemIds.AllPhoIngredients)
        {
            qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, item, 1);
        }
        qm.CompleteQuest(CoreHanoiQuests.Quest1Id);
        Assert.True(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));

        // Try starting Q1 again after Completed
        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest1Id));
        Assert.False(qm.StartQuest(CoreHanoiQuests.Quest1Id));
    }

    [Fact]
    public void Quest_CompletingNonExistentObjective_IsRejected()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        qm.StartQuest(CoreHanoiQuests.Quest1Id);

        // Targeted quest non-existent objective
        bool res1 = qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, "NonExistentIngredient", 1);
        Assert.False(res1);

        // Global overload non-existent objective
        bool res2 = qm.RecordObjectiveProgress("Objective_DoesNotExistAnywhere", 1);
        Assert.False(res2);

        // Verify quest state is completely untouched (0/5 items)
        var q1 = qm.GetQuest(CoreHanoiQuests.Quest1Id)!;
        Assert.Equal(CoreQuestState.InProgress, q1.State);
        Assert.All(q1.Objectives, obj => Assert.Equal(0, obj.CurrentCount));
    }

    [Fact]
    public void Quest_RecordProgressOnNotStartedOrCompletedQuest_IsRejected()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        // In NotStarted state
        bool res1 = qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, CoreItemIds.BanhPho, 1);
        Assert.False(res1);

        // Advance to Completed
        qm.StartQuest(CoreHanoiQuests.Quest1Id);
        foreach (var item in CoreItemIds.AllPhoIngredients)
        {
            qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, item, 1);
        }
        qm.CompleteQuest(CoreHanoiQuests.Quest1Id);

        // In Completed state
        bool res2 = qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, CoreItemIds.BanhPho, 1);
        Assert.False(res2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-9999)]
    [InlineData(int.MinValue)]
    public void Quest_NegativeCountsAndZeroAmounts_AreRejected(int invalidAmount)
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        qm.StartQuest(CoreHanoiQuests.Quest1Id);

        bool changed = qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, CoreItemIds.BanhPho, invalidAmount);
        Assert.False(changed);

        var q1 = qm.GetQuest(CoreHanoiQuests.Quest1Id)!;
        var obj = q1.GetObjective(CoreItemIds.BanhPho)!;
        Assert.Equal(0, obj.CurrentCount);
        Assert.False(obj.IsCompleted);
    }

    [Fact]
    public void Quest_DuplicateItemTurnIns_IdempotencyAndNoEventSpam()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;
        qm.StartQuest(CoreHanoiQuests.Quest1Id);

        int progressEventCount = 0;
        using var token = driver.EventBus.Subscribe<CoreQuestProgressUpdatedEvent>(e =>
        {
            if (e.ObjectiveId == CoreItemIds.BanhPho) progressEventCount++;
        });

        // Collect Item_BanhPho 50 times in a row
        bool firstTurnIn = qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, CoreItemIds.BanhPho, 1);
        Assert.True(firstTurnIn);

        for (int i = 0; i < 49; i++)
        {
            bool duplicateTurnIn = qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, CoreItemIds.BanhPho, 1);
            Assert.False(duplicateTurnIn);
        }

        // Must have fired event EXACTLY once
        Assert.Equal(1, progressEventCount);

        var q1 = qm.GetQuest(CoreHanoiQuests.Quest1Id)!;
        var obj = q1.GetObjective(CoreItemIds.BanhPho)!;
        Assert.Equal(1, obj.CurrentCount);
    }

    [Fact]
    public void Quest_FuzzyPermutationItemTurnIns_CorrectlyReachesRequirementsMet()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;
        qm.StartQuest(CoreHanoiQuests.Quest1Id);

        int requirementsMetEvents = 0;
        using var token = driver.EventBus.Subscribe<CoreQuestRequirementsMetEvent>(e => requirementsMetEvents++);

        // Feed a noisy, duplicated sequence of 100 ingredient events
        var random = new Random(42);
        var allItems = CoreItemIds.AllPhoIngredients.ToList();
        var sequence = new List<string>();

        // Ensure each item appears multiple times interspersed
        for (int i = 0; i < 100; i++)
        {
            sequence.Add(allItems[random.Next(allItems.Count)]);
        }

        foreach (var item in sequence)
        {
            qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, item, 1);
        }

        // RequirementsMet must have fired exactly once
        Assert.Equal(1, requirementsMetEvents);

        var q1 = qm.GetQuest(CoreHanoiQuests.Quest1Id)!;
        Assert.Equal(CoreQuestState.RequirementsMet, q1.State);
        Assert.All(q1.Objectives, obj => Assert.Equal(1, obj.CurrentCount));
    }

    [Fact]
    public void Quest_CompleteQuest_IsIdempotentAndDoesNotResendEvents()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        qm.StartQuest(CoreHanoiQuests.Quest1Id);
        foreach (var item in CoreItemIds.AllPhoIngredients)
        {
            qm.RecordObjectiveProgress(CoreHanoiQuests.Quest1Id, item, 1);
        }

        int completedEventCount = 0;
        using var token = driver.EventBus.Subscribe<CoreQuestCompletedEvent>(e => completedEventCount++);

        // Call CompleteQuest 10 times consecutively
        for (int i = 0; i < 10; i++)
        {
            bool result = qm.CompleteQuest(CoreHanoiQuests.Quest1Id);
            Assert.True(result);
        }

        // Event must be dispatched exactly once on initial completion
        Assert.Equal(1, completedEventCount);
        Assert.True(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));
    }

    [Fact]
    public void Quest_HydrateFromCorruptedFlags_RecoversSafely()
    {
        var driver = new CoreSimulationDriver();
        var qm = driver.QuestManager;

        // Adversarial flags: Q1 completed, but individual ingredient flags false
        var corruptedFlags = new CoreQuestFlags
        {
            Q1_Started = true,
            Q1_Completed = true,
            Q1_BanhPho_Collected = false,
            Q1_ThitBo_Collected = false,
            Q1_NuocDung_Collected = false,
            Q1_HanhLa_Collected = false,
            Q1_ThaoMoc_Collected = false,
            Q2_Started = true,
            Q2_Completed = false
        };

        qm.HydrateFromFlags(corruptedFlags);

        // Q1 must be completed because Q1_Completed = true takes precedence
        Assert.True(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));
        Assert.True(qm.IsQuestInProgress(CoreHanoiQuests.Quest2Id));

        // Export back to flags must preserve completion status
        var exported = new CoreQuestFlags();
        qm.ExportToFlags(exported);
        Assert.True(exported.Q1_Completed);
        Assert.True(exported.Q2_Started);
    }

    // =========================================================================
    // SECTION 2: STATE STACK OVERFLOWS & MODAL COMBINATIONS
    // =========================================================================

    [Fact]
    public void StateStack_RapidPushPop_Stress_1000Iterations()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        for (int i = 0; i < 1000; i++)
        {
            bool pushed = sm.PushState(CoreGameState.Dialogue);
            Assert.True(pushed);
            Assert.Equal(CoreGameState.Dialogue, sm.CurrentState);
            Assert.Equal(2, sm.StackDepth);

            bool popped = sm.PopState(out var restored);
            Assert.True(popped);
            Assert.Equal(CoreGameState.Exploration, restored);
            Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
            Assert.Equal(1, sm.StackDepth);
        }

        Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
        Assert.Equal(1, sm.StackDepth);
    }

    [Fact]
    public void StateStack_RapidTogglePause_2000Iterations()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        for (int i = 0; i < 2000; i++)
        {
            bool toggled = sm.TogglePause();
            Assert.True(toggled);

            if (i % 2 == 0)
            {
                // Odd toggle: Pause is active
                Assert.Equal(CoreGameState.Pause, sm.CurrentState);
                Assert.Equal(2, sm.StackDepth);
            }
            else
            {
                // Even toggle: Restored to Exploration
                Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
                Assert.Equal(1, sm.StackDepth);
            }
        }

        Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
        Assert.Equal(1, sm.StackDepth);
    }

    [Fact]
    public void StateStack_RapidToggleAlbum_2000Iterations()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        for (int i = 0; i < 2000; i++)
        {
            bool toggled = sm.ToggleAlbum();
            Assert.True(toggled);

            if (i % 2 == 0)
            {
                Assert.Equal(CoreGameState.Album, sm.CurrentState);
                Assert.Equal(2, sm.StackDepth);
            }
            else
            {
                Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
                Assert.Equal(1, sm.StackDepth);
            }
        }

        Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
        Assert.Equal(1, sm.StackDepth);
    }

    [Fact]
    public void StateStack_StackUnderflow_PopAtBase_IsSafelyHandled()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        Assert.Equal(1, sm.StackDepth);

        // Attempt to pop base state 500 times consecutively
        for (int i = 0; i < 500; i++)
        {
            bool popped = sm.PopState(out var restored);
            Assert.False(popped);
            Assert.Equal(CoreGameState.Exploration, restored);
            Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
            Assert.Equal(1, sm.StackDepth);
        }

        Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
        Assert.Equal(1, sm.StackDepth);
    }

    [Theory]
    [InlineData(CoreGameState.MiniGamePho)]
    [InlineData(CoreGameState.MiniGameStalls)]
    [InlineData(CoreGameState.Dialogue)]
    [InlineData(CoreGameState.Cutscene)]
    [InlineData(CoreGameState.Pause)]
    public void StateStack_IllegalModalCombinations_AlbumBlockedDuringActiveModals(CoreGameState activeModal)
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        // Put manager into active modal/minigame
        sm.PushState(activeModal);
        Assert.Equal(activeModal, sm.CurrentState);

        // Modal rule: Cannot open Album during active minigames, dialogues, cutscenes, or pause
        Assert.False(sm.CanOpenAlbum);
        bool pushAlbum = sm.PushState(CoreGameState.Album);
        Assert.False(pushAlbum);
        bool toggleAlbum = sm.ToggleAlbum();
        Assert.False(toggleAlbum);

        // Top state must remain unchanged
        Assert.Equal(activeModal, sm.CurrentState);
    }

    [Fact]
    public void StateStack_AlbumSelfNestingBlocked_AndToggleClosesAlbum()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        // Open album
        bool opened = sm.PushState(CoreGameState.Album);
        Assert.True(opened);
        Assert.Equal(CoreGameState.Album, sm.CurrentState);

        // Cannot push album on itself
        Assert.False(sm.CanOpenAlbum);
        bool pushDuplicate = sm.PushState(CoreGameState.Album);
        Assert.False(pushDuplicate);

        // ToggleAlbum closes the album
        bool closed = sm.ToggleAlbum();
        Assert.True(closed);
        Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
        Assert.Equal(1, sm.StackDepth);
    }

    [Fact]
    public void StateStack_IllegalModalCombinations_PauseBlockedInTitle()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Title);

        Assert.False(sm.CanPause);
        bool pushPause = sm.PushState(CoreGameState.Pause);
        Assert.False(pushPause);
        bool togglePause = sm.TogglePause();
        Assert.False(togglePause);

        Assert.Equal(CoreGameState.Title, sm.CurrentState);
        Assert.Equal(1, sm.StackDepth);
    }

    [Theory]
    [InlineData(CoreGameState.Exploration)]
    [InlineData(CoreGameState.Dialogue)]
    [InlineData(CoreGameState.WorldMap)]
    [InlineData(CoreGameState.Pause)]
    public void StateStack_PushIdenticalStateOnItself_IsRejected(CoreGameState state)
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, state);

        int depthBefore = sm.StackDepth;
        bool pushed = sm.PushState(state);
        Assert.False(pushed);
        Assert.Equal(depthBefore, sm.StackDepth);
        Assert.Equal(state, sm.CurrentState);
    }

    [Fact]
    public void StateStack_DeepStackNesting_500Layers_LIFORestoration()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Exploration);

        // Alternately push Dialogue and Cutscene 500 layers deep
        var expectedOrder = new List<CoreGameState> { CoreGameState.Exploration };
        for (int i = 0; i < 500; i++)
        {
            var next = (i % 2 == 0) ? CoreGameState.Dialogue : CoreGameState.Cutscene;
            bool pushed = sm.PushState(next);
            Assert.True(pushed);
            expectedOrder.Add(next);
        }

        Assert.Equal(501, sm.StackDepth);
        Assert.True(sm.IsOverlay);
        Assert.Equal(CoreGameState.Exploration, sm.BaseState);

        // Now pop all 500 layers in reverse order
        for (int i = 500; i >= 1; i--)
        {
            bool popped = sm.PopState(out var restored);
            Assert.True(popped);
            Assert.Equal(expectedOrder[i - 1], restored);
            Assert.Equal(i, sm.StackDepth);
        }

        Assert.Equal(1, sm.StackDepth);
        Assert.False(sm.IsOverlay);
        Assert.Equal(CoreGameState.Exploration, sm.CurrentState);
    }

    [Fact]
    public void StateStack_HistoryAudit_TracksAllTransitions()
    {
        var bus = new CoreEventBus();
        var sm = new CoreGameStateManager(bus, CoreGameState.Title);

        sm.ChangeState(CoreGameState.WorldMap);
        sm.ChangeState(CoreGameState.Exploration);
        sm.PushState(CoreGameState.Dialogue);
        sm.PopState();

        var history = sm.GetHistory();
        Assert.Equal(4, history.Count);
        Assert.Equal(CoreGameState.Title, history[0].State);
        Assert.Equal(CoreGameState.WorldMap, history[1].State);
        Assert.Equal(CoreGameState.Exploration, history[2].State);
        Assert.Equal(CoreGameState.Dialogue, history[3].State);
    }

    // =========================================================================
    // SECTION 3: SIMULATION DRIVER FIDELITY & STRESS HARNESS
    // =========================================================================

    [Fact]
    public void SimulationDriver_1000Ticks_StrictDeterminism()
    {
        // Two independent simulation drivers
        var driverA = new CoreSimulationDriver();
        var driverB = new CoreSimulationDriver();

        // Baseline timestamp synchronization so test isolates game simulation determinism
        var baselineTime = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
        driverA.SaveData.LastSavedTimestamp = baselineTime;
        driverB.SaveData.LastSavedTimestamp = baselineTime;

        const int ticksToSimulate = 1200; // 20 seconds at 60 FPS
        const float dt = 0.016667f;

        // Execute identical scripted gameplay on both drivers
        void RunScript(CoreSimulationDriver driver)
        {
            driver.StateManager.ChangeState(CoreGameState.Exploration);

            driver.QuestManager.StartQuest(CoreHanoiQuests.Quest1Id);
            foreach (var item in CoreItemIds.AllPhoIngredients)
            {
                driver.SimulateCollectItem(item);
                driver.AdvanceTicks(10, dt);
            }
            driver.QuestManager.CompleteQuest(CoreHanoiQuests.Quest1Id);
            driver.AdvanceTicks(20, dt);

            driver.QuestManager.StartQuest(CoreHanoiQuests.Quest2Id);
            driver.SimulateSolvePhoCooking();
            driver.AdvanceTicks(20, dt);

            driver.QuestManager.StartQuest(CoreHanoiQuests.Quest3Id);
            driver.SimulateSolveStallGrid();

            // Run remainder of ticks to reach exact 1200 ticks
            int remaining = ticksToSimulate - driver.TotalTicksSimulated;
            driver.AdvanceTicks(remaining, dt);
        }

        RunScript(driverA);
        RunScript(driverB);

        // Assert tick count equality
        Assert.Equal(ticksToSimulate, driverA.TotalTicksSimulated);
        Assert.Equal(ticksToSimulate, driverB.TotalTicksSimulated);

        // Assert simulation time equality
        Assert.Equal(driverA.CurrentSimulationTime, driverB.CurrentSimulationTime);

        // Assert event counts and sequence match bit-for-bit
        Assert.Equal(driverA.FiredEvents.Count, driverB.FiredEvents.Count);
        for (int i = 0; i < driverA.FiredEvents.Count; i++)
        {
            Assert.Equal(driverA.FiredEvents[i].GetType(), driverB.FiredEvents[i].GetType());
        }

        // Assert SaveData JSON matches bit-for-bit
        var jsonOptions = CoreSaveGameJsonContext.CreateOptions();
        string jsonA = JsonSerializer.Serialize(driverA.SaveData, jsonOptions);
        string jsonB = JsonSerializer.Serialize(driverB.SaveData, jsonOptions);
        Assert.Equal(jsonA, jsonB);
    }

    [Fact]
    public void SimulationDriver_IdleMemoryLeakAndEventPurity_5000Ticks()
    {
        var driver = new CoreSimulationDriver();

        // 100 warm-up ticks
        driver.AdvanceTicks(100);

        int eventsBefore = driver.FiredEvents.Count;
        long threadAllocBefore = GC.GetAllocatedBytesForCurrentThread();

        // Simulate 5,000 idle ticks (~83 seconds of game running)
        driver.AdvanceTicks(5000);

        long threadAllocAfter = GC.GetAllocatedBytesForCurrentThread();
        long allocatedOnThread = threadAllocAfter - threadAllocBefore;

        // Assert ZERO events were published during idle ticks
        Assert.Equal(eventsBefore, driver.FiredEvents.Count);

        // Assert State Stack remained untouched
        Assert.Equal(1, driver.StateManager.StackDepth);
        Assert.Equal(CoreGameState.Title, driver.StateManager.CurrentState);

        // Assert input and audio buffers are clean
        Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);
        Assert.Empty(driver.AudioService.PlayedSfx);

        // Assert per-thread allocations during 5,000 ticks are strictly zero or negligible (< 100 KB)
        Assert.True(allocatedOnThread < 100 * 1024, $"Potential memory leak: thread allocated {allocatedOnThread} bytes during 5,000 idle ticks.");
    }

    [Fact]
    public void SimulationDriver_TimingPrecisionAndDrift_10000Ticks()
    {
        var driver = new CoreSimulationDriver();
        const float fixedDt = 1.0f / 60.0f; // 0.0166666675f
        const int totalTicks = 10000;

        driver.AdvanceTicks(totalTicks, fixedDt);

        Assert.Equal(totalTicks, driver.TotalTicksSimulated);

        float expectedTime = totalTicks * fixedDt;
        float actualTime = driver.CurrentSimulationTime;
        float drift = Math.Abs(actualTime - expectedTime);

        // Over 10,000 frames (166.67 seconds of game time), drift must be < 0.01 seconds
        Assert.True(drift < 0.01f, $"Simulation drift exceeded threshold: actual={actualTime}, expected={expectedTime}, drift={drift}");
    }

    [Fact]
    public void SimulationDriver_AdvanceTime_CalculatesExactTicks()
    {
        var driver = new CoreSimulationDriver();
        const float duration = 5.0f;
        const float dt = 0.016667f;
        int expectedTicks = (int)Math.Ceiling(duration / dt); // 300 ticks

        driver.AdvanceTime(duration, dt);

        Assert.Equal(expectedTicks, driver.TotalTicksSimulated);
        Assert.True(driver.CurrentSimulationTime >= duration);
    }

    [Fact]
    public void SimulationDriver_AdvanceUntil_EarlyExitAndTimeoutHandling()
    {
        var driver = new CoreSimulationDriver();

        // 1. Condition met early
        int targetTick = 42;
        bool resultEarly = driver.AdvanceUntil(() => driver.TotalTicksSimulated >= targetTick, maxWaitSeconds: 5.0f);
        Assert.True(resultEarly);
        Assert.Equal(targetTick, driver.TotalTicksSimulated);

        // 2. Condition never met: must timeout safely without hanging
        bool resultTimeout = driver.AdvanceUntil(() => false, maxWaitSeconds: 0.1f);
        Assert.False(resultTimeout);
    }

    [Fact]
    public void SimulationDriver_OppositeKeyCancellation_MaintainsZeroVector()
    {
        var driver = new CoreSimulationDriver();

        // Both horizontal opposing keys
        driver.InputService.SimulateOpposingMove(up: false, down: false, left: true, right: true);
        Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);

        // Both vertical opposing keys
        driver.InputService.SimulateOpposingMove(up: true, down: true, left: false, right: false);
        Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);

        // All 4 keys simultaneously
        driver.InputService.SimulateOpposingMove(up: true, down: true, left: true, right: true);
        Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);

        // Simulate 60 ticks with opposing inputs held
        for (int i = 0; i < 60; i++)
        {
            driver.Step();
            Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);
        }
    }
}
