using System;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.Quests;

namespace GameProject.Core.Interactions;

/// <summary>
/// Execution context supplied during interactable queries and trigger activations.
/// </summary>
public record InteractionContext(
    Vector2 PlayerPosition,
    Inventory Inventory,
    QuestManager QuestManager,
    QuestFlags QuestFlags,
    IEventBus EventBus,
    IAudioService AudioService
);

/// <summary>
/// Outcome result returned upon interacting with an entity.
/// </summary>
public sealed record InteractionResult(
    bool Success,
    string DialogueSpeaker,
    string DialogueText,
    string? GrantedItemId = null,
    bool QuestStarted = false,
    bool QuestCompleted = false
);

/// <summary>
/// Contract for world entities that can be targeted and triggered via [E] or Space.
/// </summary>
public interface IInteractable
{
    string Id { get; }
    string DisplayName { get; }
    Vector2 Position { get; }
    Vector2 InteractionCenter { get; }
    float InteractionRadius { get; } // Typically 48.0f
    float VisualHeight { get; }      // Visual height in pixels for bubble anchoring

    bool CanInteract(InteractionContext context);
    InteractionResult Interact(InteractionContext context);
    string GetPromptText(InteractionContext context);
}

