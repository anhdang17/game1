using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Stages;
using GameProject.UI;
using Xunit;
using MockInput = GameProject.Core.Ports.MockInputService;
using NullAudio = GameProject.Core.Ports.NullAudioService;

namespace GameProject.Tests;

/// <summary>
/// Milestone 3 Iteration 2 Empirical Challenger 1 Stress Tests:
/// 1. PhoCookingEngine.ValidateAssembly Invariant Reset on Invalid Slot Combinations
/// 2. StallPuzzleScene Headless Modal Dismissal via IInputService
/// 3. Edge Cases: Exhaustive permutations, rapid churn, fallback dismissal, and E2E gate flow.
/// </summary>
public sealed class ChallengerM3Iteration2EmpiricalTests
{
    // =========================================================================
    // SECTION 1: PHO COOKING ENGINE INVARIANT RESET VERIFICATION
    // =========================================================================

    [Fact]
    public void PhoAssembly_PostCanonicalSolve_EveryIndividualSlotRemoval_ResetsIsSolvedToFalse()
    {
        var engine = new PhoCookingEngine();

        for (int slotToRemove = 0; slotToRemove < PhoCookingEngine.TotalSlots; slotToRemove++)
        {
            // 1. Solve canonically
            engine.PlaceIngredient(0, ItemIds.BanhPho);
            engine.PlaceIngredient(1, ItemIds.ThitBo);
            engine.PlaceIngredient(2, ItemIds.HanhLa);
            engine.PlaceIngredient(3, ItemIds.NuocDung);
            engine.PlaceIngredient(4, ItemIds.ThaoMoc);

            bool solved = engine.ValidateAssembly(out string initialError);
            Assert.True(solved, $"Precondition: Canonical assembly must validate (Iteration slot {slotToRemove})");
            Assert.True(engine.IsSolved, "Precondition: IsSolved must be true after valid assembly");
            Assert.Empty(initialError);

            // 2. Remove one slot
            bool removed = engine.RemoveIngredient(slotToRemove);
            Assert.True(removed, $"Removing slot {slotToRemove} should succeed");

            // 3. Re-validate assembly
            bool revalidated = engine.ValidateAssembly(out string postError);
            Assert.False(revalidated, $"Assembly must fail when slot {slotToRemove} is missing");
            Assert.NotEmpty(postError);

            // INVARIANT: IsSolved MUST BE FALSE
            Assert.False(engine.IsSolved,
                $"EMPIRICAL VERIFICATION: IsSolved must be reset to false when slot {slotToRemove} is removed post-solve");
        }
    }

    [Fact]
    public void PhoAssembly_PostAlternativeSolve_EveryIndividualSlotRemoval_ResetsIsSolvedToFalse()
    {
        var engine = new PhoCookingEngine();

        for (int slotToRemove = 0; slotToRemove < PhoCookingEngine.TotalSlots; slotToRemove++)
        {
            // 1. Solve with alternative garnish (GiaVi)
            engine.PlaceIngredient(0, ItemIds.BanhPho);
            engine.PlaceIngredient(1, ItemIds.ThitBo);
            engine.PlaceIngredient(2, ItemIds.HanhLa);
            engine.PlaceIngredient(3, ItemIds.NuocDung);
            engine.PlaceIngredient(4, ItemIds.GiaVi);

            bool solved = engine.ValidateAssembly(out string initialError);
            Assert.True(solved, $"Precondition: Alternative assembly must validate (Iteration slot {slotToRemove})");
            Assert.True(engine.IsSolved, "Precondition: IsSolved must be true after valid assembly");
            Assert.Empty(initialError);

            // 2. Remove one slot
            bool removed = engine.RemoveIngredient(slotToRemove);
            Assert.True(removed, $"Removing slot {slotToRemove} should succeed");

            // 3. Re-validate assembly
            bool revalidated = engine.ValidateAssembly(out string postError);
            Assert.False(revalidated, $"Assembly must fail when slot {slotToRemove} is missing");
            Assert.NotEmpty(postError);

            // INVARIANT: IsSolved MUST BE FALSE
            Assert.False(engine.IsSolved,
                $"EMPIRICAL VERIFICATION: IsSolved must be reset to false when slot {slotToRemove} is removed post-solve (alternative garnish)");
        }
    }

    [Fact]
    public void PhoAssembly_PostSolve_AllSevenFailureBranches_ResetIsSolvedToFalse_WithGuidance()
    {
        var engine = new PhoCookingEngine();

        void SetupCanonicalBowl()
        {
            engine.PlaceIngredient(0, ItemIds.BanhPho);
            engine.PlaceIngredient(1, ItemIds.ThitBo);
            engine.PlaceIngredient(2, ItemIds.HanhLa);
            engine.PlaceIngredient(3, ItemIds.NuocDung);
            engine.PlaceIngredient(4, ItemIds.ThaoMoc);
            Assert.True(engine.ValidateAssembly(out _));
            Assert.True(engine.IsSolved);
        }

        // Branch 1: Missing slot
        SetupCanonicalBowl();
        engine.RemoveIngredient(2);
        Assert.False(engine.ValidateAssembly(out string err1));
        Assert.False(engine.IsSolved);
        Assert.Contains("thiếu nguyên liệu", err1);

        // Branch 2: Duplicate ingredients (distinctCount != 5)
        SetupCanonicalBowl();
        engine.PlaceIngredient(4, ItemIds.BanhPho); // duplicate BanhPho
        Assert.False(engine.ValidateAssembly(out string err2));
        Assert.False(engine.IsSolved);
        Assert.Contains("riêng biệt", err2);

        // Branch 3a: Step 1 wrong (NuocDung at base)
        SetupCanonicalBowl();
        engine.PlaceIngredient(0, ItemIds.NuocDung);
        engine.PlaceIngredient(3, ItemIds.BanhPho);
        Assert.False(engine.ValidateAssembly(out string err3a));
        Assert.False(engine.IsSolved);
        Assert.Contains("Nước dùng mà chan trước", err3a);

        // Branch 3b: Step 1 wrong (Other at base)
        SetupCanonicalBowl();
        engine.PlaceIngredient(0, ItemIds.HanhLa);
        engine.PlaceIngredient(2, ItemIds.BanhPho);
        Assert.False(engine.ValidateAssembly(out string err3b));
        Assert.False(engine.IsSolved);
        Assert.Contains("bánh phở tươi đã chần nóng", err3b);

        // Branch 4a: Step 2 wrong (HanhLa over noodles before beef)
        SetupCanonicalBowl();
        engine.PlaceIngredient(1, ItemIds.HanhLa);
        engine.PlaceIngredient(2, ItemIds.ThitBo);
        Assert.False(engine.ValidateAssembly(out string err4a));
        Assert.False(engine.IsSolved);
        Assert.Contains("Thịt bò thái mỏng phải trải đều trên bánh phở trước", err4a);

        // Branch 4b: Step 2 wrong (Other over noodles)
        SetupCanonicalBowl();
        engine.PlaceIngredient(1, ItemIds.NuocDung);
        engine.PlaceIngredient(3, ItemIds.ThitBo);
        Assert.False(engine.ValidateAssembly(out string err4b));
        Assert.False(engine.IsSolved);
        Assert.Contains("Xếp các lát thịt bò tươi mềm", err4b);

        // Branch 5: Step 3 wrong (Broth before herbs)
        SetupCanonicalBowl();
        engine.PlaceIngredient(2, ItemIds.NuocDung);
        engine.PlaceIngredient(3, ItemIds.HanhLa);
        Assert.False(engine.ValidateAssembly(out string err5));
        Assert.False(engine.IsSolved);
        Assert.Contains("hành hoa chẻ và rau mùi", err5);

        // Branch 6: Step 4 wrong (Herbs in broth step)
        SetupCanonicalBowl();
        engine.PlaceIngredient(3, ItemIds.ThaoMoc);
        engine.PlaceIngredient(4, ItemIds.NuocDung);
        Assert.False(engine.ValidateAssembly(out string err6));
        Assert.False(engine.IsSolved);
        Assert.Contains("chan nước dùng hầm xương", err6);

        // Branch 7: Step 5 wrong (Invalid garnish)
        SetupCanonicalBowl();
        engine.PlaceIngredient(4, "Item_UnknownHerb");
        Assert.False(engine.ValidateAssembly(out string err7));
        Assert.False(engine.IsSolved);
        Assert.Contains("chanh ớt, hạt tiêu Bắc hoặc hương thảo mộc", err7);
    }

    [Fact]
    public void PhoAssembly_ExhaustivePermutations_IsSolvedAccuratelyMirrorsValidity()
    {
        var engine = new PhoCookingEngine();

        // 1. First solve to establish IsSolved = true
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);
        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved);

        // 2. Generate all permutations of the 5 canonical ingredients
        var ingredients = new[] { ItemIds.BanhPho, ItemIds.ThitBo, ItemIds.HanhLa, ItemIds.NuocDung, ItemIds.ThaoMoc };
        var allPermutations = GeneratePermutations(ingredients);
        Assert.Equal(120, allPermutations.Count);

        int validCount = 0;
        int invalidCount = 0;

        foreach (var perm in allPermutations)
        {
            for (int i = 0; i < 5; i++)
            {
                engine.PlaceIngredient(i, perm[i]);
            }

            bool valid = engine.ValidateAssembly(out _);
            if (valid)
            {
                validCount++;
                Assert.True(engine.IsSolved, "IsSolved must be true for valid sequence");
                Assert.Equal(ItemIds.BanhPho, perm[0]);
                Assert.Equal(ItemIds.ThitBo, perm[1]);
                Assert.Equal(ItemIds.HanhLa, perm[2]);
                Assert.Equal(ItemIds.NuocDung, perm[3]);
                Assert.Equal(ItemIds.ThaoMoc, perm[4]);
            }
            else
            {
                invalidCount++;
                Assert.False(engine.IsSolved, "IsSolved MUST BE FALSE for any invalid permutation");
            }
        }

        Assert.Equal(1, validCount);
        Assert.Equal(119, invalidCount);
    }

    [Fact]
    public void PhoAssembly_RapidAlternatingChurn_1000Cycles_InvariantNeverDrifts()
    {
        var engine = new PhoCookingEngine();

        for (int cycle = 0; cycle < 1000; cycle++)
        {
            // Solve
            engine.PlaceIngredient(0, ItemIds.BanhPho);
            engine.PlaceIngredient(1, ItemIds.ThitBo);
            engine.PlaceIngredient(2, ItemIds.HanhLa);
            engine.PlaceIngredient(3, ItemIds.NuocDung);
            engine.PlaceIngredient(4, (cycle % 2 == 0) ? ItemIds.ThaoMoc : ItemIds.GiaVi);

            Assert.True(engine.ValidateAssembly(out _), $"Cycle {cycle}: Valid assembly failed");
            Assert.True(engine.IsSolved, $"Cycle {cycle}: IsSolved was false when valid");

            // Mutate into guaranteed invalid state
            int slotToCorrupt = cycle % PhoCookingEngine.TotalSlots;
            if (cycle % 3 == 0)
            {
                engine.RemoveIngredient(slotToCorrupt);
            }
            else if (cycle % 3 == 1)
            {
                // Place an ingredient that is distinct from what should be at slotToCorrupt,
                // or duplicate an adjacent slot to guarantee invalidity.
                string duplicateIngredient = (slotToCorrupt == 0) ? ItemIds.ThitBo : ItemIds.BanhPho;
                engine.PlaceIngredient(slotToCorrupt, duplicateIngredient);
            }
            else
            {
                engine.PlaceIngredient(slotToCorrupt, "UnknownIngredient");
            }

            Assert.False(engine.ValidateAssembly(out _), $"Cycle {cycle}: Corrupted assembly unexpectedly validated");
            Assert.False(engine.IsSolved, $"Cycle {cycle}: INVARIANT FAILURE: IsSolved remained true after invalid validation");
        }
    }

    // =========================================================================
    // SECTION 2: STALL PUZZLE SCENE HEADLESS MODAL DISMISSAL VIA IINPUTSERVICE
    // =========================================================================

    [Fact]
    public void StallPuzzleScene_HeadlessModalDismissal_TwoPulseFastForwardAndDismiss_WithoutHanging()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var inputService = new MockInput();

        var cutsceneEvents = new List<CutsceneEndedEvent>();
        var letterEvents = new List<LetterOpenedEvent>();
        bus.Subscribe<CutsceneEndedEvent>(e => cutsceneEvents.Add(e));
        bus.Subscribe<LetterOpenedEvent>(e => letterEvents.Add(e));

        var scene = new StallPuzzleScene(saveData, qm, bus, inputService: inputService);
        scene.Enter();

        // 1. Solve the puzzle
        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved, "Puzzle must be solved");

        // 2. Advance solve timer to 3.0s to trigger letter modal
        scene.Update(3.0f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;

        Assert.True(letterModal.IsActive, "Letter modal must be active at solveTimer >= 3.0f");
        Assert.False(letterModal.IsTypingComplete, "Typewriter should not be complete immediately");
        Assert.False(scene.RequestNextScene, "RequestNextScene should be false while modal is active");
        Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead, "GrayShadow_LetterRead must remain false while modal is open");

        // 3. Pulse 1: Confirm input fast-forwards typewriter
        inputService.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        inputService.IsConfirmJustPressed = false;

        Assert.True(letterModal.IsActive, "Modal should still be active on fast-forward frame");
        Assert.True(letterModal.IsTypingComplete, "Typewriter must be fast-forwarded to complete");
        Assert.False(scene.RequestNextScene, "Modal is not yet dismissed");

        // 4. Pulse 2: Confirm input dismisses modal
        inputService.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        inputService.IsConfirmJustPressed = false;

        // VERIFICATION: Modal is dismissed, next scene requested, letter read flag updated
        Assert.False(letterModal.IsActive, "Modal must be dismissed");
        Assert.True(scene.RequestNextScene, "RequestNextScene must be true upon modal dismissal");
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead, "GrayShadow_LetterRead must be true");

        // Verify events published
        Assert.Single(cutsceneEvents);
        Assert.Equal("GrayShadow_Letter", cutsceneEvents[0].CutsceneId);
        Assert.Single(letterEvents);
        Assert.Equal("Letter_GrayShadow_Hanoi", letterEvents[0].LetterId);
        Assert.Equal("Bóng Xám", letterEvents[0].Sender);

        // 5. Subsequent updates do NOT re-trigger modal or hang
        for (int i = 0; i < 60; i++)
        {
            scene.Update(0.1f);
        }
        Assert.False(letterModal.IsActive, "Modal must NOT re-activate on subsequent ticks");
        Assert.True(scene.RequestNextScene, "RequestNextScene must remain true");
    }

    [Fact]
    public void StallPuzzleScene_HeadlessModalDismissal_CancelSkipThenConfirmDismiss_WithoutHanging()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var inputService = new MockInput();

        var scene = new StallPuzzleScene(saveData, qm, bus, inputService: inputService);
        scene.Enter();

        // Solve
        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved);

        // Advance to modal
        scene.Update(3.0f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Pulse Cancel (Skip typewriter)
        inputService.IsCancelJustPressed = true;
        scene.Update(0.016f);
        inputService.IsCancelJustPressed = false;

        Assert.True(letterModal.IsTypingComplete, "Cancel pulse should fast-forward typewriter");
        Assert.True(letterModal.IsActive, "Modal remains active waiting for confirm to dismiss");

        // Pulse Confirm (Dismiss)
        inputService.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        inputService.IsConfirmJustPressed = false;

        Assert.False(letterModal.IsActive, "Modal must be dismissed");
        Assert.True(scene.RequestNextScene, "RequestNextScene must be true");
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);
    }

    [Fact]
    public void StallPuzzleScene_HeadlessModalDismissal_NaturalTypewriterThenSingleConfirm()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);
        var inputService = new MockInput();

        var scene = new StallPuzzleScene(saveData, qm, bus, inputService: inputService);
        scene.Enter();

        // Solve
        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);

        // Advance to modal
        scene.Update(3.0f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Advance 15 seconds without pressing any buttons -> typewriter finishes naturally
        for (int i = 0; i < 30; i++)
        {
            scene.Update(0.5f);
        }
        Assert.True(letterModal.IsTypingComplete, "Typewriter should be complete after 15s");
        Assert.True(letterModal.IsActive, "Modal should still be active waiting for confirm");
        Assert.False(scene.RequestNextScene, "Next scene not requested until player confirms");

        // Single Confirm pulse dismisses immediately
        inputService.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        inputService.IsConfirmJustPressed = false;

        Assert.False(letterModal.IsActive);
        Assert.True(scene.RequestNextScene);
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);
    }

    [Fact]
    public void StallPuzzleScene_NullInputService_DefensiveFallbackDismissal()
    {
        var bus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        // Construct WITHOUT IInputService (null)
        var scene = new StallPuzzleScene(saveData, qm, bus, inputService: null);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved);

        // Advance 3.0s -> triggers letter modal
        scene.Update(3.0f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Advance several ticks: should NOT throw NullReferenceException
        var exception = Record.Exception(() =>
        {
            for (int i = 0; i < 10; i++)
            {
                scene.Update(0.1f);
            }
        });
        Assert.Null(exception);

        // Programmatic dismissal via Dismiss()
        letterModal.Dismiss();
        Assert.False(letterModal.IsActive);
        Assert.True(letterModal.RequestClose);

        // Next scene update triggers fallback synchronization
        scene.Update(0.016f);

        Assert.True(scene.RequestNextScene, "Fallback branch must set RequestNextScene");
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "Fallback branch must set GrayShadow_LetterRead");
    }

    // =========================================================================
    // SECTION 3: END-TO-END GATE TRANSITION & PROGRESSION INTEGRATION
    // =========================================================================

    [Fact]
    public void E2E_HanoiGateFlow_StallSolveDismiss_DirectTransitionToWorldMap_NoDuplicateCutscene()
    {
        var bus = new EventBus();
        var audio = new NullAudio();
        var input = new MockInput();
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

        scene.Enter();

        // 1. Move player to Long Bien Gate & interact
        scene.Kinematics.Position = scene.LongBienGate.Position;
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Verify transitioned into Stall Puzzle MiniGame
        Assert.Equal(GameState.MiniGameStalls, sm.CurrentState);

        var stallModalField = typeof(HanoiExplorationScene).GetField("_stallPuzzleModal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var stallModal = (StallPuzzleScene)stallModalField!.GetValue(scene)!;
        Assert.NotNull(stallModal);

        // 2. Solve the stall puzzle
        stallModal.Push("Obj_FruitStall", Direction.Left);
        stallModal.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(stallModal.IsSolved);
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);

        // 3. Advance to letter modal in StallPuzzleScene
        for (int i = 0; i < 35; i++)
        {
            scene.Update(0.1f);
        }

        var letterModalField = typeof(StallPuzzleScene).GetField("_letterModal",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)letterModalField!.GetValue(stallModal)!;
        Assert.True(letterModal.IsActive);

        // 4. Pulse confirm 1 (fast forward typewriter)
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;
        Assert.True(letterModal.IsTypingComplete);

        // 5. Pulse confirm 2 (dismiss letter modal)
        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Modal closed and exploration popped back to Exploration
        Assert.Equal(GameState.Exploration, sm.CurrentState);
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "GrayShadow_LetterRead must be true upon dismiss from StallPuzzleScene");

        // 6. Player interacts with Long Bien gate again
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // VERIFICATION: RequestWorldMap is true, and gameState was NEVER set to Cutscene!
        Assert.True(scene.RequestWorldMap, "RequestWorldMap must be true because gate is unlocked and letter was already read");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }

    // Helper: Permutation generator
    private static List<string[]> GeneratePermutations(string[] items)
    {
        var result = new List<string[]>();
        Permute(items, 0, items.Length - 1, result);
        return result;
    }

    private static void Permute(string[] items, int start, int end, List<string[]> result)
    {
        if (start == end)
        {
            result.Add((string[])items.Clone());
            return;
        }

        for (int i = start; i <= end; i++)
        {
            (items[start], items[i]) = (items[i], items[start]);
            Permute(items, start + 1, end, result);
            (items[start], items[i]) = (items[i], items[start]);
        }
    }
}
