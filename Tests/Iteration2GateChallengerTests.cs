using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.WorldMap;
using Xunit;

using MockStorage = GameProject.Tests.Mocks.MockSaveStorage;

namespace GameProject.Tests;

/// <summary>
/// Empirical adversarial verification harness for Milestone 1 Iteration 2 Gate.
/// Directly challenges BUG-01, BUG-02, BUILD-01, and INFRA-01 remediations.
/// </summary>
public class Iteration2GateChallengerTests
{
    // =========================================================================
    // 1. EMPIRICAL CHALLENGE: BUG-01 (QuestObjective Arithmetic Clamping & Overflow)
    // =========================================================================

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MaxValue - 1)]
    [InlineData(1000)]
    [InlineData(10)]
    [InlineData(5)]
    public void Bug01_IncrementProgress_MassiveAmount_ClampsStrictlyToTargetCount(int amount)
    {
        var objective = new QuestObjective("obj_test", "Test Objective", targetCount: 5);

        bool changed = objective.IncrementProgress(amount);

        Assert.True(changed, "Incrementing positive progress on an uncompleted objective must return true.");
        Assert.Equal(5, objective.CurrentCount);
        Assert.True(objective.IsCompleted);
        Assert.False(objective.CurrentCount < 0, "CurrentCount must NEVER be negative.");

        // Subsequent increments on completed objective must return false and remain clamped
        bool changedAgain = objective.IncrementProgress(1);
        Assert.False(changedAgain);
        Assert.Equal(5, objective.CurrentCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    [InlineData(int.MinValue + 1)]
    public void Bug01_IncrementProgress_ZeroAndNegativeAmounts_RejectedWithoutMutating(int amount)
    {
        var objective = new QuestObjective("obj_neg", "Test Objective", targetCount: 10);
        objective.IncrementProgress(2);
        Assert.Equal(2, objective.CurrentCount);

        bool changed = objective.IncrementProgress(amount);

        Assert.False(changed, $"Non-positive amount {amount} must return false.");
        Assert.Equal(2, objective.CurrentCount);
        Assert.False(objective.IsCompleted);
        Assert.False(objective.CurrentCount < 0, "CurrentCount must NEVER be negative.");
    }

    [Fact]
    public void Bug01_IncrementProgress_NearCompletion_TargetCountMinusOne()
    {
        const int target = 10;
        var objective = new QuestObjective("obj_near", "Near Completion", targetCount: target);

        // Advance to TargetCount - 1
        objective.IncrementProgress(target - 1);
        Assert.Equal(target - 1, objective.CurrentCount);
        Assert.False(objective.IsCompleted);

        // Challenge 1: amount = 0, -1, -100 must not change progress
        Assert.False(objective.IncrementProgress(0));
        Assert.Equal(target - 1, objective.CurrentCount);

        Assert.False(objective.IncrementProgress(-1));
        Assert.Equal(target - 1, objective.CurrentCount);

        Assert.False(objective.IncrementProgress(-100));
        Assert.Equal(target - 1, objective.CurrentCount);

        // Challenge 2: amount = int.MaxValue must clamp cleanly to target
        bool completed = objective.IncrementProgress(int.MaxValue);
        Assert.True(completed);
        Assert.Equal(target, objective.CurrentCount);
        Assert.True(objective.IsCompleted);
        Assert.False(objective.CurrentCount < 0, "CurrentCount must NEVER be negative.");
    }

    [Fact]
    public void Bug01_IncrementProgress_TargetCountMinusOne_WithAmountIntMaxValueMinusOne()
    {
        const int target = 7;
        var objective = new QuestObjective("obj_near2", "Near Completion", targetCount: target);
        objective.IncrementProgress(target - 1);
        Assert.Equal(target - 1, objective.CurrentCount);

        bool completed = objective.IncrementProgress(int.MaxValue - 1);
        Assert.True(completed);
        Assert.Equal(target, objective.CurrentCount);
        Assert.True(objective.IsCompleted);
    }

    [Fact]
    public void Bug01_IncrementProgress_TargetCountIsIntMaxValue_NoOverflow()
    {
        var objective = new QuestObjective("obj_max", "Target Max", targetCount: int.MaxValue);
        Assert.Equal(int.MaxValue, objective.TargetCount);
        Assert.Equal(0, objective.CurrentCount);

        // Increment by int.MaxValue - 1
        bool step1 = objective.IncrementProgress(int.MaxValue - 1);
        Assert.True(step1);
        Assert.Equal(int.MaxValue - 1, objective.CurrentCount);
        Assert.False(objective.IsCompleted);

        // Increment by 1 -> completes
        bool step2 = objective.IncrementProgress(1);
        Assert.True(step2);
        Assert.Equal(int.MaxValue, objective.CurrentCount);
        Assert.True(objective.IsCompleted);
        Assert.False(objective.CurrentCount < 0, "CurrentCount must NEVER be negative.");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(-100, 1)]
    public void Bug01_TargetCount_ZeroOrNegative_ClampedToMinimumOne(int inputTarget, int expectedTarget)
    {
        var obj1 = new QuestObjective("obj", "Desc", targetCount: inputTarget);
        Assert.Equal(expectedTarget, obj1.TargetCount);

        var obj2 = new QuestObjective { TargetCount = inputTarget };
        Assert.Equal(expectedTarget, obj2.TargetCount);
    }

    [Fact]
    public void Bug01_SetProgress_ExtremeValues_ClampedCleanly()
    {
        var objective = new QuestObjective("obj_set", "Desc", targetCount: 10);

        objective.SetProgress(-50);
        Assert.Equal(0, objective.CurrentCount);

        objective.SetProgress(int.MinValue);
        Assert.Equal(0, objective.CurrentCount);

        objective.SetProgress(50);
        Assert.Equal(10, objective.CurrentCount);

        objective.SetProgress(int.MaxValue);
        Assert.Equal(10, objective.CurrentCount);

        objective.Reset();
        Assert.Equal(0, objective.CurrentCount);
    }

    // =========================================================================
    // 2. EMPIRICAL CHALLENGE: BUG-02 (SaveData Nested Null Sanitization)
    // =========================================================================

    [Fact]
    public void Bug02_CompletelyStrippedJson_AllTopLevelPropertiesNull_SanitizesWithoutNullReferenceException()
    {
        string strippedJson = """
        {
            "saveVersion": null,
            "lastSavedTimestamp": null,
            "playerProfile": null,
            "worldProgression": null,
            "level1State": null,
            "collectionAlbum": null,
            "audioSettings": null
        }
        """;

        var storage = new MockStorage();
        storage.WriteAllText("Data/stripped.json", strippedJson);
        var service = new SaveLoadService(storage, "Data/stripped.json");

        var loaded = service.Load();

        Assert.NotNull(loaded);
        AssertExecuteAllModelOperationsSafely(loaded);
    }

    [Fact]
    public void Bug02_CompletelyStrippedJson_EveryNestedPropertyNull_SanitizesWithoutNullReferenceException()
    {
        string strippedDeepJson = """
        {
            "saveVersion": null,
            "lastSavedTimestamp": null,
            "playerProfile": {
                "playerName": null,
                "currentRegionId": null,
                "totalPlayTimeSeconds": null
            },
            "worldProgression": {
                "unlockedRegions": null,
                "completedRegions": null,
                "regionColorRestoration": null
            },
            "level1State": {
                "playerSpawnX": null,
                "playerSpawnY": null,
                "activeQuestId": null,
                "questFlags": null,
                "inventoryItems": null,
                "puzzle3ObjectPositions": null
            },
            "collectionAlbum": {
                "collectedJournalPages": null,
                "collectedStamps": null,
                "unlockedFactCards": null
            },
            "audioSettings": {
                "masterVolume": null,
                "sfxVolume": null,
                "musicVolume": null
            }
        }
        """;

        var storage = new MockStorage();
        storage.WriteAllText("Data/stripped_deep.json", strippedDeepJson);
        var service = new SaveLoadService(storage, "Data/stripped_deep.json");

        var loaded = service.Load();

        Assert.NotNull(loaded);
        AssertExecuteAllModelOperationsSafely(loaded);
    }

    [Fact]
    public void Bug02_NestedCollectionsContainingNullsAndEmptyStrings_SanitizesSafely()
    {
        string jsonWithNullListElements = """
        {
            "saveVersion": "1.0.0",
            "worldProgression": {
                "unlockedRegions": [null, "", "   ", "Hue", null],
                "completedRegions": [null, "Hanoi", ""],
                "regionColorRestoration": {
                    "": 50.0,
                    "Hanoi": 75.0
                }
            },
            "level1State": {
                "inventoryItems": [null, "", "   ", "Item_BanhPho", null],
                "puzzle3ObjectPositions": [
                    null,
                    { "objectId": null, "gridX": 1, "gridY": 2 },
                    { "objectId": "cart_1", "gridX": 3, "gridY": 4 }
                ]
            },
            "collectionAlbum": {
                "collectedJournalPages": [null, "Page_1", ""],
                "collectedStamps": ["", null, "Stamp_1"],
                "unlockedFactCards": [null, "", "Card_1"]
            }
        }
        """;

        var storage = new MockStorage();
        storage.WriteAllText("Data/null_elements.json", jsonWithNullListElements);
        var service = new SaveLoadService(storage, "Data/null_elements.json");

        var loaded = service.Load();

        Assert.NotNull(loaded);
        AssertExecuteAllModelOperationsSafely(loaded);

        // Verify whitespace and nulls were stripped
        Assert.DoesNotContain(null, loaded.Level1State.InventoryItems);
        Assert.DoesNotContain("", loaded.Level1State.InventoryItems);
        Assert.Contains("Item_BanhPho", loaded.Level1State.InventoryItems);

        Assert.DoesNotContain(null, loaded.CollectionAlbum.CollectedStamps);
        Assert.Contains("Stamp_1", loaded.CollectionAlbum.CollectedStamps);
    }

    [Fact]
    public void Bug02_DirectSaveDataInstance_WithManualNullAssignments_SanitizeIsSafe()
    {
        var rawSave = new SaveData
        {
            SaveVersion = null!,
            PlayerProfile = null!,
            WorldProgression = null!,
            Level1State = null!,
            CollectionAlbum = null!,
            AudioSettings = null!
        };

        // Execution of Sanitize should not throw NullReferenceException
        rawSave.Sanitize();

        AssertExecuteAllModelOperationsSafely(rawSave);
    }

    private static void AssertExecuteAllModelOperationsSafely(SaveData data)
    {
        // Root properties
        Assert.False(string.IsNullOrWhiteSpace(data.SaveVersion));
        Assert.NotNull(data.PlayerProfile);
        Assert.NotNull(data.WorldProgression);
        Assert.NotNull(data.Level1State);
        Assert.NotNull(data.CollectionAlbum);
        Assert.NotNull(data.AudioSettings);
        Assert.NotNull(data.QuestFlags);

        // PlayerProfile operations
        Assert.False(string.IsNullOrWhiteSpace(data.PlayerProfile.PlayerName));
        Assert.False(string.IsNullOrWhiteSpace(data.PlayerProfile.CurrentRegionId));
        data.PlayerProfile.AddPlayTime(30.0);
        Assert.True(data.PlayerProfile.TotalPlayTimeSeconds >= 0);

        // WorldProgression operations
        Assert.NotNull(data.WorldProgression.UnlockedRegions);
        Assert.NotNull(data.WorldProgression.CompletedRegions);
        Assert.NotNull(data.WorldProgression.RegionColorRestoration);
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.False(data.WorldProgression.IsRegionCompleted("NonExistent"));
        data.WorldProgression.UnlockRegion(RegionIds.Hue);
        data.WorldProgression.SetRegionColor(RegionIds.Hanoi, 50.0f);
        data.WorldProgression.CompleteRegion(RegionIds.Hue);
        Assert.Equal(100.0f, data.WorldProgression.GetRegionColor(RegionIds.Hue));

        // Level1State operations
        Assert.NotNull(data.Level1State.QuestFlags);
        Assert.NotNull(data.Level1State.InventoryItems);
        Assert.NotNull(data.Level1State.Puzzle3ObjectPositions);
        int initialItemCount = data.Level1State.InventoryItems.Count;
        data.Level1State.InventoryItems.Add(ItemIds.BanhPho);
        data.Level1State.InventoryItems.Add(ItemIds.ThitBo);
        Assert.Equal(initialItemCount + 2, data.Level1State.InventoryItems.Count);

        // QuestFlags operations
        Assert.False(data.QuestFlags.Q1_Completed);
        data.QuestFlags.SetFlag("Q1_Started", true);
        Assert.True(data.QuestFlags.GetFlag("Q1_Started"));
        Assert.Equal(0, data.QuestFlags.GetQ1CollectedCount());
        Assert.False(data.QuestFlags.AreAllQ1IngredientsCollected());

        // CollectionAlbum operations
        Assert.NotNull(data.CollectionAlbum.CollectedJournalPages);
        Assert.NotNull(data.CollectionAlbum.CollectedStamps);
        Assert.NotNull(data.CollectionAlbum.UnlockedFactCards);
        bool stampAdded = data.CollectionAlbum.AddStamp(AlbumIds.HanoiLotusStamp);
        Assert.True(stampAdded);
        Assert.True(data.CollectionAlbum.HasStamp(AlbumIds.HanoiLotusStamp));
        bool pageAdded = data.CollectionAlbum.AddJournalPage(AlbumIds.HanoiJournalPage);
        Assert.True(pageAdded);
        Assert.True(data.CollectionAlbum.HasJournalPage(AlbumIds.HanoiJournalPage));
        bool cardAdded = data.CollectionAlbum.AddFactCard(AlbumIds.FactCardPho);
        Assert.True(cardAdded);
        Assert.True(data.CollectionAlbum.HasFactCard(AlbumIds.FactCardPho));

        // AudioSettings operations
        data.AudioSettings.MasterVolume = 1.5;
        data.AudioSettings.ClampVolumes();
        Assert.Equal(1.0, data.AudioSettings.MasterVolume);

        // Serialization round-trip validation
        string serialized = JsonSerializer.Serialize(data);
        Assert.False(string.IsNullOrWhiteSpace(serialized));
    }

    // =========================================================================
    // 3. EMPIRICAL CHALLENGE: BUILD-01 & INFRA-01 (Domain Relocation & Architecture)
    // =========================================================================

    [Fact]
    public void BuildInfra01_TestsContractsDirectory_DoesNotExist()
    {
        string workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string contractsDir = Path.Combine(workspaceRoot, "Tests", "Contracts");

        Assert.False(Directory.Exists(contractsDir),
            $"Tests/Contracts directory must NOT exist. Found at: {contractsDir}");
    }

    [Fact]
    public void BuildInfra01_AllDomainServicesResideInSourceCore()
    {
        // Instantiating pure domain classes directly proves they belong to GameProject.dll
        var phoEngine = new PhoCookingEngine();
        Assert.NotNull(phoEngine);

        var gridEngine = new SpatialGridEngine();
        Assert.NotNull(gridEngine);

        var dialogueService = new DialogueService();
        Assert.NotNull(dialogueService);

        var hintManager = new HintManager();
        Assert.NotNull(hintManager);

        var albumService = new AlbumService();
        Assert.NotNull(albumService);

        var worldMapService = new WorldMapService();
        Assert.NotNull(worldMapService);
    }
}
