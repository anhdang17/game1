using System;
using GameProject.Core.State;

namespace GameProject.Core.Events;

// --- State Transitions ---
public record GameStateChangedEvent(GameState OldState, GameState NewState, object? Context, bool IsOverlay) : GameEventBase;

// --- Quest & Objective Events ---
public record QuestStartedEvent(string QuestId, string Title = "", string Description = "") : GameEventBase;
public record QuestProgressUpdatedEvent(string QuestId, string ObjectiveId, int CurrentCount, int TargetCount, string ObjectiveText = "") : GameEventBase;
public record QuestRequirementsMetEvent(string QuestId) : GameEventBase;
public record QuestCompletedEvent(string QuestId, string Title = "") : GameEventBase;

// --- Inventory & Gathering Events ---
public record ItemCollectedEvent(string ItemId, string ItemName = "", int CurrentCount = 1, int RequiredCount = 1) : GameEventBase;
public record ItemRemovedEvent(string ItemId, int RemainingCount = 0) : GameEventBase;

// --- Puzzle 2: Pho Assembly Events ---
public record PhoIngredientPlacedEvent(int SlotIndex, string IngredientId, bool IsCorrectOrder) : GameEventBase;
public record PhoAssemblyAttemptedEvent(bool IsSuccess, string? ErrorMessage = null) : GameEventBase;
public record PhoAssemblyCompletedEvent() : GameEventBase;
public record PhoPuzzleResetEvent() : GameEventBase;

// --- Puzzle 3: Long Bien Stall Sliding Events ---
public record StallPushedEvent(string ObjectId, int FromX, int FromY, int ToX, int ToY, bool Success) : GameEventBase;
public record StallPuzzleResetEvent() : GameEventBase;
public record StallPuzzleSolvedEvent(int TotalMoves) : GameEventBase;
public record PuzzleSolvedEvent(string PuzzleId) : GameEventBase;

// --- Progression & World Restoration Events ---
public record GateUnlockedEvent(string GateId, string RegionId = "Hanoi") : GameEventBase;
public record ColorRestoredEvent(string RegionId, float OldPercentage, float NewPercentage, string SourceTrigger = "") : GameEventBase;
public record RegionColorUpdatedEvent(string RegionId, float NewPercentage) : GameEventBase;
public record RegionCompletedEvent(string RegionId) : GameEventBase;
public record RegionUnlockedEvent(string RegionId) : GameEventBase;

// --- Narrative & Dialogue Events ---
public record DialogueStartedEvent(string DialogueId, string Speaker = "") : GameEventBase;
public record DialogueLineAdvancedEvent(string DialogueId, string NodeId, string Speaker, string Text) : GameEventBase;
public record DialogueChoiceSelectedEvent(string DialogueId, string ChoiceId, string NextNodeId) : GameEventBase;
public record DialogueEndedEvent(string DialogueId) : GameEventBase;
public record CutsceneTriggeredEvent(string CutsceneId) : GameEventBase;
public record CutsceneEndedEvent(string CutsceneId) : GameEventBase;
public record LetterOpenedEvent(string LetterId, string Sender = "Bóng Xám") : GameEventBase;

// --- Companion & Hint Events ---
public record HintRequestedEvent(string ActiveQuestId, string HintText, bool IsIdleTrigger = false) : GameEventBase;

// --- Collection Album Awards ---
public record JournalPageAwardedEvent(string PageId, string Title = "", string RegionId = "Hanoi") : GameEventBase;
public record StampAwardedEvent(string StampId, string Name = "", string RegionId = "Hanoi") : GameEventBase;
public record FactCardUnlockedEvent(string CardId, string Title = "", string RegionId = "Hanoi") : GameEventBase;

// --- Persistence Events ---
public record GameSavedEvent(string FilePath, bool Success, string? ErrorMessage = null) : GameEventBase;
public record GameLoadedEvent(string FilePath, bool Success, bool WasCorruptedAndRecovered = false) : GameEventBase;
