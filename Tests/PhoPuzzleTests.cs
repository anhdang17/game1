using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Models;
using GameProject.Core.Puzzles;
using Xunit;

namespace GameProject.Tests;

public class PhoPuzzleTests
{
    [Fact]
    public void CorrectAuthenticSequence_ValidatesSuccessfully()
    {
        var engine = new PhoCookingEngine();

        // 1. Noodles at base
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        // 2. Meat slices
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        // 3. Fresh herbs
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        // 4. Hot bone broth
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        // 5. Spice / garnish
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool success = engine.ValidateAssembly(out string error);

        Assert.True(success);
        Assert.Empty(error);
        Assert.True(engine.IsSolved);
    }

    [Fact]
    public void AlternativeGarnish_AlsoValidatesSuccessfully()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.GiaVi);

        bool success = engine.ValidateAssembly(out string error);

        Assert.True(success);
        Assert.Empty(error);
        Assert.True(engine.IsSolved);
    }

    [Fact]
    public void IncompleteBowl_ReturnsMissingIngredientsError()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        // Slot 3 & 4 empty

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("thiếu nguyên liệu", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void BrothPlacedFirst_ReturnsContextualAdviceFromButChi()
    {
        var engine = new PhoCookingEngine();

        // Broth placed in slot 0
        engine.PlaceIngredient(0, ItemIds.NuocDung);
        engine.PlaceIngredient(1, ItemIds.BanhPho);
        engine.PlaceIngredient(2, ItemIds.ThitBo);
        engine.PlaceIngredient(3, ItemIds.HanhLa);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("Nước dùng mà chan trước khi cho bánh phở", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void HerbsPlacedBeforeMeat_ReturnsSpecificFeedback()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.HanhLa); // Herbs placed before meat
        engine.PlaceIngredient(2, ItemIds.ThitBo);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("Thịt bò thái mỏng phải trải đều trên bánh phở trước", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void DuplicateIngredients_AreRejected()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.BanhPho); // Duplicate
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("riêng biệt", error);
    }

    [Fact]
    public void ResetPuzzle_ClearsAllSlots()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);

        engine.ResetPuzzle();

        for (int i = 0; i < PhoCookingEngine.TotalSlots; i++)
        {
            Assert.Null(engine.CurrentSlots[i]);
        }
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void Adversarial_AllOtherPermutationsOf5Ingredients_AreRejected()
    {
        // 5! = 120 total permutations.
        // Exactly 1 permutation is the canonical solution.
        // Test that all other 119 permutations return false.

        var ingredients = new[]
        {
            ItemIds.BanhPho,
            ItemIds.ThitBo,
            ItemIds.HanhLa,
            ItemIds.NuocDung,
            ItemIds.ThaoMoc
        };

        var permutations = GetPermutations(ingredients, 5).ToList();
        Assert.Equal(120, permutations.Count);

        int validCount = 0;
        int rejectedCount = 0;

        var engine = new PhoCookingEngine();

        foreach (var perm in permutations)
        {
            engine.ResetPuzzle();
            var list = perm.ToList();
            for (int i = 0; i < 5; i++)
            {
                engine.PlaceIngredient(i, list[i]);
            }

            if (engine.ValidateAssembly(out _))
            {
                validCount++;
            }
            else
            {
                rejectedCount++;
            }
        }

        Assert.Equal(1, validCount);
        Assert.Equal(119, rejectedCount);
    }

    private static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
    {
        if (length == 1) return list.Select(t => new T[] { t });

        return GetPermutations(list, length - 1)
            .SelectMany(t => list.Where(e => !t.Contains(e)),
                (t1, t2) => t1.Concat(new T[] { t2 }));
    }

    [Fact]
    public void PhoAssemblyScene_CompleteSequence_SetsQ2CompletedAnd33PercentColor()
    {
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new GameProject.Core.Quests.QuestManager(eventBus);
        GameProject.Core.Quests.HanoiQuests.RegisterAll(questManager);

        bool completedEventFired = false;
        eventBus.Subscribe<GameProject.Core.Events.PhoAssemblyCompletedEvent>(_ => completedEventFired = true);

        var scene = new GameProject.Stages.PhoAssemblyScene(saveData, questManager, eventBus);
        scene.Enter();

        // Place cards in canonical order:
        // 0: BanhPho, 1: ThitBo, 2: HanhLa, 3: NuocDung, 4: ThaoMoc
        for (int i = 0; i < 5; i++)
        {
            scene.PlaceCard(i, i);
        }

        bool valid = scene.Validate();
        Assert.True(valid);
        Assert.True(scene.IsSolved);
        Assert.True(saveData.Level1State.QuestFlags.Q2_Completed);
        Assert.True(saveData.Level1State.QuestFlags.Q2_PhoAssembled);
        Assert.Equal(33.3f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi));
        Assert.True(completedEventFired);
    }

    [Fact]
    public void PhoAssemblyScene_BrothBeforeNoodles_RejectsWithButChiWarning()
    {
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new GameProject.Core.Quests.QuestManager(eventBus);
        GameProject.Core.Quests.HanoiQuests.RegisterAll(questManager);

        var scene = new GameProject.Stages.PhoAssemblyScene(saveData, questManager, eventBus);
        scene.Enter();

        // Place broth (card 3) in slot 0, noodles (card 0) in slot 1, and rest filled
        scene.PlaceCard(0, 3);
        scene.PlaceCard(1, 0);
        scene.PlaceCard(2, 1);
        scene.PlaceCard(3, 2);
        scene.PlaceCard(4, 4);

        bool valid = scene.Validate();
        Assert.False(valid);
        Assert.False(scene.IsSolved);
        Assert.NotNull(scene.FeedbackMessage);
        Assert.Contains("Nước dùng mà chan trước khi cho bánh phở", scene.FeedbackMessage);
        Assert.False(saveData.Level1State.QuestFlags.Q2_Completed);
    }

    [Fact]
    public void PhoAssemblyScene_AudioFeedback_EmitsSuccessAndBloomSfx()
    {
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new GameProject.Core.Quests.QuestManager(eventBus);
        GameProject.Core.Quests.HanoiQuests.RegisterAll(questManager);
        var audioService = new GameProject.Core.Ports.RecordingAudioService();

        var scene = new GameProject.Stages.PhoAssemblyScene(saveData, questManager, eventBus, audioService);
        scene.Enter();

        for (int i = 0; i < 5; i++)
        {
            scene.PlaceCard(i, i);
        }
        scene.Validate();

        Assert.Contains(GameProject.Core.Ports.SfxType.PhoCookSuccess, audioService.PlayedSfx);
        Assert.Contains(GameProject.Core.Ports.SfxType.ColorBloomWave, audioService.PlayedSfx);
    }

    [Fact]
    public void PhoAssemblyScene_Reset_ClearsEngineAndUI()
    {
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new GameProject.Core.Quests.QuestManager(eventBus);

        bool resetFired = false;
        eventBus.Subscribe<GameProject.Core.Events.PhoPuzzleResetEvent>(_ => resetFired = true);

        var scene = new GameProject.Stages.PhoAssemblyScene(saveData, questManager, eventBus);
        scene.Enter();

        scene.PlaceCard(0, 0);
        scene.PlaceCard(1, 1);
        scene.Reset();

        Assert.True(resetFired);
        for (int i = 0; i < 5; i++)
        {
            Assert.Null(scene.CurrentSlots[i]);
        }
    }

    [Fact]
    public void PhoAssemblyScene_SaveLoad_HydratesQ2FlagsProperly()
    {
        var storage = new GameProject.Tests.Mocks.MockSaveStorage();
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveService = new GameProject.Core.Persistence.SaveLoadService("Data/savegame.json", storage, eventBus);

        var data = SaveData.CreateDefault();
        var qm = new GameProject.Core.Quests.QuestManager(eventBus);
        GameProject.Core.Quests.HanoiQuests.RegisterAll(qm);

        var scene = new GameProject.Stages.PhoAssemblyScene(data, qm, eventBus);
        scene.Enter();
        for (int i = 0; i < 5; i++)
        {
            scene.PlaceCard(i, i);
        }
        scene.Validate();

        saveService.Save(data);

        var reloaded = saveService.Load();
        Assert.True(reloaded.Level1State.QuestFlags.Q2_Completed);
        Assert.True(reloaded.Level1State.QuestFlags.Q2_PhoAssembled);
        Assert.Equal(33.3f, reloaded.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi));
    }
}
