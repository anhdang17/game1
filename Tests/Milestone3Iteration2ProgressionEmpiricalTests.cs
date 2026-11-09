using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Stages;
using GameProject.UI;
using Xunit;

// Resolve ambiguous type aliases
using MockInputService = GameProject.Core.Ports.MockInputService;
using NullAudioService = GameProject.Core.Ports.NullAudioService;
using MockSaveStorage = GameProject.Core.Ports.MockSaveStorage;

namespace GameProject.Tests;

/// <summary>
/// Empirical Challenger 2 verification suite for Milestone 3 Iteration 2:
/// Rigorously validates StallPuzzleScene modal lifecycle, event publication,
/// flag synchronization (GrayShadow_LetterRead), and Long Biên Gate transition to World Map.
/// </summary>
public sealed class Milestone3Iteration2ProgressionEmpiricalTests
{
    // =========================================================================
    // 1. STALL PUZZLE SOLVE -> LETTER MODAL -> GRAYSHADOW_LETTERREAD SYNCHRONIZATION
    // =========================================================================

    [Fact]
    public void StallPuzzleScene_WhenSolvedAndDismissedWithConfirm_SetsGrayShadowLetterReadTrue()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(bus);
        HanoiQuests.RegisterAll(questManager);
        var input = new MockInputService();

        var scene = new StallPuzzleScene(saveData, questManager, bus, inputService: input);
        scene.Enter();

        // 1. Solve the puzzle
        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved, "Stall puzzle must be solved after two canonical pushes.");

        // Before modal presentation, flag is false
        Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "GrayShadow_LetterRead must remain false before modal is read/dismissed.");

        // 2. Advance time past 3.0s to trigger modal
        scene.Update(3.2f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal", BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive, "Letter modal must become active at _solveTimer >= 3.0s.");

        // 3. Fast-forward typewriter to completion
        for (int i = 0; i < 30; i++)
        {
            scene.Update(0.5f);
        }
        Assert.True(letterModal.IsTypingComplete, "Typewriter must complete after simulated duration.");

        // 4. Press Confirm to dismiss
        input.IsConfirmJustPressed = true;
        scene.Update(0.1f);
        input.IsConfirmJustPressed = false;

        // 5. EMPIRICAL VERIFICATION: Modal dismissed, GrayShadow_LetterRead == true, RequestNextScene == true
        Assert.False(letterModal.IsActive, "Letter modal must be inactive after confirm dismissal.");
        Assert.True(letterModal.RequestClose, "Letter modal RequestClose must be true.");
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "CRITICAL: GrayShadow_LetterRead must be synchronized to true upon modal dismissal.");
        Assert.True(scene.RequestNextScene, "StallPuzzleScene.RequestNextScene must be set to true.");
    }

    [Fact]
    public void StallPuzzleScene_WhenSolvedAndDismissedViaSkipAndConfirm_SetsGrayShadowLetterReadTrue()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(bus);
        HanoiQuests.RegisterAll(questManager);
        var input = new MockInputService();

        var scene = new StallPuzzleScene(saveData, questManager, bus, inputService: input);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        scene.Update(3.2f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal", BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Instant skip pulse during typing
        input.IsCancelJustPressed = true;
        scene.Update(0.016f);
        input.IsCancelJustPressed = false;

        Assert.True(letterModal.IsTypingComplete, "Skip input must instantly complete typewriter reveal.");

        // Next pulse confirm to dismiss
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "GrayShadow_LetterRead must be true when skipped and dismissed.");
        Assert.True(scene.RequestNextScene, "RequestNextScene must be true.");
    }

    [Fact]
    public void StallPuzzleScene_ProgrammaticDismissalFallback_SynchronizesFlagAndRequestsNextScene()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(bus);
        HanoiQuests.RegisterAll(questManager);

        var scene = new StallPuzzleScene(saveData, questManager, bus);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        scene.Update(3.2f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal", BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Simulate external/programmatic dismissal
        letterModal.Dismiss();
        Assert.False(letterModal.IsActive);
        Assert.True(letterModal.RequestClose);

        // Next scene update triggers the fallback branch
        scene.Update(0.016f);

        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "Fallback branch must catch programmatic modal dismissal and synchronize GrayShadow_LetterRead.");
        Assert.True(scene.RequestNextScene, "Fallback branch must set RequestNextScene = true.");
    }

    // =========================================================================
    // 2. EVENT PUBLICATION EMPIRICAL VERIFICATION
    // =========================================================================

    [Fact]
    public void StallPuzzleScene_DismissalPublishesCutsceneEndedAndLetterOpenedEvents()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(bus);
        HanoiQuests.RegisterAll(questManager);
        var input = new MockInputService();

        var cutsceneEndedEvents = new List<CutsceneEndedEvent>();
        var letterOpenedEvents = new List<LetterOpenedEvent>();

        bus.Subscribe<CutsceneEndedEvent>(e => cutsceneEndedEvents.Add(e));
        bus.Subscribe<LetterOpenedEvent>(e => letterOpenedEvents.Add(e));

        var scene = new StallPuzzleScene(saveData, questManager, bus, inputService: input);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        scene.Update(3.2f);

        // Skip to end of typing
        input.IsCancelJustPressed = true;
        scene.Update(0.016f);
        input.IsCancelJustPressed = false;

        // Dismiss modal
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // EMPIRICAL VERIFICATION OF PUBLISHED EVENTS
        Assert.Single(cutsceneEndedEvents);
        Assert.Equal("GrayShadow_Letter", cutsceneEndedEvents[0].CutsceneId);

        Assert.Single(letterOpenedEvents);
        Assert.Equal("Letter_GrayShadow_Hanoi", letterOpenedEvents[0].LetterId);
        Assert.Equal("Bóng Xám", letterOpenedEvents[0].Sender);

        // Verify that subsequent frames do NOT repeatedly fire events
        for (int i = 0; i < 20; i++)
        {
            scene.Update(0.1f);
        }

        Assert.Single(cutsceneEndedEvents);
        Assert.Single(letterOpenedEvents);
    }

    // =========================================================================
    // 3. LONG BIÊN GATE INTERACTION & REQUESTWORLDMAP VERIFICATION
    // =========================================================================

    [Fact]
    public void LongBienGate_AfterPuzzleSolveAndDismissal_ImmediatelySetsRequestWorldMapWithoutCutsceneTrap()
    {
        var bus = new EventBus();
        var audio = new NullAudioService();
        var input = new MockInputService();
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Completed = true;
        saveData.Level1State.QuestFlags.Q2_Completed = true;

        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: audio,
            eventBus: bus
        );

        // 1. Move to Long Bien Gate & open puzzle
        scene.Kinematics.Position = scene.LongBienGate.Position;
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.Equal(GameState.MiniGameStalls, sm.CurrentState);

        // 2. Solve StallPuzzleScene
        var stallModalField = typeof(HanoiExplorationScene).GetField("_stallPuzzleModal", BindingFlags.NonPublic | BindingFlags.Instance);
        var stallModal = (StallPuzzleScene)stallModalField!.GetValue(scene)!;
        Assert.NotNull(stallModal);

        stallModal.Push("Obj_FruitStall", Direction.Left);
        stallModal.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(stallModal.IsSolved);

        // 3. Advance to modal and dismiss via input forwarding
        for (int i = 0; i < 35; i++)
        {
            scene.Update(0.1f);
        }

        // Fast-forward text
        input.IsCancelJustPressed = true;
        scene.Update(0.016f);
        input.IsCancelJustPressed = false;

        // Dismiss modal
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Scene pops modal back to Exploration
        Assert.Equal(GameState.Exploration, sm.CurrentState);
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "GrayShadow_LetterRead must be true upon exiting StallPuzzleScene.");

        // 4. Player interacts with Long Bien Gate
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        // Confirm dialogue
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // EMPIRICAL VERIFICATION:
        // RequestWorldMap is TRUE, State is Exploration (NEVER pushed into Cutscene!)
        Assert.True(scene.RequestWorldMap, "Long Bien gate interaction MUST set RequestWorldMap = true.");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }

    [Fact]
    public void LongBienGate_InteractionsBeforeQuestProgression_SafelyBlocked()
    {
        var bus = new EventBus();
        var audio = new NullAudioService();
        var input = new MockInputService();
        var saveData = SaveData.CreateDefault();

        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: audio,
            eventBus: bus
        );

        scene.Kinematics.Position = scene.LongBienGate.Position;

        // Case 1: Q1 not completed -> Gate interaction should show hint, not trigger puzzle or world map
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.False(scene.RequestWorldMap, "RequestWorldMap must not trigger when Q1 is incomplete.");
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // Case 2: Q1 completed, Q2 not completed
        saveData.Level1State.QuestFlags.Q1_Completed = true;

        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.False(scene.RequestWorldMap, "RequestWorldMap must not trigger when Q2 is incomplete.");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }

    [Fact]
    public void LongBienGate_FallbackCutsceneFlow_WhenLetterReadWasInitiallyFalse_RecoversAndEnablesTransition()
    {
        var bus = new EventBus();
        var audio = new NullAudioService();
        var input = new MockInputService();
        var saveData = SaveData.CreateDefault();
        // Simulate abnormal state where Gate is unlocked but Letter was not marked read
        saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        saveData.Level1State.QuestFlags.GrayShadow_LetterRead = false;

        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: audio,
            eventBus: bus
        );

        scene.Kinematics.Position = scene.LongBienGate.Position;

        // 1. First interaction: enters cutscene because Letter was not read
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.Equal(GameState.Cutscene, sm.CurrentState);

        // Fast-forward cutscene typewriter text
        input.IsCancelJustPressed = true;
        scene.Update(0.016f);
        input.IsCancelJustPressed = false;

        // Dismiss cutscene modal
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Cutscene ends: state popped, flag set
        Assert.Equal(GameState.Exploration, sm.CurrentState);
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);

        // 2. Second interaction: now immediately sets RequestWorldMap = true
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.True(scene.RequestWorldMap, "Second interaction must set RequestWorldMap = true without duplicate cutscene.");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }

    // =========================================================================
    // 4. CROSS-SESSION PERSISTENCE INTEGRITY
    // =========================================================================

    [Fact]
    public void Persistence_SolvedStateAndLetterRead_SurvivesSerializationAndTransfersToWorldMap()
    {
        var storage = new MockSaveStorage();
        var bus = new EventBus();
        string savePath = "Data/test_save_persistence.json";
        var saveLoad = new SaveLoadService((string?)savePath, storage, bus);

        // Setup save data where Q3 is completed and letter read
        var originalSave = SaveData.CreateDefault();
        originalSave.Level1State.QuestFlags.Q1_Completed = true;
        originalSave.Level1State.QuestFlags.Q2_Completed = true;
        originalSave.Level1State.QuestFlags.Q3_Completed = true;
        originalSave.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        originalSave.Level1State.QuestFlags.GrayShadow_LetterRead = true;
        originalSave.Level1State.QuestFlags.Level1_Finished = true;
        originalSave.WorldProgression.SetColorRestoration(WorldProgression.RegionHanoi, 100.0f);
        originalSave.WorldProgression.UnlockRegion(WorldProgression.RegionHue);

        saveLoad.Save(originalSave);

        // Load into fresh instance
        var rehydratedSave = saveLoad.Load();

        Assert.True(rehydratedSave.Level1State.QuestFlags.GrayShadow_LetterRead,
            "GrayShadow_LetterRead must persist across serialization/deserialization.");
        Assert.True(rehydratedSave.Level1State.QuestFlags.Gate_LongBien_Unlocked);

        // Fresh scene using rehydrated data
        var input = new MockInputService();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: rehydratedSave,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudioService(),
            eventBus: bus
        );

        scene.Kinematics.Position = scene.LongBienGate.Position;

        // Interact with gate
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        Assert.True(scene.RequestWorldMap, "Rehydrated game state must transition to World Map directly without cutscene modal.");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }

    // =========================================================================
    // 5. PHO COOKING ENGINE INVARIANT RESET EMPIRICAL VERIFICATION
    // =========================================================================

    [Fact]
    public void PhoCookingEngine_PostSolveMutations_StrictlyResetIsSolved()
    {
        var engine = new PhoCookingEngine();

        // Solve canonically
        engine.PlaceIngredient(0, "Item_BanhPho");
        engine.PlaceIngredient(1, "Item_ThitBo");
        engine.PlaceIngredient(2, "Item_HanhLa");
        engine.PlaceIngredient(3, "Item_NuocDung");
        engine.PlaceIngredient(4, "Item_GiaVi");

        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved, "Engine must be solved.");

        // Mutation 1: Remove an ingredient
        engine.RemoveIngredient(2);
        bool validAfterRemoval = engine.ValidateAssembly(out string err1);
        Assert.False(validAfterRemoval);
        Assert.NotEmpty(err1);
        Assert.False(engine.IsSolved, "INVARIANT CHECK: IsSolved must be reset to false when slot is empty.");

        // Mutation 2: Put wrong ingredient in slot
        engine.PlaceIngredient(2, "Item_ThitBo"); // Duplicate beef instead of green onion
        bool validAfterDuplicate = engine.ValidateAssembly(out string err2);
        Assert.False(validAfterDuplicate);
        Assert.NotEmpty(err2);
        Assert.False(engine.IsSolved, "INVARIANT CHECK: IsSolved must remain false on invalid recipe.");

        // Mutation 3: Clear all
        engine.ResetPuzzle();
        Assert.False(engine.IsSolved);
        Assert.False(engine.ValidateAssembly(out _));
        Assert.False(engine.IsSolved);
    }
}
