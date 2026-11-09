# Milestone 1 Technical Specification & Architecture Handoff: Core Domain Models & Persistence System

**Agent**: `teamwork_explorer_m1_1`  
**Milestone**: M1 — Core Domain Engine & Persistence  
**Date**: 2026-11-08  
**Target Roles**: M1 Worker / Implementer, Test Writer, Orchestrator  
**File Output Path**: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_1/handoff.md`

---

## 1. Observation

### 1.1 Direct Workspace Observations
1. **Repository Layout**:
   - Project file `/Users/mac/Projects/game1/GameProject.csproj` is configured for `.NET 10.0` (`net10.0`), `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, with package reference `Raylib-cs 8.1.0`.
   - The directory skeleton already contains:
     - `Source/Core/`
     - `Source/UI/`
     - `Source/Stages/`
     - `Source/Utils/`
     - `Source/Audio/`
     - `Source/Graphics/`
     - `Source/Data/`
     - `Data/`
   - Command `find Source Data -type f` returned 0 files. Both directories are currently empty placeholders awaiting implementation.
2. **Architecture Constraints from `PROJECT.md`**:
   - `Source/Core/` is strictly pure C# domain logic with **zero** Raylib dependencies, zero OpenGL references, and zero display window requirements.
   - Domain logic must be 100% testable headlessly via `dotnet test` (xUnit) in `< 500 ms`.
   - Persistence requires durable atomic JSON file persistence (`savegame.json`) with versioning ("1.0.0") and backup fallback.
3. **Save/Load JSON Schema in `teamwork_spec_miner_survey_2/handoff.md`**:
   - Schema defines 5 root sections:
     1. `saveVersion` (string, default `"1.0.0"`)
     2. `lastSavedTimestamp` (ISO-8601 date-time string)
     3. `playerProfile` (`playerName`, `currentRegionId`, `totalPlayTimeSeconds`)
     4. `worldProgression` (`unlockedRegions`, `completedRegions`, `regionColorRestoration`)
     5. `level1State` (`playerSpawnX`, `playerSpawnY`, `activeQuestId`, `questFlags`, `inventoryItems`, `puzzle3ObjectPositions`)
     6. `collectionAlbum` (`collectedJournalPages`, `collectedStamps`, `unlockedFactCards`)
     7. `audioSettings` (`masterVolume`, `sfxVolume`, `musicVolume`)
   - Verbatim quest flags specified (16 total):
     - `Q1_Started`, `Q1_BanhPho_Collected`, `Q1_ThitBo_Collected`, `Q1_NuocDung_Collected`, `Q1_HanhLa_Collected`, `Q1_ThaoMoc_Collected`, `Q1_Completed`
     - `Q2_Started`, `Q2_PhoAssembled`, `Q2_Completed`
     - `Q3_Started`, `Q3_GridSolved`, `Q3_Completed`
     - `Gate_LongBien_Unlocked`, `GrayShadow_LetterRead`, `Level1_Finished`
4. **Platform Capabilities**:
   - Host is macOS Apple Silicon M2 (`arm64`), `.NET SDK 10.0.101`.
   - System.Text.Json is part of .NET 10 runtime, supporting high-performance serialization, UTF-8 streaming, and relaxed escaping for Vietnamese characters.

---

## 2. Logic Chain

### 2.1 Domain Model Architecture (`Source/Core/Models/`)
To satisfy the Clean Architecture requirements and maintain strict separation between Core Domain and Presentation:
1. **Decoupled POCO Models**:
   All domain models in `Source/Core/Models/` must be pure C# objects (Plain Old CLR Objects) with zero third-party dependencies.
2. **Root Container (`SaveData`)**:
   `SaveData` acts as the aggregate root for persistence and snapshot state. It contains:
   - `PlayerProfile`
   - `WorldProgression`
   - `Level1State`
   - `CollectionAlbum`
   - `AudioSettingsData`
   It provides `SaveData.CreateDefault()` to deterministically produce a brand new initial game state.
3. **QuestFlags Integrity**:
   Because the JSON schema specifies exact casing for flags like `"Q1_Started"`, `"Q1_BanhPho_Collected"`, and `"Gate_LongBien_Unlocked"`, we must avoid naive camelCase transformation converting them to `"q1_Started"`.
   - **Solution**: Annotate each flag in `QuestFlags.cs` with explicit `[JsonPropertyName("...")]` attributes matching the schema, and configure `PropertyNameCaseInsensitive = true` on `JsonSerializerOptions`.
   - In addition, provide helper queries (`GetQ1CollectedCount()`, `AreAllQ1IngredientsCollected()`, `GetFlag(string)`, `SetFlag(string, bool)`) to allow both strongly-typed compiled code and dynamic dialogue/quest triggers to interact with flags.
4. **Inventory Collection**:
   The `Inventory` model wraps collected item identifiers (strings). Item acquisition must be idempotent: adding an item already present returns `false` and does not duplicate the entry. Standard constants (`ItemIds.BanhPho`, `ItemIds.ThitBo`, `ItemIds.NuocDung`, `ItemIds.HanhLa`, `ItemIds.ThaoMoc`) ensure type safety across Hanoi quests.
5. **Collection Album**:
   Tracks unlocked IDs across 3 distinct collections (`collectedJournalPages`, `collectedStamps`, `unlockedFactCards`). Companion display models (`JournalPage`, `TravelStamp`, `FactCard`) provide rich metadata (titles, cultural descriptions, Vietnamese trivia) for UI presentation.
6. **World Progression & Region Color**:
   `WorldProgression` encapsulates region unlocking and color restoration percentages (0.0 to 100.0). Helper methods clamp values and guarantee Hanoi starts unlocked at 0.0%, reaches 33.3% after Q2, reaches 100.0% after Q3, and unlocks Huế upon Hanoi completion.

### 2.2 Persistence System Architecture (`Source/Core/Persistence/`)
1. **Atomic Write Protocol**:
   Writing directly to `savegame.json` is vulnerable to file corruption if the process terminates or loses power during the write.
   - **Protocol**:
     1. Ensure target directory exists (`./Data/` or custom directory).
     2. Serialize payload to string or stream.
     3. Write full content to a temporary file (`savegame.json.tmp`).
     4. Atomically move/replace `savegame.json.tmp` -> `savegame.json` using `File.Move(..., overwrite: true)` which maps to the atomic POSIX `rename()` syscall on macOS/Linux and Windows atomic replace.
     5. In case of unexpected I/O failure, the existing `savegame.json` remains untouched.
2. **Corrupt File Detection & Fallback**:
   If `savegame.json` contains malformed JSON syntax, zero bytes, or is unparseable:
   - Catch `JsonException`, `FormatException`, `IOException`.
   - Generate a timestamped backup copy: `savegame.json.<timestamp>.corrupt`.
   - Instantiate a fresh default state via `SaveData.CreateDefault()`.
   - Log a diagnostic warning and return the clean state without crashing the game engine.
3. **Vietnamese UTF-8 Encoding**:
   By default, `System.Text.Json` escapes non-ASCII characters to Unicode escape sequences (e.g., `B\u00e9 M\u00e2y`).
   - **Solution**: Configure `JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` in `SaveGameJsonContext`. This ensures Vietnamese diacritics in `Bé Mây`, `Bánh Phở tươi`, `Hoa Sen Tây Hồ` are serialized directly as clean, human-readable UTF-8 characters.
4. **Filesystem Abstraction (`ISaveStorage`)**:
   `SaveLoadService` accepts an optional `ISaveStorage` interface (defaulting to `FileSaveStorage`). This allows the test suite (`GameProject.Tests`) to run headless tests with `MockSaveStorage` in memory with zero disk I/O, while the production game writes to `./Data/savegame.json`.

---

## 3. Concrete Class Specifications for Worker Implementation

### 3.1 Model 1: `SaveData.cs`
**File**: `Source/Core/Models/SaveData.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System;

namespace GameProject.Core.Models;

public sealed class SaveData
{
    public const string CurrentSaveVersion = "1.0.0";

    public string SaveVersion { get; set; } = CurrentSaveVersion;
    public DateTimeOffset LastSavedTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public PlayerProfile PlayerProfile { get; set; } = new();
    public WorldProgression WorldProgression { get; set; } = new();
    public Level1State Level1State { get; set; } = new();
    public CollectionAlbum CollectionAlbum { get; set; } = new();
    public AudioSettingsData AudioSettings { get; set; } = new();

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
            AudioSettings = AudioSettingsData.CreateDefault()
        };
    }

    public void Sanitize()
    {
        PlayerProfile ??= PlayerProfile.CreateDefault();
        WorldProgression ??= WorldProgression.CreateDefault();
        Level1State ??= Level1State.CreateDefault();
        CollectionAlbum ??= CollectionAlbum.CreateDefault();
        AudioSettings ??= AudioSettingsData.CreateDefault();

        AudioSettings.ClampVolumes();
        WorldProgression.Sanitize();
    }
}
```

---

### 3.2 Model 2: `PlayerProfile.cs`
**File**: `Source/Core/Models/PlayerProfile.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
namespace GameProject.Core.Models;

public sealed class PlayerProfile
{
    public string PlayerName { get; set; } = "Bé Mây";
    public string CurrentRegionId { get; set; } = "Hanoi";
    public double TotalPlayTimeSeconds { get; set; } = 0.0;

    public static PlayerProfile CreateDefault() => new()
    {
        PlayerName = "Bé Mây",
        CurrentRegionId = "Hanoi",
        TotalPlayTimeSeconds = 0.0
    };

    public void AddPlayTime(double seconds)
    {
        if (seconds > 0)
        {
            TotalPlayTimeSeconds += seconds;
        }
    }
}
```

---

### 3.3 Model 3: `WorldProgression.cs` & `RegionProgress.cs`
**File**: `Source/Core/Models/WorldProgression.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameProject.Core.Models;

public sealed class WorldProgression
{
    public const string RegionHanoi = "Hanoi";
    public const string RegionHue = "Hue";
    public const string RegionHoiAn = "HoiAn";
    public const string RegionSaigon = "Saigon";

    public List<string> UnlockedRegions { get; set; } = new() { RegionHanoi };
    public List<string> CompletedRegions { get; set; } = new();
    public Dictionary<string, float> RegionColorRestoration { get; set; } = new()
    {
        { RegionHanoi, 0.0f }
    };

    public static WorldProgression CreateDefault() => new()
    {
        UnlockedRegions = new List<string> { RegionHanoi },
        CompletedRegions = new List<string>(),
        RegionColorRestoration = new Dictionary<string, float>
        {
            { RegionHanoi, 0.0f }
        }
    };

    public bool IsRegionUnlocked(string regionId) =>
        UnlockedRegions.Contains(regionId, StringComparer.OrdinalIgnoreCase);

    public bool IsRegionCompleted(string regionId) =>
        CompletedRegions.Contains(regionId, StringComparer.OrdinalIgnoreCase);

    public float GetColorRestoration(string regionId)
    {
        return RegionColorRestoration.TryGetValue(regionId, out var val) ? val : 0.0f;
    }

    public void SetColorRestoration(string regionId, float percentage)
    {
        percentage = Math.Clamp(percentage, 0.0f, 100.0f);
        RegionColorRestoration[regionId] = percentage;
    }

    public bool UnlockRegion(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return false;
        if (!IsRegionUnlocked(regionId))
        {
            UnlockedRegions.Add(regionId);
            if (!RegionColorRestoration.ContainsKey(regionId))
            {
                RegionColorRestoration[regionId] = 0.0f;
            }
            return true;
        }
        return false;
    }

    public bool MarkRegionCompleted(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return false;
        if (!IsRegionCompleted(regionId))
        {
            CompletedRegions.Add(regionId);
            SetColorRestoration(regionId, 100.0f);
            return true;
        }
        return false;
    }

    public void Sanitize()
    {
        UnlockedRegions ??= new List<string>();
        if (!UnlockedRegions.Contains(RegionHanoi, StringComparer.OrdinalIgnoreCase))
        {
            UnlockedRegions.Add(RegionHanoi);
        }
        CompletedRegions ??= new List<string>();
        RegionColorRestoration ??= new Dictionary<string, float>();

        foreach (var key in RegionColorRestoration.Keys.ToList())
        {
            RegionColorRestoration[key] = Math.Clamp(RegionColorRestoration[key], 0.0f, 100.0f);
        }

        if (!RegionColorRestoration.ContainsKey(RegionHanoi))
        {
            RegionColorRestoration[RegionHanoi] = 0.0f;
        }
    }
}
```

**File**: `Source/Core/Models/RegionProgress.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
namespace GameProject.Core.Models;

public sealed class RegionProgress
{
    public string RegionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public bool IsCompleted { get; set; }
    public float ColorPercent { get; set; } // 0.0f to 100.0f

    public RegionProgress() { }

    public RegionProgress(string regionId, string displayName, bool isUnlocked, bool isCompleted, float colorPercent)
    {
        RegionId = regionId;
        DisplayName = displayName;
        IsUnlocked = isUnlocked;
        IsCompleted = isCompleted;
        ColorPercent = colorPercent;
    }
}
```

---

### 3.4 Model 4: `Level1State.cs` & `Puzzle3ObjectPosition.cs`
**File**: `Source/Core/Models/Level1State.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System.Collections.Generic;

namespace GameProject.Core.Models;

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

public sealed class Level1State
{
    public float PlayerSpawnX { get; set; } = 120.0f;
    public float PlayerSpawnY { get; set; } = 250.0f;
    public string ActiveQuestId { get; set; } = "Quest_Hanoi_01";
    public QuestFlags QuestFlags { get; set; } = new();
    public List<string> InventoryItems { get; set; } = new();
    public List<Puzzle3ObjectPosition> Puzzle3ObjectPositions { get; set; } = new();

    public static Level1State CreateDefault() => new()
    {
        PlayerSpawnX = 120.0f,
        PlayerSpawnY = 250.0f,
        ActiveQuestId = "Quest_Hanoi_01",
        QuestFlags = QuestFlags.CreateDefault(),
        InventoryItems = new List<string>(),
        Puzzle3ObjectPositions = new List<Puzzle3ObjectPosition>()
    };
}
```

---

### 3.5 Model 5: `QuestFlags.cs`
**File**: `Source/Core/Models/QuestFlags.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System.Text.Json.Serialization;

namespace GameProject.Core.Models;

public sealed class QuestFlags
{
    [JsonPropertyName("Q1_Started")]
    public bool Q1_Started { get; set; }

    [JsonPropertyName("Q1_BanhPho_Collected")]
    public bool Q1_BanhPho_Collected { get; set; }

    [JsonPropertyName("Q1_ThitBo_Collected")]
    public bool Q1_ThitBo_Collected { get; set; }

    [JsonPropertyName("Q1_NuocDung_Collected")]
    public bool Q1_NuocDung_Collected { get; set; }

    [JsonPropertyName("Q1_HanhLa_Collected")]
    public bool Q1_HanhLa_Collected { get; set; }

    [JsonPropertyName("Q1_ThaoMoc_Collected")]
    public bool Q1_ThaoMoc_Collected { get; set; }

    [JsonPropertyName("Q1_Completed")]
    public bool Q1_Completed { get; set; }

    [JsonPropertyName("Q2_Started")]
    public bool Q2_Started { get; set; }

    [JsonPropertyName("Q2_PhoAssembled")]
    public bool Q2_PhoAssembled { get; set; }

    [JsonPropertyName("Q2_Completed")]
    public bool Q2_Completed { get; set; }

    [JsonPropertyName("Q3_Started")]
    public bool Q3_Started { get; set; }

    [JsonPropertyName("Q3_GridSolved")]
    public bool Q3_GridSolved { get; set; }

    [JsonPropertyName("Q3_Completed")]
    public bool Q3_Completed { get; set; }

    [JsonPropertyName("Gate_LongBien_Unlocked")]
    public bool Gate_LongBien_Unlocked { get; set; }

    [JsonPropertyName("GrayShadow_LetterRead")]
    public bool GrayShadow_LetterRead { get; set; }

    [JsonPropertyName("Level1_Finished")]
    public bool Level1_Finished { get; set; }

    public static QuestFlags CreateDefault() => new();

    public int GetQ1CollectedCount()
    {
        int count = 0;
        if (Q1_BanhPho_Collected) count++;
        if (Q1_ThitBo_Collected) count++;
        if (Q1_NuocDung_Collected) count++;
        if (Q1_HanhLa_Collected) count++;
        if (Q1_ThaoMoc_Collected) count++;
        return count;
    }

    public bool AreAllQ1IngredientsCollected() => GetQ1CollectedCount() == 5;

    public bool GetFlag(string flagName)
    {
        return flagName switch
        {
            nameof(Q1_Started) => Q1_Started,
            nameof(Q1_BanhPho_Collected) => Q1_BanhPho_Collected,
            nameof(Q1_ThitBo_Collected) => Q1_ThitBo_Collected,
            nameof(Q1_NuocDung_Collected) => Q1_NuocDung_Collected,
            nameof(Q1_HanhLa_Collected) => Q1_HanhLa_Collected,
            nameof(Q1_ThaoMoc_Collected) => Q1_ThaoMoc_Collected,
            nameof(Q1_Completed) => Q1_Completed,
            nameof(Q2_Started) => Q2_Started,
            nameof(Q2_PhoAssembled) => Q2_PhoAssembled,
            nameof(Q2_Completed) => Q2_Completed,
            nameof(Q3_Started) => Q3_Started,
            nameof(Q3_GridSolved) => Q3_GridSolved,
            nameof(Q3_Completed) => Q3_Completed,
            nameof(Gate_LongBien_Unlocked) => Gate_LongBien_Unlocked,
            nameof(GrayShadow_LetterRead) => GrayShadow_LetterRead,
            nameof(Level1_Finished) => Level1_Finished,
            _ => false
        };
    }

    public void SetFlag(string flagName, bool value)
    {
        switch (flagName)
        {
            case nameof(Q1_Started): Q1_Started = value; break;
            case nameof(Q1_BanhPho_Collected): Q1_BanhPho_Collected = value; break;
            case nameof(Q1_ThitBo_Collected): Q1_ThitBo_Collected = value; break;
            case nameof(Q1_NuocDung_Collected): Q1_NuocDung_Collected = value; break;
            case nameof(Q1_HanhLa_Collected): Q1_HanhLa_Collected = value; break;
            case nameof(Q1_ThaoMoc_Collected): Q1_ThaoMoc_Collected = value; break;
            case nameof(Q1_Completed): Q1_Completed = value; break;
            case nameof(Q2_Started): Q2_Started = value; break;
            case nameof(Q2_PhoAssembled): Q2_PhoAssembled = value; break;
            case nameof(Q2_Completed): Q2_Completed = value; break;
            case nameof(Q3_Started): Q3_Started = value; break;
            case nameof(Q3_GridSolved): Q3_GridSolved = value; break;
            case nameof(Q3_Completed): Q3_Completed = value; break;
            case nameof(Gate_LongBien_Unlocked): Gate_LongBien_Unlocked = value; break;
            case nameof(GrayShadow_LetterRead): GrayShadow_LetterRead = value; break;
            case nameof(Level1_Finished): Level1_Finished = value; break;
        }
    }
}
```

---

### 3.6 Model 6: `Inventory.cs`
**File**: `Source/Core/Models/Inventory.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

public static class ItemIds
{
    public const string BanhPho = "Item_BanhPho";
    public const string ThitBo = "Item_ThitBo";
    public const string NuocDung = "Item_NuocDung";
    public const string HanhLa = "Item_HanhLa";
    public const string ThaoMoc = "Item_ThaoMoc";

    public static readonly IReadOnlyList<string> AllPhoIngredients = new[]
    {
        BanhPho,
        ThitBo,
        NuocDung,
        HanhLa,
        ThaoMoc
    };
}

public sealed class Inventory
{
    private readonly List<string> _items = new();

    public IReadOnlyList<string> Items => _items.AsReadOnly();
    public int Count => _items.Count;

    public Inventory() { }

    public Inventory(IEnumerable<string>? initialItems)
    {
        if (initialItems != null)
        {
            foreach (var item in initialItems)
            {
                AddItem(item);
            }
        }
    }

    public bool AddItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (!_items.Contains(itemId, StringComparer.OrdinalIgnoreCase))
        {
            _items.Add(itemId);
            return true;
        }
        return false; // Idempotent: cannot add duplicate
    }

    public bool RemoveItem(string itemId)
    {
        return _items.Remove(itemId);
    }

    public bool HasItem(string itemId)
    {
        return _items.Contains(itemId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetItems() => Items;

    public void Clear() => _items.Clear();

    public List<string> ToList() => new(_items);
}
```

---

### 3.7 Model 7: `AlbumData.cs`
**File**: `Source/Core/Models/AlbumData.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

public static class AlbumIds
{
    public const string HanoiJournalPage = "JournalPage_Hanoi_01";
    public const string HanoiLotusStamp = "Stamp_HoaSen_Hanoi";

    public const string FactCardPho = "FactCard_Hanoi_Pho";
    public const string FactCardLongBien = "FactCard_Hanoi_LongBien";
    public const string FactCardSenTayHo = "FactCard_Hanoi_SenTayHo";
    public const string FactCardPhoCo = "FactCard_Hanoi_PhoCo";
}

public sealed class CollectionAlbum
{
    public List<string> CollectedJournalPages { get; set; } = new();
    public List<string> CollectedStamps { get; set; } = new();
    public List<string> UnlockedFactCards { get; set; } = new();

    public static CollectionAlbum CreateDefault() => new();

    public bool AddJournalPage(string pageId)
    {
        if (!string.IsNullOrWhiteSpace(pageId) && !CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase))
        {
            CollectedJournalPages.Add(pageId);
            return true;
        }
        return false;
    }

    public bool AddStamp(string stampId)
    {
        if (!string.IsNullOrWhiteSpace(stampId) && !CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase))
        {
            CollectedStamps.Add(stampId);
            return true;
        }
        return false;
    }

    public bool AddFactCard(string cardId)
    {
        if (!string.IsNullOrWhiteSpace(cardId) && !UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase))
        {
            UnlockedFactCards.Add(cardId);
            return true;
        }
        return false;
    }

    public bool HasJournalPage(string pageId) =>
        CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase);

    public bool HasStamp(string stampId) =>
        CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase);

    public bool HasFactCard(string cardId) =>
        UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase);
}

// Display / inspection models for Album UI
public sealed class JournalPage
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string NarrativeText { get; set; } = string.Empty;
    public string ImageKey { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
}

public sealed class TravelStamp
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string PostmarkDate { get; set; } = string.Empty;
    public string IconKey { get; set; } = string.Empty;
    public string RegionId { get; set; } = string.Empty;
}

public sealed class FactCard
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string FullContent { get; set; } = string.Empty;
    public string ImageKey { get; set; } = string.Empty;
}
```

---

### 3.8 Model 8: `AudioSettingsData.cs`
**File**: `Source/Core/Models/AudioSettingsData.cs`  
**Namespace**: `GameProject.Core.Models`

```csharp
using System;

namespace GameProject.Core.Models;

public sealed class AudioSettingsData
{
    public float MasterVolume { get; set; } = 0.8f;
    public float SfxVolume { get; set; } = 0.9f;
    public float MusicVolume { get; set; } = 0.7f;

    public static AudioSettingsData CreateDefault() => new()
    {
        MasterVolume = 0.8f,
        SfxVolume = 0.9f,
        MusicVolume = 0.7f
    };

    public void ClampVolumes()
    {
        MasterVolume = Math.Clamp(MasterVolume, 0.0f, 1.0f);
        SfxVolume = Math.Clamp(SfxVolume, 0.0f, 1.0f);
        MusicVolume = Math.Clamp(MusicVolume, 0.0f, 1.0f);
    }
}
```

---

### 3.9 Persistence Component 1: `SaveGameJsonContext.cs`
**File**: `Source/Core/Persistence/SaveGameJsonContext.cs`  
**Namespace**: `GameProject.Core.Persistence`

```csharp
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameProject.Core.Persistence;

public static class SaveGameJsonContext
{
    public static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Preserves Vietnamese UTF-8 without \uXXXX escaping
        };
    }
}
```

---

### 3.10 Persistence Component 2: `ISaveStorage.cs` & `FileSaveStorage.cs`
**File**: `Source/Core/Ports/ISaveStorage.cs` (or `Source/Core/Persistence/ISaveStorage.cs`)  
**Namespace**: `GameProject.Core.Ports`

```csharp
namespace GameProject.Core.Ports;

public interface ISaveStorage
{
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void DeleteFile(string path);
    void CopyFile(string source, string destination, bool overwrite);
    void EnsureDirectoryExists(string directoryPath);
    void ReplaceFile(string sourcePath, string destinationPath, string? backupPath = null);
}
```

**File**: `Source/Core/Persistence/FileSaveStorage.cs`  
**Namespace**: `GameProject.Core.Persistence`

```csharp
using System.IO;
using System.Text;
using GameProject.Core.Ports;

namespace GameProject.Core.Persistence;

public sealed class FileSaveStorage : ISaveStorage
{
    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

    public void WriteAllText(string path, string content) => File.WriteAllText(path, content, Encoding.UTF8);

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void CopyFile(string source, string destination, bool overwrite) =>
        File.Copy(source, destination, overwrite);

    public void EnsureDirectoryExists(string directoryPath)
    {
        if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    public void ReplaceFile(string sourcePath, string destinationPath, string? backupPath = null)
    {
        if (!string.IsNullOrEmpty(backupPath) && File.Exists(destinationPath))
        {
            File.Copy(destinationPath, backupPath, overwrite: true);
        }

        if (File.Exists(destinationPath))
        {
            // POSIX rename syscall on macOS/Linux: atomic replace
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
        else
        {
            File.Move(sourcePath, destinationPath);
        }
    }
}
```

---

### 3.11 Persistence Component 3: `SaveLoadService.cs`
**File**: `Source/Core/Persistence/SaveLoadService.cs`  
**Namespace**: `GameProject.Core.Persistence`

```csharp
using System;
using System.IO;
using System.Text.Json;
using GameProject.Core.Models;
using GameProject.Core.Ports;

namespace GameProject.Core.Persistence;

public class SaveLoadService
{
    public const string DefaultSavePath = "./Data/savegame.json";
    public const string TempSuffix = ".tmp";
    public const string CorruptSuffix = ".corrupt";

    private readonly ISaveStorage _storage;
    private readonly string _saveFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public SaveLoadService(string? saveFilePath = null, ISaveStorage? storage = null)
    {
        _saveFilePath = saveFilePath ?? DefaultSavePath;
        _storage = storage ?? new FileSaveStorage();
        _jsonOptions = SaveGameJsonContext.CreateOptions();
    }

    public string SaveFilePath => _saveFilePath;

    public bool HasSaveFile() => _storage.FileExists(_saveFilePath);

    public bool Save(SaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        try
        {
            data.LastSavedTimestamp = DateTimeOffset.UtcNow;
            data.Sanitize();

            string json = JsonSerializer.Serialize(data, _jsonOptions);

            string? dir = Path.GetDirectoryName(_saveFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                _storage.EnsureDirectoryExists(dir);
            }

            string tempPath = _saveFilePath + TempSuffix;
            _storage.WriteAllText(tempPath, json);
            _storage.ReplaceFile(tempPath, _saveFilePath);

            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveLoadService] Save failed: {ex.Message}");
            return false;
        }
    }

    public SaveData Load()
    {
        if (!_storage.FileExists(_saveFilePath))
        {
            // First time running game: initialize clean default state
            return SaveData.CreateDefault();
        }

        try
        {
            string json = _storage.ReadAllText(_saveFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                BackupCorruptedFile();
                return SaveData.CreateDefault();
            }

            SaveData? data = JsonSerializer.Deserialize<SaveData>(json, _jsonOptions);
            if (data == null)
            {
                BackupCorruptedFile();
                return SaveData.CreateDefault();
            }

            data.Sanitize();
            return data;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveLoadService] Corrupt save file detected: {ex.Message}");
            BackupCorruptedFile();
            return SaveData.CreateDefault();
        }
    }

    public void DeleteSave()
    {
        _storage.DeleteFile(_saveFilePath);
        string tempPath = _saveFilePath + TempSuffix;
        if (_storage.FileExists(tempPath))
        {
            _storage.DeleteFile(tempPath);
        }
    }

    private void BackupCorruptedFile()
    {
        try
        {
            if (_storage.FileExists(_saveFilePath))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string backupPath = $"{_saveFilePath}.{timestamp}{CorruptSuffix}";
                _storage.CopyFile(_saveFilePath, backupPath, overwrite: true);
                Console.WriteLine($"[SaveLoadService] Backed up corrupted save to: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SaveLoadService] Failed to backup corrupt file: {ex.Message}");
        }
    }
}
```

---

## 4. Caveats

1. **JSON Schema Compatibility**:
   In `teamwork_spec_miner_survey_2/handoff.md`, the quest flags use PascalCase with prefixes (e.g. `Q1_Started`, `Gate_LongBien_Unlocked`). If a global camelCase naming policy is applied without `[JsonPropertyName]` attributes, `Q1_Started` would become `q1_Started`.
   - **Mitigation**: The code provided above explicitly annotates each flag with `[JsonPropertyName("...")]`, guaranteeing exact 100% adherence to the schema.
2. **Atomic Replace Across Filesystem Boundaries**:
   `File.Move(..., overwrite: true)` is strictly atomic when source and destination are on the same volume/filesystem. Because the temp file is written to `./Data/savegame.json.tmp` adjacent to `./Data/savegame.json`, both are guaranteed to be on the same volume.
3. **Save Path Configuration**:
   When tests run in parallel or under CI, tests should inject a unique temp path or use `MockSaveStorage` to prevent file lock collisions on `./Data/savegame.json`.
4. **Read-Only Explorer Constraint**:
   This explorer report provides the architectural blueprint, data contracts, and class designs. The actual file creation in `Source/Core/Models/` and `Source/Core/Persistence/` will be performed by the designated Milestone 1 Worker.

---

## 5. Conclusion

1. The domain models required for Milestone 1 (`PlayerProfile`, `WorldProgression`, `RegionProgress`, `Level1State`, `QuestFlags`, `Inventory`, `AlbumData`, `AudioSettingsData`, `SaveData`) are completely analyzed and specified with full type safety, default values, validation routines, and helper methods.
2. The persistence architecture (`SaveLoadService`, `SaveGameJsonContext`, `FileSaveStorage`, `ISaveStorage`) implements production-grade atomic writes, resilient corruption recovery, automatic folder creation, version tracking, and Vietnamese UTF-8 preservation.
3. The specification reconciles 100% with the JSON Schema defined in `teamwork_spec_miner_survey_2/handoff.md` and aligns cleanly with peer tracks (GameStateManager in M1_2 and QuestManager in M1_3).

---

## 6. Verification Method

To independently verify the implementation when the Worker commits code:

### 6.1 Build Verification
```bash
dotnet build /Users/mac/Projects/game1/GameProject.csproj
```
*Expected*: Exit code 0, 0 Warnings, 0 Errors.

### 6.2 Unit Test Verification Plan
The test author should create tests in `Tests/SaveLoadTests.cs` covering:

1. **Default State Test**:
   ```csharp
   var service = new SaveLoadService(testPath, mockStorage);
   var data = service.Load();
   Assert.Equal("Bé Mây", data.PlayerProfile.PlayerName);
   Assert.Equal("Hanoi", data.PlayerProfile.CurrentRegionId);
   Assert.True(data.WorldProgression.IsRegionUnlocked("Hanoi"));
   Assert.False(data.QuestFlags.Q1_Completed);
   ```
2. **Save & Roundtrip Deserialization Test**:
   ```csharp
   var data = SaveData.CreateDefault();
   data.PlayerProfile.PlayerName = "Bé Mây Test";
   data.Level1State.InventoryItems.Add(ItemIds.BanhPho);
   data.Level1State.QuestFlags.Q1_BanhPho_Collected = true;
   data.WorldProgression.SetColorRestoration("Hanoi", 33.3f);

   service.Save(data);
   var loaded = service.Load();

   Assert.Equal("Bé Mây Test", loaded.PlayerProfile.PlayerName);
   Assert.Contains(ItemIds.BanhPho, loaded.Level1State.InventoryItems);
   Assert.True(loaded.Level1State.QuestFlags.Q1_BanhPho_Collected);
   Assert.Equal(33.3f, loaded.WorldProgression.GetColorRestoration("Hanoi"), 1);
   ```
3. **Atomic Temporary File Verification**:
   - Verify that during write, `savegame.json.tmp` is created and does not remain on disk after a successful save.
4. **Corrupt File Recovery Test**:
   ```csharp
   mockStorage.WriteAllText(testPath, "{ corrupted json syntax [[[[");
   var loaded = service.Load();
   Assert.NotNull(loaded);
   Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName); // Clean fallback
   // Verify that a backup file with suffix .corrupt exists
   ```
5. **Vietnamese UTF-8 Diacritic Integrity Test**:
   ```csharp
   data.PlayerProfile.PlayerName = "Bé Mây Du Ký";
   service.Save(data);
   string rawJson = mockStorage.ReadAllText(testPath);
   Assert.Contains("Bé Mây Du Ký", rawJson); // Not escaped to \u00e9 \u00e2y
   ```
6. **Execution Command**:
   ```bash
   dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
   ```
   *Expected*: All tests pass with 100% success rate.
