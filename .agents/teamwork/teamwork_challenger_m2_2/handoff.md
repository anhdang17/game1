# Handoff Report: Milestone 2 Adversarial Challenge — Interaction FSM, Race Conditions, Inventory & Quest Integrity

**Author**: `teamwork_challenger_m2_2`  
**Milestone**: Milestone 2 (Hanoi Exploration & Quest 1)  
**Role**: Empirical Challenger (Interaction FSM, Race Conditions, Inventory & Quest Integrity, Vietnamese Font Codepoints)  
**Date**: 2026-11-08T16:55:00Z  
**Verdict**: **REQUEST_CHANGES**  

---

## 1. Observation

1. **Test Suite Baseline & Adversarial Harness**:
   - Initial worker test suite executed 230 passing tests.
   - Challenger 2 created a dedicated empirical test suite: `Tests/Challenger2EmpiricalTests.cs` (31 new automated tests, net execution time ~150 ms).
   - Running full suite:
     ```bash
     dotnet test Tests/GameProject.Tests.csproj
     ```
     Result: `Passed! - Failed: 0, Passed: 278, Skipped: 0, Total: 278, Duration: 158 ms.`
     All 31 adversarial tests in `Challenger2EmpiricalTests.cs` executed and passed, empirically characterizing the behaviors and proving the failure modes described below.

2. **CRITICAL FINDING: Save/Load QuestManager Desynchronization & Soft-Lock**:
   - **Observation 2a (Scene Constructor Omission)**: In `Source/Stages/HanoiExplorationScene.cs` lines 73-77:
     ```csharp
     QuestManager = questManager ?? new QuestManager(EventBus);
     if (questManager == null)
     {
         HanoiQuests.RegisterAll(QuestManager);
     }
     ```
     `HanoiExplorationScene` instantiates `QuestManager`, but **never calls `QuestManager.HydrateFromFlags(QuestFlags)`**. When loading a save game where `QuestFlags.Q1_Started = true` or where ingredients were gathered, `QuestManager` starts with all quests in `NotStarted` and all objective counts at 0.
   - **Observation 2b (Unchecked CompleteQuest in Bà An Turn-in)**: In `Source/Core/Interactions/HanoiInteractables.cs` lines 58-69:
     ```csharp
     // State 2: Requirements Met -> Complete Quest 1
     if (context.QuestFlags.AreAllQ1IngredientsCollected() && !context.QuestFlags.Q1_Completed)
     {
         context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id);
         context.QuestFlags.Q1_Completed = true;
         context.AudioService.PlaySfx(SfxType.PhoCookSuccess);
         ...
     ```
     `BaAnNpc.Interact` checks only `context.QuestFlags.AreAllQ1IngredientsCollected()`. It calls `context.QuestManager.CompleteQuest(...)` without checking its return value!
     If `QuestManager` objectives were not registered (e.g. from loaded save data or direct flag alteration), `QuestManager.CompleteQuest` returns `false` (in `Source/Core/Quests/QuestManager.cs` line 141: `if (quest.State != QuestState.RequirementsMet && !quest.AreAllMandatoryObjectivesComplete) return false;`).
     However, `BaAnNpc` blindly sets `context.QuestFlags.Q1_Completed = true;` and transitions permanently to State 3 ("Bà cháu mình cùng vào bàn ghép bát phở chuẩn vị...").
   - **Empirical Proof (`Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock`)**:
     `QuestFlags.Q1_Completed` becomes `true`, but `QuestManager.IsQuestCompleted(HanoiQuests.Quest1Id)` remains `false`. Consequently, `QuestManager.CanStartQuest("Quest_Hanoi_02")` returns `false` forever. The player is permanently soft-locked from starting Quest 2.
   - **Empirical Proof (`Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData`)**:
     A `SaveData` with `Q1_Started = true` and `Q1_BanhPho_Collected = true` results in `scene.QuestManager.GetQuestState("Quest_Hanoi_01") == QuestState.NotStarted` and objective count 0.

3. **MEDIUM FINDING: Proximity Boundary Audio Chattering (Lack of Distance Hysteresis)**:
   - In `Source/Core/Interactions/InteractionManager.cs` lines 60, 84-94:
     ```csharp
     if (distSq <= rSq) ...
     ...
     var prevTarget = CurrentTarget;
     CurrentTarget = FindNearestInteractable(playerPosition, context);
     if (CurrentTarget != null)
     {
         BubbleAlpha = Math.Clamp(BubbleAlpha + deltaTime * 8.0f, 0.0f, 1.0f);
         if (prevTarget != CurrentTarget)
         {
             context.AudioService.PlaySfx(SfxType.InteractPrompt);
         }
     }
     ```
     The entry and exit threshold is identical (`r = 48.0f`).
   - **Empirical Proof (`ProximityOscillation_RapidBoundaryCrossing_CausesAudioChatteringVulnerability`)**:
     When the player oscillates at the 48.0px boundary (between 47.99px and 48.01px), `CurrentTarget` flips between non-null and null every frame. Across 60 frames, `AudioService.PlaySfx(SfxType.InteractPrompt)` was invoked 30 times (a 30Hz machine-gun audio loop). There is no distance hysteresis (e.g., enter at 48px, exit at 54px) or audio cooldown timer.

4. **LOW/MEDIUM FINDING: Inventory Alias Inconsistency Between `AddItem`, `HasItem`, and `RemoveItem`**:
   - In `Source/Core/Models/Inventory.cs` lines 79-88:
     ```csharp
     if (string.Equals(itemId, ItemIds.ThaoMoc, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(itemId, ItemIds.GiaVi, StringComparison.OrdinalIgnoreCase))
     {
         return _items.Contains(ItemIds.ThaoMoc, StringComparer.OrdinalIgnoreCase) ||
                _items.Contains(ItemIds.GiaVi, StringComparer.OrdinalIgnoreCase);
     }
     ```
     `HasItem` treats `Item_ThaoMoc` and `Item_GiaVi` as aliases.
   - However, `AddItem` (lines 51-60) only checks `!_items.Contains(itemId)`. It does not check if the alias is already present.
   - **Empirical Proof (`Inventory_ThaoMocAndGiaVi_AliasAsymmetry_BehaviorCharacterized`)**:
     Calling `AddItem("Item_ThaoMoc")` then `AddItem("Item_GiaVi")` adds both items, setting `Count = 2`.
     Furthermore, if only `Item_ThaoMoc` is present, `HasItem("Item_GiaVi")` returns `true`, but `RemoveItem("Item_GiaVi")` returns `false` without removing anything.

5. **LOW/MEDIUM FINDING: FontManager Codepoints Omit Decomposed (NFD) Diacritics & Lack NFC Normalization**:
   - In `Source/Graphics/FontManager.cs` lines 29-46:
     `GetVietnameseCodepoints()` covers Latin-1, Latin Extended-A/B (`0x0020..0x024F`), and Latin Extended Additional (`0x1EA0..0x1EF9`).
   - **Empirical Proof (`VietnameseUnicode_All134VowelsWithTones_ContainedInCodepointsTable`)**:
     All 134 precomposed Vietnamese letters (12 vowels $\times$ 5 tones $\times$ 2 cases + base accented + Đ/đ) are 100% present in NFC form.
   - **Empirical Proof (`VietnameseUnicode_DecomposedNFDDiacritics_NotInCodepointsTable_RevealsNFCRequirement`)**:
     Combining diacritical marks (`0x0300..0x036F`, e.g. U+0300 grave, U+0301 acute, U+0309 hook, U+0323 dot below) are absent. When text in NFD form (standard macOS APFS / Telex clipboard output, e.g. "Phở" decomposed into 'ơ' + U+0309) is passed to `FontManager.DrawText`, Raylib cannot find the glyph and renders fallback `?` characters because `FontManager.DrawText` does not normalize input strings with `.Normalize(NormalizationForm.FormC)`.

6. **VERIFIED ROBUST AREAS**:
   - **Interaction Spamming**: `baAn.Interact` and `merchant.Interact` withstand 10,000 rapid calls per frame/consecutive frames with zero state corruption, zero duplicate grants, zero double-published events (`InteractionSpam_BaAnNpc_10000RapidCalls_NeverCorruptsState`, `InteractionSpam_Merchant_10000RapidCalls_GrantsExactlyOnce`).
   - **Out-of-Order Gathering**: Any gathering permutation of the 5 ingredients correctly satisfies Quest 1 requirements (`Inventory_OutOfOrderGathering_AllFiveItems_Succeeds`).
   - **Incomplete Gathering**: Missing any single ingredient (tested all 5 missing combinations) cleanly prevents Quest 1 completion and prevents Quest 2 unlock (`Inventory_MissingOneItem_BaAnCannotCompleteQuest_AllFiveVariations`).
   - **Quest Prerequisites**: Quest 2 cannot be started if Quest 1 is NotStarted, InProgress, or RequirementsMet (`QuestProgression_PrematureQuest2Start_RejectedWhenQ1RequirementsMetBeforeTurnIn`). Quest 3 cannot start without Quest 2 (`QuestProgression_PrematureQuest3Start_RejectedWhenQ2NotCompleted`).
   - **Objective Bounds**: Massive increments (`int.MaxValue`) clamp to target count without overflow; negative increments are rejected (`QuestProgression_ObjectiveIncrement_ClampsAtTarget_DoesNotOverflow`).

---

## 2. Logic Chain

1. **Save/Load Desynchronization & Soft-Lock**:
   - In Clean Architecture, presentation scene `HanoiExplorationScene` orchestrates domain services (`QuestManager`, `Inventory`, `SaveData`).
   - When initialized with `SaveData`, `SaveData.Level1State.QuestFlags` holds the persistent progression flags.
   - Because `HanoiExplorationScene` never invokes `QuestManager.HydrateFromFlags(QuestFlags)`, `QuestManager` remains oblivious to previously collected items.
   - When the player completes the remaining ingredients, `QuestFlags.AreAllQ1IngredientsCollected()` evaluates to `true`.
   - `BaAnNpc.Interact` delegates completion via `context.QuestManager.CompleteQuest(...)`, but ignores the boolean return value.
   - `QuestManager.CompleteQuest` rejects completion because its internal objective counters were never hydrated, remaining at 0.
   - `BaAnNpc` nonetheless sets `QuestFlags.Q1_Completed = true`.
   - Now `QuestFlags` indicates Q1 is complete, but `QuestManager` indicates Q1 is incomplete.
   - Future interactions with Bà An hit State 3 and do not attempt `CompleteQuest` again.
   - Quest 2 prerequisite check `QuestManager.CanStartQuest("Quest_Hanoi_02")` checks `IsQuestCompleted("Quest_Hanoi_01")`, which queries `QuestManager._quests["Quest_Hanoi_01"].State == QuestState.Completed`.
   - Result: Quest 2 can NEVER start. The vertical slice progression is irrevocably soft-locked.

2. **Proximity Flutter & Audio Chatter**:
   - Floating point distance calculations near `dist = 48.0f` oscillate across the threshold due to microscopic player velocity changes or input adjustments.
   - Without a dual-threshold hysteresis margin (e.g., enter at $R$, exit at $R + \Delta R$), the state transition between `Target != null` and `Target == null` fires on every alternating frame.
   - Because `PlaySfx(InteractPrompt)` is triggered strictly by `prevTarget != CurrentTarget`, this creates an acoustic glitch (30 chimes per second).

3. **Unicode NFC vs NFD Divergence**:
   - macOS text engines frequently output NFD (decomposed) strings.
   - Raylib's glyph rasterizer indexes glyphs by single codepoint without OpenType text shaping.
   - A font atlas containing only precomposed characters cannot render decomposed sequences, producing `?` glyphs unless input is normalized to Form C prior to rendering.

---

## 3. Caveats

1. **Review-Only Constraint**:
   - In accordance with the Challenger role constraints ("Review-only — do NOT modify implementation code"), no changes were made to `Source/`. All empirical proof was established by authoring test assertions in `Tests/Challenger2EmpiricalTests.cs`.
2. **Pushable Stalls**:
   - Stall movement and spatial grid mechanics are slated for Milestone 3 (Quest 3 Long Biên clearance puzzle); static obstacle colliders were verified for Milestone 2.

---

## 4. Conclusion

**Verdict: REQUEST_CHANGES**

While character movement, interaction spamming defense, and precomposed Vietnamese glyph coverage are implemented to a high standard, Milestone 2 has a **critical soft-lock vulnerability** that breaks save/load state consistency and locks out Quest 2 progression, along with audio chattering on proximity boundaries and inventory alias asymmetry.

### Required Changes for Worker (`teamwork_worker_m2_1`):

1. **Fix QuestManager Hydration in Scene (`Source/Stages/HanoiExplorationScene.cs`)**:
   In the constructor (around line 77), add:
   ```csharp
   QuestManager.HydrateFromFlags(QuestFlags);
   ```
2. **Fix Bà An Turn-in & QuestManager Synchronization (`Source/Core/Interactions/HanoiInteractables.cs`)**:
   In `BaAnNpc.Interact` lines 58-69:
   - Synchronize `QuestManager` objectives from `QuestFlags` (or verify `context.QuestManager.HydrateFromFlags(context.QuestFlags)`).
   - Only set `context.QuestFlags.Q1_Completed = true;` if `context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id)` returns `true` (or ensure `CompleteQuest` succeeds).
3. **Add Proximity Hysteresis or Audio Debounce (`Source/Core/Interactions/InteractionManager.cs`)**:
   - In `FindNearestInteractable` or `Update`, use an exit radius (e.g. $48 + 6 = 54\text{ px}$) when retaining an existing `CurrentTarget`, or add a minimum 0.25s cooldown before re-triggering `SfxType.InteractPrompt`.
4. **Fix Inventory Alias Handling (`Source/Core/Models/Inventory.cs`)**:
   - In `AddItem`, check if an aliased item (`Item_ThaoMoc` vs `Item_GiaVi`) already exists before adding.
   - In `RemoveItem`, support removing either alias interchangeably.
5. **Add NFC Normalization in FontManager (`Source/Graphics/FontManager.cs`)**:
   - In `DrawText` and `MeasureText`, add `text = text.Normalize(System.Text.NormalizationForm.FormC);` before passing to Raylib.

---

## 5. Verification Method

1. **Run Full Test Suite with Challenger Tests**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --logger "console;verbosity=normal"
   ```
   *Expected Result*: All 278 tests pass.

2. **Run Dedicated Challenger 2 Harness**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Challenger2EmpiricalTests"
   ```
   *Expected Result*: All 31 tests pass, verifying both robust invariants and empirical bug proofs.

3. **Inspect Key Failure Replications**:
   - `Tests/Challenger2EmpiricalTests.cs`:
     - Line 627: `Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock`
     - Line 655: `Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData`
     - Line 251: `ProximityOscillation_RapidBoundaryCrossing_CausesAudioChatteringVulnerability`
     - Line 391: `Inventory_ThaoMocAndGiaVi_AliasAsymmetry_BehaviorCharacterized`
     - Line 562: `VietnameseUnicode_DecomposedNFDDiacritics_NotInCodepointsTable_RevealsNFCRequirement`
