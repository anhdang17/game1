# Handoff Report: Domain Bug Remediation Analysis & Exact Fix Strategy

**Agent**: `teamwork_explorer_m1_it2_1`  
**Role**: explorer (investigation, synthesis)  
**Milestone**: Milestone 1 (Core Domain Engine & Persistence) — Iteration 2  
**Date**: 2026-11-08  
**Working Directory**: `/Users/mac/Projects/game1`  
**State Folder**: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_1/`  
**Status**: COMPLETE  

---

## Executive Summary

During Milestone 1 Iteration 1 adversarial challenger evaluations, two concrete domain bugs were identified and empirically confirmed:
1. **BUG-01 (Arithmetic Integer Overflow)**: In `Source/Core/Quests/QuestObjective.cs`, `IncrementProgress(int amount)` wraps around to negative values when `amount = int.MaxValue`, turning `CurrentCount` into `-2147483648`.
2. **BUG-02 (Incomplete Nested Sanitization)**: In `Source/Core/Models/SaveData.cs`, `Sanitize()` only ensures that top-level model references are non-null. Deserializing malformed or partial JSON leaves internal properties (e.g. `Level1State.QuestFlags`, `Level1State.InventoryItems`, `CollectionAlbum.CollectedStamps`) as `null`, triggering immediate `NullReferenceException` upon save load or property access.

This report delivers a comprehensive code investigation and an airtight, mathematically proven, multi-tiered defensive remediation strategy with verbatim code replacements ready for Worker implementation.

---

## 1. Observation

### 1.1 Direct Observation of BUG-01 (Integer Overflow in `QuestObjective.cs`)

**Source Location**: `/Users/mac/Projects/game1/Source/Core/Quests/QuestObjective.cs`, lines 33-40:
```csharp
    /// <summary>
    /// Increments objective progress by the specified amount, clamped to TargetCount.
    /// Returns true if progress changed; false if already completed.
    /// </summary>
    public bool IncrementProgress(int amount = 1)
    {
        if (amount <= 0 || CurrentCount >= TargetCount)
            return false;

        CurrentCount = Math.Min(TargetCount, CurrentCount + amount);
        return true;
    }
```

**Direct Observation & Empirical Defect**:
- In C#, 32-bit signed integer arithmetic is `unchecked` by default.
- When `CurrentCount > 0` (e.g. `CurrentCount = 1`, `TargetCount = 5`) and `amount` is large (e.g. `int.MaxValue = 2,147,483,647`), the expression `CurrentCount + amount` wraps around:
  `1 + 2,147,483,647 = -2,147,483,648` (`int.MinValue`).
- `Math.Min(TargetCount, -2147483648)` compares `5` against `-2147483648` and evaluates to `-2147483648`.
- Consequently, `CurrentCount` becomes `-2147483648`.
- In `Tests/AdversarialCoreTests.cs` (lines 690-705), this defect is empirically captured by the following test:
  ```csharp
  [Fact]
  public void Bug_QuestObjective_IntegerOverflow_CausesNegativeCurrentCount()
  {
      var obj = new CoreQuestObjective("Item_BanhPho", "Thu thập bánh phở", targetCount: 5);
      obj.IncrementProgress(1);
      Assert.Equal(1, obj.CurrentCount);

      // Exploit: Pass int.MaxValue
      obj.IncrementProgress(int.MaxValue);

      // Verification: The count has overflowed to a negative number!
      Assert.True(obj.CurrentCount < 0, 
          $"Vulnerability Confirmed: QuestObjective.CurrentCount overflowed to {obj.CurrentCount}");
  }
  ```
- Executing this test confirmed the bug verbatim:
  ```bash
  dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Bug_QuestObjective_IntegerOverflow"
  ```
  Output: `Passed! - Failed: 0, Passed: 1, Total: 1` (Passed because the test asserts that `CurrentCount < 0`).

---

### 1.2 Direct Observation of BUG-02 (Incomplete Sanitization in `SaveData.cs`)

**Source Locations**:
1. `/Users/mac/Projects/game1/Source/Core/Models/SaveData.cs`, lines 21-26, 41-51:
   ```csharp
   [JsonIgnore]
   public QuestFlags QuestFlags => Level1State.QuestFlags;
   ...
   public void Sanitize()
   {
       PlayerProfile ??= PlayerProfile.CreateDefault();
       WorldProgression ??= WorldProgression.CreateDefault();
       Level1State ??= Level1State.CreateDefault();
       CollectionAlbum ??= CollectionAlbum.CreateDefault();
       AudioSettings ??= AudioSettings.CreateDefault();

       AudioSettings.ClampVolumes();
       WorldProgression.Sanitize();
   }
   ```
2. `/Users/mac/Projects/game1/Source/Core/Models/Level1State.cs`, lines 27-45:
   ```csharp
   public sealed class Level1State
   {
       public double PlayerSpawnX { get; set; } = 120.0;
       public double PlayerSpawnY { get; set; } = 250.0;
       public string ActiveQuestId { get; set; } = "Quest_Hanoi_01";
       public QuestFlags QuestFlags { get; set; } = new();
       public List<string> InventoryItems { get; set; } = new();
       public List<Puzzle3ObjectPosition> Puzzle3ObjectPositions { get; set; } = new();
       ...
       // NO Sanitize() method exists on Level1State
   }
   ```
3. `/Users/mac/Projects/game1/Source/Core/Models/CollectionAlbum.cs`, lines 23-45:
   ```csharp
   public sealed class CollectionAlbum
   {
       public List<string> CollectedJournalPages { get; set; } = new();
       public List<string> CollectedStamps { get; set; } = new();
       public List<string> UnlockedFactCards { get; set; } = new();
       ...
       // NO Sanitize() method exists on CollectionAlbum
   }
   ```

**Direct Observation & Empirical Defect**:
- When deserializing JSON payloads that contain explicit null fields or omitted sub-properties:
  ```json
  {
      "saveVersion": "1.0.0",
      "level1State": {
          "questFlags": null,
          "inventoryItems": null,
          "puzzle3ObjectPositions": null
      },
      "collectionAlbum": {
          "collectedJournalPages": null,
          "collectedStamps": null,
          "unlockedFactCards": null
      }
  }
  ```
- Deserialization instantiates `Level1State` and `CollectionAlbum`, but assigns `null` to their internal properties.
- When `SaveData.Sanitize()` runs:
  - `Level1State ??= Level1State.CreateDefault();` does nothing because `Level1State` is already non-null.
  - `CollectionAlbum ??= CollectionAlbum.CreateDefault();` does nothing because `CollectionAlbum` is already non-null.
  - No sanitization is performed on child properties.
- Consequence:
  - Calling `saveData.QuestFlags` invokes `Level1State.QuestFlags` (which is `null`), and dereferencing any flag throws `NullReferenceException`.
  - Calling `saveData.Level1State.InventoryItems.Add(...)` throws `NullReferenceException`.
  - Calling `saveData.CollectionAlbum.AddStamp(...)` throws `ArgumentNullException: Value cannot be null. (Parameter 'source')` in LINQ `Contains`.
- In `Tests/AdversarialCoreTests.cs` (lines 707-755), this defect is empirically captured by `Bug_SaveData_Sanitize_LeavesSubPropertiesNull_LeadingToNullReferenceException`.
- Executing this test confirmed the bug verbatim:
  ```bash
  dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Bug_SaveData_Sanitize"
  ```
  Output: `Passed! - Failed: 0, Passed: 1, Total: 1` (Passed because the test asserts that `QuestFlags == null` and unhandled exceptions are thrown).

---

### 1.3 Direct Observation of Proof-of-Defect Tests in Test Suite

- The test suite in `Tests/AdversarialCoreTests.cs` currently has 25 tests, of which tests 24 and 25 (`Bug_QuestObjective_IntegerOverflow_CausesNegativeCurrentCount` and `Bug_SaveData_Sanitize_LeavesSubPropertiesNull_LeadingToNullReferenceException`) assert that the defects exist.
- When the Worker remediates the production code, these two tests will fail unless they are also updated to assert the remediated, safe behavior.

---

## 2. Logic Chain

### 2.1 Failure Sequence of BUG-01
1. **Observation 1.1** shows `IncrementProgress(amount)` computes `CurrentCount + amount` in unchecked arithmetic.
2. In C#, signed 32-bit integers range from `-2,147,483,648` to `+2,147,483,647`. If `CurrentCount = 1` and `amount = int.MaxValue`, the sum wraps to `int.MinValue`.
3. `Math.Min(TargetCount, int.MinValue)` returns `int.MinValue`.
4. As a result, `CurrentCount` corrupts to a large negative number, `IsCompleted` evaluates to `false`, and event publishers dispatch negative progress.

### 2.2 Mathematical Proof of the Overflow-Free Subtraction Guard
1. In `IncrementProgress(amount)`:
   - Line 1: `if (amount <= 0 || CurrentCount >= TargetCount) return false;`
   - Therefore, for all code below this line: `amount > 0` and `CurrentCount < TargetCount` and `CurrentCount >= 0`.
2. Compute `int remainingNeeded = TargetCount - CurrentCount;`.
   - Since `CurrentCount < TargetCount` and `CurrentCount >= 0`, `remainingNeeded` is strictly in the range `[1, TargetCount]`.
   - Because `TargetCount <= int.MaxValue` and `CurrentCount >= 0`, `TargetCount - CurrentCount` cannot overflow or underflow `int`.
3. If `amount >= remainingNeeded`:
   - The caller is providing enough progress to reach or exceed `TargetCount`.
   - Set `CurrentCount = TargetCount;`.
   - No addition is ever performed. Overflow is mathematically impossible.
4. If `amount < remainingNeeded`:
   - This inequality implies `CurrentCount + amount < TargetCount`.
   - Since `TargetCount <= int.MaxValue`, the sum `CurrentCount + amount` is strictly less than `int.MaxValue`.
   - Therefore, `CurrentCount += amount;` is strictly guaranteed not to overflow.
5. In addition, guarding `TargetCount` at property initialization (`init => _targetCount = Math.Max(1, value);`) guarantees `TargetCount` is never `<= 0`, preventing any `ArgumentException` in `Math.Clamp`.

### 2.3 Failure Sequence of BUG-02
1. **Observation 1.2** demonstrates that JSON deserialization can produce objects where root containers exist, but child collections or sub-models are null.
2. `SaveData.Sanitize()` only checks `PlayerProfile ??= ...`, `WorldProgression ??= ...`, `Level1State ??= ...`, `CollectionAlbum ??= ...`.
3. Because `Level1State` and `CollectionAlbum` lack child `Sanitize()` routines, their child references remain null.
4. Downstream systems relying on `saveData.QuestFlags`, `saveData.Level1State.InventoryItems`, and `saveData.CollectionAlbum.CollectedStamps` suffer fatal runtime crashes (`NullReferenceException` / `ArgumentNullException`).

### 2.4 Four-Tier Defense-in-Depth Sanitization Architecture
To eliminate all possible permutations of null reference crashes, a 4-tier defense is formulated:
1. **Tier 1 (Encapsulated Backing Fields & Null-Coalescing Accessors)**:
   In `Level1State`, `CollectionAlbum`, `PlayerProfile`, and `SaveData`, properties use backing fields with null-coalescing getters and setters. If a property is read or set to null, it automatically falls back to an initialized empty container.
2. **Tier 2 (Dedicated Child `Sanitize()` Methods)**:
   Add `public void Sanitize()` to `Level1State`, `CollectionAlbum`, and `PlayerProfile`. Each method ensures collections are non-null, prunes null/whitespace strings (e.g. `InventoryItems.RemoveAll(string.IsNullOrWhiteSpace)`), clamps coordinates/times, and enforces defaults.
3. **Tier 3 (Cascade from `SaveData.Sanitize()`)**:
   `SaveData.Sanitize()` calls child sanitizers: `PlayerProfile.Sanitize()`, `WorldProgression.Sanitize()`, `Level1State.Sanitize()`, `CollectionAlbum.Sanitize()`, and `AudioSettings.ClampVolumes()`.
4. **Tier 4 (Safe Accessor on `SaveData.QuestFlags`)**:
   `SaveData.QuestFlags` getter defensively checks `Level1State ??= Level1State.CreateDefault(); return Level1State.QuestFlags;`. It can never return null under any condition.

### 2.5 Test Alignment Logic
1. **Observation 1.3** showed that `AdversarialCoreTests.cs` includes tests that assert the presence of these two bugs.
2. Once the Worker implements the remediations, these tests must be updated to assert the fixed behavior (safe clamping to `TargetCount`, non-null restored child properties, and successful collection additions).

---

## 3. Caveats

1. **Pure C# Scope**: This investigation is strictly focused on the pure C# domain model layer (`Source/Core/`). Presentation layers (Raylib rendering, window events, audio device drivers) are out of scope for Milestone 1.
2. **Test Project Type Shadowing**: As noted by previous challengers, `Tests/Contracts/` contains pre-existing stub classes. Tests verifying production domain logic should bind to `CoreApp::GameProject.Core.*` (via `Aliases="global,CoreApp"`) to ensure real production binaries in `GameProject.dll` are exercised.
3. **Read-Only Explorer Constraint**: In accordance with the Explorer archetype rules, no source code files in `Source/` or `Tests/` were modified during this investigation. All proposed modifications below are concrete specifications for the Worker.

---

## 4. Conclusion & Actionable Code Remediation Specifications

The exact causes of BUG-01 and BUG-02 have been completely isolated and verified. Below are the verbatim, drop-in replacement specifications for the Worker.

### 4.1 Remediation Step 1: `Source/Core/Quests/QuestObjective.cs`

**Target File**: `/Users/mac/Projects/game1/Source/Core/Quests/QuestObjective.cs`  
**Action**: Replace the entire file contents with:

```csharp
using System;

namespace GameProject.Core.Quests;

/// <summary>
/// Tracks individual objective criteria and progress within a quest.
/// </summary>
public sealed class QuestObjective
{
    private readonly int _targetCount = 1;

    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int TargetCount
    {
        get => _targetCount;
        init => _targetCount = Math.Max(1, value);
    }
    public int CurrentCount { get; private set; } = 0;
    public bool IsOptional { get; init; } = false;

    public bool IsCompleted => CurrentCount >= TargetCount;

    public QuestObjective() { }

    public QuestObjective(string id, string description, int targetCount = 1, bool isOptional = false)
    {
        Id = id;
        Description = description;
        _targetCount = Math.Max(1, targetCount);
        IsOptional = isOptional;
        CurrentCount = 0;
    }

    /// <summary>
    /// Increments objective progress by the specified amount, safely clamped to TargetCount without integer overflow.
    /// Returns true if progress changed; false if already completed or amount is non-positive.
    /// </summary>
    public bool IncrementProgress(int amount = 1)
    {
        if (amount <= 0 || CurrentCount >= TargetCount)
            return false;

        // Defensive overflow prevention:
        // Since CurrentCount < TargetCount, (TargetCount - CurrentCount) is strictly in [1, TargetCount].
        // If amount >= remaining needed, we complete the objective immediately without addition.
        int remainingNeeded = TargetCount - CurrentCount;
        if (amount >= remainingNeeded)
        {
            CurrentCount = TargetCount;
        }
        else
        {
            // Guaranteed safe: CurrentCount + amount < TargetCount <= int.MaxValue
            CurrentCount += amount;
        }

        return true;
    }

    /// <summary>
    /// Directly sets progress count, clamped between 0 and TargetCount.
    /// </summary>
    public void SetProgress(int count)
    {
        CurrentCount = Math.Clamp(count, 0, TargetCount);
    }

    /// <summary>
    /// Resets progress back to zero.
    /// </summary>
    public void Reset()
    {
        CurrentCount = 0;
    }
}
```

---

### 4.2 Remediation Step 2: `Source/Core/Models/Level1State.cs`

**Target File**: `/Users/mac/Projects/game1/Source/Core/Models/Level1State.cs`  
**Action**: Replace the entire file contents with:

```csharp
using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

/// <summary>
/// Serializable position coordinates for pushable obstacles in the Hanoi Quest 3 spatial grid puzzle.
/// </summary>
public sealed class Puzzle3ObjectPosition
{
    public string ObjectId { get; set; } = string.Empty;
    public int GridX { get; set; }
    public int GridY { get; set; }

    public Puzzle3ObjectPosition() { }

    public Puzzle3ObjectPosition(string objectId, int gridX, int gridY)
    {
        ObjectId = objectId;
        GridX = gridX;
        GridY = gridY;
    }
}

/// <summary>
/// State container for Hanoi Level 1 exploration, active quest, inventory, and puzzle object coordinates.
/// </summary>
public sealed class Level1State
{
    private string _activeQuestId = "Quest_Hanoi_01";
    private QuestFlags _questFlags = new();
    private List<string> _inventoryItems = new();
    private List<Puzzle3ObjectPosition> _puzzle3ObjectPositions = new();

    public double PlayerSpawnX { get; set; } = 120.0;
    public double PlayerSpawnY { get; set; } = 250.0;

    public string ActiveQuestId
    {
        get => _activeQuestId;
        set => _activeQuestId = string.IsNullOrWhiteSpace(value) ? "Quest_Hanoi_01" : value;
    }

    public QuestFlags QuestFlags
    {
        get => _questFlags ??= QuestFlags.CreateDefault();
        set => _questFlags = value ?? QuestFlags.CreateDefault();
    }

    public List<string> InventoryItems
    {
        get => _inventoryItems ??= new List<string>();
        set => _inventoryItems = value ?? new List<string>();
    }

    public List<Puzzle3ObjectPosition> Puzzle3ObjectPositions
    {
        get => _puzzle3ObjectPositions ??= new List<Puzzle3ObjectPosition>();
        set => _puzzle3ObjectPositions = value ?? new List<Puzzle3ObjectPosition>();
    }

    public static Level1State CreateDefault() => new()
    {
        PlayerSpawnX = 120.0,
        PlayerSpawnY = 250.0,
        ActiveQuestId = "Quest_Hanoi_01",
        QuestFlags = QuestFlags.CreateDefault(),
        InventoryItems = new List<string>(),
        Puzzle3ObjectPositions = new List<Puzzle3ObjectPosition>()
    };

    public void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(ActiveQuestId))
        {
            ActiveQuestId = "Quest_Hanoi_01";
        }

        QuestFlags ??= QuestFlags.CreateDefault();
        InventoryItems ??= new List<string>();
        Puzzle3ObjectPositions ??= new List<Puzzle3ObjectPosition>();

        InventoryItems.RemoveAll(string.IsNullOrWhiteSpace);
        Puzzle3ObjectPositions.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.ObjectId));

        if (double.IsNaN(PlayerSpawnX) || double.IsInfinity(PlayerSpawnX))
        {
            PlayerSpawnX = 120.0;
        }

        if (double.IsNaN(PlayerSpawnY) || double.IsInfinity(PlayerSpawnY))
        {
            PlayerSpawnY = 250.0;
        }
    }
}
```

---

### 4.3 Remediation Step 3: `Source/Core/Models/CollectionAlbum.cs`

**Target File**: `/Users/mac/Projects/game1/Source/Core/Models/CollectionAlbum.cs`  
**Action**: Replace the `CollectionAlbum` class definition (lines 20-70) with:

```csharp
/// <summary>
/// Persistent state model tracking collected journal pages, travel stamps, and unlocked cultural fact cards.
/// </summary>
public sealed class CollectionAlbum
{
    private List<string> _collectedJournalPages = new();
    private List<string> _collectedStamps = new();
    private List<string> _unlockedFactCards = new();

    public List<string> CollectedJournalPages
    {
        get => _collectedJournalPages ??= new List<string>();
        set => _collectedJournalPages = value ?? new List<string>();
    }

    public List<string> CollectedStamps
    {
        get => _collectedStamps ??= new List<string>();
        set => _collectedStamps = value ?? new List<string>();
    }

    public List<string> UnlockedFactCards
    {
        get => _unlockedFactCards ??= new List<string>();
        set => _unlockedFactCards = value ?? new List<string>();
    }

    public static CollectionAlbum CreateDefault() => new();

    public void Sanitize()
    {
        CollectedJournalPages ??= new List<string>();
        CollectedStamps ??= new List<string>();
        UnlockedFactCards ??= new List<string>();

        CollectedJournalPages.RemoveAll(string.IsNullOrWhiteSpace);
        CollectedStamps.RemoveAll(string.IsNullOrWhiteSpace);
        UnlockedFactCards.RemoveAll(string.IsNullOrWhiteSpace);
    }

    public bool AddJournalPage(string pageId)
    {
        Sanitize();
        if (!string.IsNullOrWhiteSpace(pageId) && !CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase))
        {
            CollectedJournalPages.Add(pageId);
            return true;
        }
        return false;
    }

    public bool AddStamp(string stampId)
    {
        Sanitize();
        if (!string.IsNullOrWhiteSpace(stampId) && !CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase))
        {
            CollectedStamps.Add(stampId);
            return true;
        }
        return false;
    }

    public bool AddFactCard(string cardId)
    {
        Sanitize();
        if (!string.IsNullOrWhiteSpace(cardId) && !UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase))
        {
            UnlockedFactCards.Add(cardId);
            return true;
        }
        return false;
    }

    public bool HasJournalPage(string pageId)
    {
        Sanitize();
        return !string.IsNullOrWhiteSpace(pageId) &&
               CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasStamp(string stampId)
    {
        Sanitize();
        return !string.IsNullOrWhiteSpace(stampId) &&
               CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase);
    }

    public bool HasFactCard(string cardId)
    {
        Sanitize();
        return !string.IsNullOrWhiteSpace(cardId) &&
               UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase);
    }
}
```

---

### 4.4 Remediation Step 4: `Source/Core/Models/PlayerProfile.cs`

**Target File**: `/Users/mac/Projects/game1/Source/Core/Models/PlayerProfile.cs`  
**Action**: Replace the entire file contents with:

```csharp
using System;

namespace GameProject.Core.Models;

/// <summary>
/// Represents the player's basic profile, current region, and accumulated play time.
/// </summary>
public sealed class PlayerProfile
{
    private string _playerName = "Bé Mây";
    private string _currentRegionId = "Hanoi";

    public string PlayerName
    {
        get => _playerName;
        set => _playerName = string.IsNullOrWhiteSpace(value) ? "Bé Mây" : value;
    }

    public string CurrentRegionId
    {
        get => _currentRegionId;
        set => _currentRegionId = string.IsNullOrWhiteSpace(value) ? "Hanoi" : value;
    }

    public double TotalPlayTimeSeconds { get; set; } = 0.0;

    public static PlayerProfile CreateDefault() => new()
    {
        PlayerName = "Bé Mây",
        CurrentRegionId = "Hanoi",
        TotalPlayTimeSeconds = 0.0
    };

    public void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(PlayerName))
        {
            PlayerName = "Bé Mây";
        }

        if (string.IsNullOrWhiteSpace(CurrentRegionId))
        {
            CurrentRegionId = "Hanoi";
        }

        if (double.IsNaN(TotalPlayTimeSeconds) || double.IsInfinity(TotalPlayTimeSeconds) || TotalPlayTimeSeconds < 0)
        {
            TotalPlayTimeSeconds = 0.0;
        }
    }

    public void AddPlayTime(double seconds)
    {
        if (seconds > 0 && !double.IsNaN(seconds) && !double.IsInfinity(seconds))
        {
            TotalPlayTimeSeconds += seconds;
        }
    }
}
```

---

### 4.5 Remediation Step 5: `Source/Core/Models/WorldProgression.cs`

**Target File**: `/Users/mac/Projects/game1/Source/Core/Models/WorldProgression.cs`  
**Action**: Replace the `Sanitize()` method (lines 104-123) and property declarations (lines 39-44) with:

```csharp
    private List<string> _unlockedRegions = new() { RegionHanoi };
    private List<string> _completedRegions = new();
    private Dictionary<string, float> _regionColorRestoration = new()
    {
        { RegionHanoi, 0.0f }
    };

    public List<string> UnlockedRegions
    {
        get => _unlockedRegions ??= new List<string> { RegionHanoi };
        set => _unlockedRegions = value ?? new List<string> { RegionHanoi };
    }

    public List<string> CompletedRegions
    {
        get => _completedRegions ??= new List<string>();
        set => _completedRegions = value ?? new List<string>();
    }

    public Dictionary<string, float> RegionColorRestoration
    {
        get => _regionColorRestoration ??= new Dictionary<string, float> { { RegionHanoi, 0.0f } };
        set => _regionColorRestoration = value ?? new Dictionary<string, float> { { RegionHanoi, 0.0f } };
    }

    public void Sanitize()
    {
        UnlockedRegions ??= new List<string>();
        UnlockedRegions.RemoveAll(string.IsNullOrWhiteSpace);
        if (!UnlockedRegions.Contains(RegionHanoi, StringComparer.OrdinalIgnoreCase))
        {
            UnlockedRegions.Add(RegionHanoi);
        }

        CompletedRegions ??= new List<string>();
        CompletedRegions.RemoveAll(string.IsNullOrWhiteSpace);

        RegionColorRestoration ??= new Dictionary<string, float>();

        foreach (var key in RegionColorRestoration.Keys.ToList())
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                RegionColorRestoration.Remove(key);
                continue;
            }

            float val = RegionColorRestoration[key];
            if (float.IsNaN(val) || float.IsInfinity(val))
            {
                RegionColorRestoration[key] = 0.0f;
            }
            else
            {
                RegionColorRestoration[key] = Math.Clamp(val, 0.0f, 100.0f);
            }
        }

        if (!RegionColorRestoration.ContainsKey(RegionHanoi))
        {
            RegionColorRestoration[RegionHanoi] = 0.0f;
        }
    }
```

---

### 4.6 Remediation Step 6: `Source/Core/Models/SaveData.cs`

**Target File**: `/Users/mac/Projects/game1/Source/Core/Models/SaveData.cs`  
**Action**: Replace the entire file contents with:

```csharp
using System;
using System.Text.Json.Serialization;

namespace GameProject.Core.Models;

/// <summary>
/// Root aggregate model for durable persistence and snapshot game state.
/// </summary>
public sealed class SaveData
{
    public const string CurrentSaveVersion = "1.0.0";

    private string _saveVersion = CurrentSaveVersion;
    private PlayerProfile _playerProfile = new();
    private WorldProgression _worldProgression = new();
    private Level1State _level1State = new();
    private CollectionAlbum _collectionAlbum = new();
    private AudioSettings _audioSettings = new();

    public string SaveVersion
    {
        get => _saveVersion;
        set => _saveVersion = string.IsNullOrWhiteSpace(value) ? CurrentSaveVersion : value;
    }

    public DateTimeOffset LastSavedTimestamp { get; set; } = DateTimeOffset.UtcNow;

    public PlayerProfile PlayerProfile
    {
        get => _playerProfile ??= PlayerProfile.CreateDefault();
        set => _playerProfile = value ?? PlayerProfile.CreateDefault();
    }

    public WorldProgression WorldProgression
    {
        get => _worldProgression ??= WorldProgression.CreateDefault();
        set => _worldProgression = value ?? WorldProgression.CreateDefault();
    }

    public Level1State Level1State
    {
        get => _level1State ??= Level1State.CreateDefault();
        set => _level1State = value ?? Level1State.CreateDefault();
    }

    public CollectionAlbum CollectionAlbum
    {
        get => _collectionAlbum ??= CollectionAlbum.CreateDefault();
        set => _collectionAlbum = value ?? CollectionAlbum.CreateDefault();
    }

    public AudioSettings AudioSettings
    {
        get => _audioSettings ??= AudioSettings.CreateDefault();
        set => _audioSettings = value ?? AudioSettings.CreateDefault();
    }

    /// <summary>
    /// Convenience property providing direct, null-safe access to QuestFlags in Level1State.
    /// </summary>
    [JsonIgnore]
    public QuestFlags QuestFlags
    {
        get
        {
            Level1State ??= Level1State.CreateDefault();
            return Level1State.QuestFlags;
        }
    }

    public static SaveData CreateDefault()
    {
        return new SaveData
        {
            SaveVersion = CurrentSaveVersion,
            LastSavedTimestamp = DateTimeOffset.UtcNow,
            PlayerProfile = PlayerProfile.CreateDefault(),
            WorldProgression = WorldProgression.CreateDefault(),
            Level1State = Level1State.CreateDefault(),
            CollectionAlbum = CollectionAlbum.CreateDefault(),
            AudioSettings = AudioSettings.CreateDefault()
        };
    }

    public void Sanitize()
    {
        SaveVersion = string.IsNullOrWhiteSpace(SaveVersion) ? CurrentSaveVersion : SaveVersion;

        PlayerProfile ??= PlayerProfile.CreateDefault();
        WorldProgression ??= WorldProgression.CreateDefault();
        Level1State ??= Level1State.CreateDefault();
        CollectionAlbum ??= CollectionAlbum.CreateDefault();
        AudioSettings ??= AudioSettings.CreateDefault();

        PlayerProfile.Sanitize();
        WorldProgression.Sanitize();
        Level1State.Sanitize();
        CollectionAlbum.Sanitize();
        AudioSettings.ClampVolumes();
    }
}
```

---

### 4.7 Remediation Step 7: `Tests/AdversarialCoreTests.cs` (Update Proof-of-Defect Tests)

**Target File**: `/Users/mac/Projects/game1/Tests/AdversarialCoreTests.cs`  
**Action**: Replace lines 690-754 with the updated verification tests:

```csharp
    [Fact]
    public void Remediation_QuestObjective_IntegerOverflow_ClampedSafelyToTargetCount()
    {
        var obj = new CoreQuestObjective("Item_BanhPho", "Thu thập bánh phở", targetCount: 5);
        obj.IncrementProgress(1);
        Assert.Equal(1, obj.CurrentCount);

        // Exploit attempt: Pass int.MaxValue
        bool changed = obj.IncrementProgress(int.MaxValue);

        // Verification: The count is safely clamped to TargetCount without overflow or negative numbers
        Assert.True(changed);
        Assert.Equal(5, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Subsequent increment returns false (already completed)
        Assert.False(obj.IncrementProgress(1));
        Assert.Equal(5, obj.CurrentCount);
    }

    [Fact]
    public void Remediation_SaveData_Sanitize_RepairsNullNestedProperties_PreventingExceptions()
    {
        string jsonWithNullNestedProperties = """
        {
            "saveVersion": "1.0.0",
            "level1State": {
                "questFlags": null,
                "inventoryItems": null,
                "puzzle3ObjectPositions": null
            },
            "collectionAlbum": {
                "collectedJournalPages": null,
                "collectedStamps": null,
                "unlockedFactCards": null
            }
        }
        """;

        var storage = new CoreInMemorySaveStorage();
        storage.WriteAllText("Data/nested_null.json", jsonWithNullNestedProperties);
        var service = new CoreSaveLoadService(storage, "Data/nested_null.json");

        var loaded = service.Load();
        Assert.NotNull(loaded);

        // Verification: Level1State.QuestFlags and root QuestFlags are restored and non-null
        Assert.NotNull(loaded.Level1State.QuestFlags);
        Assert.NotNull(loaded.QuestFlags);
        Assert.False(loaded.QuestFlags.Q1_Started);

        // Verification: InventoryItems is restored, and operations succeed without throwing
        Assert.NotNull(loaded.Level1State.InventoryItems);
        loaded.Level1State.InventoryItems.Add(CoreItemIds.BanhPho);
        Assert.Single(loaded.Level1State.InventoryItems);

        // Verification: CollectionAlbum collections are restored, and operations succeed without throwing
        Assert.NotNull(loaded.CollectionAlbum.CollectedStamps);
        bool stampAdded = loaded.CollectionAlbum.AddStamp("Stamp_1");
        Assert.True(stampAdded);
        Assert.True(loaded.CollectionAlbum.HasStamp("Stamp_1"));

        Assert.NotNull(loaded.CollectionAlbum.CollectedJournalPages);
        Assert.NotNull(loaded.CollectionAlbum.UnlockedFactCards);
        Assert.NotNull(loaded.Level1State.Puzzle3ObjectPositions);
    }
```

---

## 5. Verification Method

### 5.1 Project Compilation
Execute:
```bash
dotnet build /Users/mac/Projects/game1/GameProject.csproj
dotnet build /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
```
*Expected Result*: Build succeeded with 0 errors.

### 5.2 Remediated Test Verification
Execute the two targeted remediation test cases:
```bash
dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Remediation_" --logger "console;verbosity=normal"
```
*Expected Result*:
- `Remediation_QuestObjective_IntegerOverflow_ClampedSafelyToTargetCount`: Passed (CurrentCount == 5, IsCompleted == true).
- `Remediation_SaveData_Sanitize_RepairsNullNestedProperties_PreventingExceptions`: Passed (Zero NullReferenceException, collections populated).

### 5.3 Full Test Suite Execution
Execute all unit and adversarial test suites:
```bash
dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
```
*Expected Result*: 125/125 passed, 0 failed, duration < 500 ms.

### 5.4 Invalidation Conditions
This remediation plan is invalidated if:
1. `QuestObjective.IncrementProgress(int.MaxValue)` produces any value other than `TargetCount` or results in `CurrentCount < 0`.
2. A save file with null nested properties (`questFlags: null`, `inventoryItems: null`) throws `NullReferenceException` when calling `saveData.QuestFlags`, `saveData.Level1State.InventoryItems.Add(...)`, or `saveData.CollectionAlbum.AddStamp(...)`.
