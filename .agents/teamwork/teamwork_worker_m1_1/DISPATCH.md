## 2026-11-08T15:22:46Z
You are teamwork_worker_m1_1.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m1_1/

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY READS BEFORE STARTING:
1. /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
2. /Users/mac/Projects/game1/PROJECT.md
3. /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_1/handoff.md
4. /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_2/handoff.md
5. /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_3/handoff.md

Your exclusive write ownership:
- Everything inside `/Users/mac/Projects/game1/Source/Core/` and its subdirectories.
- Your state folder: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m1_1/`
DO NOT touch any files outside `Source/Core/` and your state folder.

Your mission for Milestone 1 (Core Domain Engine & Persistence):
Implement the complete, production-grade Pure C# Core Domain logic following the detailed architectural blueprints provided in the 3 Explorer handoff reports:
1. `Source/Core/Models/`:
   - `PlayerProfile.cs`, `WorldProgression.cs`, `Level1State.cs`, `QuestFlags.cs`, `Inventory.cs`, `CollectionAlbum.cs`, `AudioSettings.cs`, `SaveData.cs`.
   - Ensure 100% adherence to the Save/Load JSON schema and quest flags (Q1_Started, Q1_BanhPho_Collected, ..., Q3_Completed, Gate_LongBien_Unlocked, GrayShadow_LetterRead, Level1_Finished).
2. `Source/Core/Persistence/`:
   - `SaveLoadService.cs`: Durable persistence with atomic write pattern (.tmp flush then replace), backup on overwrite (.bak), corrupt JSON isolation (.corrupt) and clean fallback, UTF-8 relaxed escaping for Vietnamese characters.
   - `SaveGameJsonContext.cs`: Source-generated or optimized System.Text.Json configuration.
3. `Source/Core/State/`:
   - `GameState.cs`: Enum defining Title, WorldMap, Exploration, MiniGamePho, MiniGameStalls, Dialogue, Cutscene, Album, Pause.
   - `GameStateManager.cs`: Hybrid state stack machine with ChangeState, PushState, PopState, and modal stacking rules.
4. `Source/Core/Events/`:
   - `EventBus.cs`: Thread-safe decoupled observer event bus with snapshot dispatching and IDisposable subscription tokens.
   - `GameEvents.cs`: Immutable record events for all domain interactions (ItemCollected, QuestStarted, QuestCompleted, GateUnlocked, ColorRestored, DialogueStarted, StateChanged, etc.).
5. `Source/Core/Ports/`:
   - Interfaces: `IInputService.cs`, `IAudioService.cs`, `ISaveStorage.cs`.
   - Implementations: `FileSaveStorage.cs`, `InMemorySaveStorage.cs`, `MockInputService.cs`, `NullAudioService.cs`, `RecordingAudioService.cs`.
6. `Source/Core/Quests/`:
   - `QuestState.cs`, `QuestObjective.cs`, `QuestDefinition.cs`.
   - `QuestManager.cs`: Full FSM (NotStarted -> InProgress -> RequirementsMet -> Completed) with Hanoi Q1, Q2, Q3 definitions and prerequisite progression rules.
7. `Source/Core/Simulation/`:
   - `SimulationDriver.cs`: Headless game loop tick runner with mock ports for headless testing.

Build and Verification:
- Run `dotnet build` to ensure 0 errors and 0 warnings.
- Verify all implementations are clean, authentic, robust, and well-commented.
- Write your completion handoff report to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m1_1/handoff.md`
Follow Handoff Protocol (Observation, Logic Chain, Caveats, Conclusion, Verification Method).
Update `progress.md` regularly. Send a message to parent when done.
