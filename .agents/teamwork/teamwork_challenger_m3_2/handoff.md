# Milestone 3 Challenger 2 Handoff Report: Progression, Persistence & Cutscene Stress Testing

**Author**: `teamwork_challenger_m3_2`  
**Milestone**: Milestone 3 (Hanoi Quests 2 & 3 & Progression Climax)  
**Role**: Empirical Challenger (Progression Climax, Persistence Durability, Gate Interaction & Cutscene Dynamics)  
**Date**: 2026-11-09T01:27:00Z  
**Verdict**: **REQUEST_CHANGES**  

---

## 1. Observation

### 1.1 Test Suite Baseline and Suite Execution
- Base test command executed:
  `dotnet test Tests/GameProject.Tests.csproj`
  Output: `Passed! - Failed: 0, Passed: 391, Skipped: 0, Total: 391, Duration: 209 ms - GameProject.Tests.dll (net10.0)`.
- Authored 28 empirical stress tests in `Tests/ChallengerM3ProgressionStressTests.cs` covering:
  - Repeated invocations & multithreading of `LevelProgressionService.CompleteLevel1()`.
  - Persistence atomic overwrite on real disk with `.tmp` and `.bak` rotation.
  - Resilience across 6 corrupted JSON formats and clean fallback quarantine.
  - Full cross-instance rehydration preserving Vietnamese UTF-8 diacritics.
  - Long Biên Gate pre-Q3, mid-Q3, and post-Q3 interaction prompt and collision toggling.
  - `GrayShadowLetterModal` typewriter pacing, two-step dismiss, key spamming, and extreme delta time bounds.
  - End-to-end climax-to-gate transition lifecycle.

### 1.2 Progression Climax & Idempotency (`Source/Core/Quests/LevelProgressionService.cs`)
- `CompleteLevel1()` was invoked 50 times sequentially and 20 times concurrently (`CompleteLevel1_RepeatedCalls_PreventAlbumDuplication` & `CompleteLevel1_ConcurrentInvocations_MaintainsDataIntegrity`):
  - `CollectionAlbum.CollectedJournalPages` contained exactly 1 entry (`AlbumIds.HanoiJournalPage`).
  - `CollectionAlbum.CollectedStamps` contained exactly 1 entry (`AlbumIds.HanoiLotusStamp`).
  - `WorldProgression.GetColorRestoration(RegionIds.Hanoi)` stayed strictly at 100.0f.
  - `Gate_LongBien_Unlocked`, `Level1_Finished`, and `RegionHue` unlock flags remained true.
- Event publication observation:
  `CompleteLevel1()` unconditionally emits 8 events (`RegionColorUpdatedEvent`, `ColorRestoredEvent`, `RegionCompletedEvent`, `GateUnlockedEvent`, `JournalPageAwardedEvent`, `StampAwardedEvent`, `RegionUnlockedEvent`, `CutsceneTriggeredEvent`) on each call. Calling it $N$ times publishes $N$ batches of events. This behavior directly satisfies the regression contract in `Tests/AdversarialCoreTests.cs` line 617 (`Assert.Equal(3, gateUnlockEvents);`).

### 1.3 Persistence Durability & Rehydration (`Source/Core/Persistence/SaveLoadService.cs`)
- Real disk filesystem testing (`Persistence_AtomicSaveWithRealDisk_ProducesBackupAndCleanState`):
  - Initial save creates `savegame.json`.
  - Second save atomically writes to `savegame.json.tmp`, backs up old state to `savegame.json.bak`, and replaces `savegame.json` via POSIX atomic rename. No orphaned `.tmp` files remained.
- Corruption quarantine testing (`Persistence_CorruptedSaveFile_RecoversToCleanDefaultAndQuarantines`):
  - 6 corrupted formats tested: empty file, whitespace only, JSON `null` literal, truncated JSON `{"SaveVersion": "1.0.0", "PlayerPro`, garbage payload `<<<NOT_VALID_JSON_AT_ALL>>>`, and schema mismatch `{"Level1State": "invalid_type"}`.
  - `SaveLoadService.Load()` caught all format corruptions without unhandled exceptions, created `.corrupt` backups, restored healthy `SaveData.CreateDefault()`, auto-saved clean state, and published `GameLoadedEvent` with `WasCorruptedAndRecovered: true`.
- Cross-instance re-hydration (`Persistence_CrossInstanceRehydration_PreservesVietnameseDiacriticsAndClimaxState`):
  - Completely isolated `SaveLoadService` instance reloaded state with 100% fidelity: player name `"Bé Mây Cầu Long Biên 1000 Năm Văn Hiến"`, journal page title `"Hương Vị Phố Cổ & Nhịp Cầu Thời Gian"`, and stamp postmark `"HÀ NỘI - 1000 NĂM VĂN HIẾN"`.

### 1.4 Long Biên Gate Interaction States (`Source/Core/Interactions/HanoiInteractables.cs`)
- Three distinct state prompts and interaction results verified:
  1. Pre-Q3 (Quest 1/2 incomplete): `"[E] Cổng Cầu Long Biên (Bị chặn bởi sạp hàng)"`. Dialogue directs player to Bà An and Phở.
  2. Mid-Q3 (Quest 2 solved, Quest 3 stall puzzle pending): `"[E] Dọn dẹp sạp hàng mở lối"`. Dialogue prompts stall clearance; dismissing dialogue pushes `GameState.MiniGameStalls`.
  3. Post-Q3 (Gate unlocked): `"[E] Đi qua Cầu Long Biên (Bản Đồ Thế Giới)"`. Dialogue celebrates passage to Huế.
- `CollisionWorld` obstacle registered at `(1480, 400)` with extents `(30, 300)` when gate is locked, and dynamically unblocked when `Gate_LongBien_Unlocked == true`.

### 1.5 GrayShadowLetterModal Typewriter & Skip Dynamics (`Source/UI/GrayShadowLetterModal.cs`)
- Visual typewriter text typing progresses at 35 characters/sec (`VisibleCharCount => Math.Clamp((int)(TypewriterProgress * 35.0f), 0, LetterText.Length)`).
- Skip/Confirm behavior:
  - First press of Skip/Confirm while typing immediately sets `TypewriterProgress = LetterText.Length / 35.0f`, revealing full text in 1 frame. The modal remains active (`IsActive == true`, `RequestClose == false`) so the player can read the content.
  - Second press of Confirm cleanly triggers `Dismiss()` (`IsActive = false`, `RequestClose = true`).
- Clamping and boundary safety:
  - Tested extreme delta times: `-100.0f`, `-0.001f`, `0.0f`, `100.0f`, `1,000,000.0f`. `VisibleCharCount` stayed strictly within $[0, 423]$. Zero `ArgumentOutOfRangeException` thrown across 500 rapid randomized input pulses.
  - Text layout: 10 lines of text fit within the 840x540 parchment body (max line length 95 characters), leaving 147px vertical clearance before the footer button.

### 1.6 CRITICAL DEFECT: Missing `GrayShadow_LetterRead` Flag in `StallPuzzleScene.cs`
- **Location**: `Source/Stages/StallPuzzleScene.cs` lines 97-105:
  ```csharp
  if (_letterModal.IsActive)
  {
      _letterModal.Update(dt);
      if (_letterModal.RequestClose)
      {
          RequestNextScene = true;
      }
      return;
  }
  ```
- **Defect Observation**:
  1. When the stall puzzle is solved, `_letterModal` is activated at 3.0s.
  2. When the player finishes and dismisses `_letterModal`, `StallPuzzleScene` sets `RequestNextScene = true`, but **never sets `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true`**, nor does it publish `CutsceneEndedEvent` or `LetterOpenedEvent`.
  3. In contrast, `HanoiExplorationScene.cs` (lines 241-247) sets `SaveData.Level1State.QuestFlags.GrayShadow_LetterRead = true` when its own modal closes.
  4. In `HanoiExplorationScene.cs` lines 287-298:
     ```csharp
     else if (QuestFlags.Gate_LongBien_Unlocked)
     {
         if (!QuestFlags.GrayShadow_LetterRead)
         {
             _grayShadowLetterModal.Show();
             StateManager.PushState(GameState.Cutscene);
         }
         else
         {
             RequestWorldMap = true;
         }
     }
     ```
  5. Because `StallPuzzleScene` left `GrayShadow_LetterRead == false`, when the player walks over to the unlocked Long Biên Gate and interacts to enter the World Map, the game evaluates `if (!QuestFlags.GrayShadow_LetterRead)` (which is still false) and **forces the player into a duplicate `GameState.Cutscene` showing the exact same letter a second time** before `RequestWorldMap` can ever become true.
- **Empirical Test Confirmation**:
  `Tests/ChallengerM3ProgressionStressTests.cs::StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap` passed and confirmed this exact defect.

---

## 2. Logic Chain

1. **Idempotency Verification**:
   - Observation 1.2 proves that `CollectionAlbum` internally uses `Contains(..., StringComparer.OrdinalIgnoreCase)` guards before adding entries. Calling `CompleteLevel1()` repeatedly maintains a single copy of `HanoiJournalPage` and `HanoiLotusStamp`.
   - Event publication is not deduplicated, but this is explicitly tested and mandated by `AdversarialCoreTests.cs:617`. Scene subscribers do not retain leaked state from these events.

2. **Persistence Integrity Verification**:
   - Observation 1.3 proves that atomic replace (`savegame.json.tmp` -> `savegame.json` + `savegame.json.bak`) works seamlessly on real POSIX disk storage.
   - All 6 corruption attack vectors are safely quarantined to `.corrupt` files, preventing crash-on-load scenarios. Default clean state is loaded, auto-saved, and signaled via `GameLoadedEvent(WasCorruptedAndRecovered: true)`.

3. **Cutscene Typewriter & Input Dynamics**:
   - Observation 1.5 proves that `GrayShadowLetterModal`'s two-step confirmation pattern (`!IsTypingComplete` -> fast-forward; `IsTypingComplete` -> dismiss) provides clean UX protection against accidental cutscene skips while allowing impatient players to bypass typewriter delays.
   - `Math.Clamp` bounds check ensures zero substring buffer overruns.

4. **Root Cause of Duplicate Cutscene Trap**:
   - Observation 1.6 reveals a split ownership of `GrayShadowLetterModal`: an internal instance inside `StallPuzzleScene` and another instance inside `HanoiExplorationScene`.
   - `StallPuzzleScene.cs` line 100 handles modal close by setting `RequestNextScene = true`, but neglects to synchronize `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true`.
   - When control returns to `HanoiExplorationScene`, interacting with the gate checks `QuestFlags.GrayShadow_LetterRead`. Because this flag was never updated, `HanoiExplorationScene` assumes the player has never seen the cutscene, pushing `GameState.Cutscene` and displaying the letter again.
   - This defect is easily remediated by adding `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;` to `StallPuzzleScene.cs` line 101.

---

## 3. Caveats

- **No Caveats**: All 4 dispatch mandate areas plus end-to-end transition flows were investigated and empirically characterized with 28 automated tests.
- GPU shader rendering was verified headlessly with mocked presentation ports; display rendering relies on Raylib-cs tested in interactive builds.

---

## 4. Conclusion

- **Verdict**: **REQUEST_CHANGES**
- **Required Remediation**:
  In `Source/Stages/StallPuzzleScene.cs` line 101, update the modal close block to synchronize the quest flag and publish cutscene events:
  ```csharp
  if (_letterModal.IsActive)
  {
      _letterModal.Update(dt);
      if (_letterModal.RequestClose)
      {
          _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
          _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
          _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
          RequestNextScene = true;
      }
      return;
  }
  ```
  This eliminates the duplicate cutscene trap, guarantees that the player only sees the Bóng Xám letter once upon completing Quest 3, and enables immediate transition from the Long Biên Gate to the World Map (`RequestWorldMap = true`).

---

## 5. Verification Method

To independently verify the test suite and empirical findings:

```bash
# 1. Run all 391 tests in test suite
dotnet test Tests/GameProject.Tests.csproj

# 2. Specifically run Challenger 2's empirical stress test suite (28 tests)
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~ChallengerM3ProgressionStressTests"

# 3. Specifically verify the empirical test demonstrating the duplicate cutscene trap
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap"
```

Invalidation Conditions:
- If `dotnet test` fails any test or throws unhandled `ArgumentOutOfRangeException` / `NullReferenceException`.
- If `CompleteLevel1()` produces duplicate entries in `CollectionAlbum`.
- If `SaveLoadService.Load()` crashes on corrupted JSON files.
- If `StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap` fails to prove that `GrayShadow_LetterRead` is left `false` by `StallPuzzleScene`.
