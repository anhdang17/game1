using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Ports;

namespace GameProject.Core.Interactions;

/// <summary>
/// Manages proximity detection, dual-threshold distance hysteresis, nearest-target priority selection, and interaction triggers.
/// </summary>
public sealed class InteractionManager
{
    public const float DefaultRadius = 48.0f;
    public const float DefaultEnterRadius = 48.0f;
    public const float DefaultExitRadius = 54.0f;
    public const float HysteresisBuffer = DefaultExitRadius - DefaultEnterRadius; // 6.0px buffer
    public const float AudioPromptCooldown = 0.25f; // Minimum cooldown in seconds between prompt chimes

    private readonly List<IInteractable> _interactables = new();
    private float _audioPromptCooldownTimer;

    public IReadOnlyList<IInteractable> Interactables => _interactables;
    public IInteractable? CurrentTarget { get; private set; }

    public float BubbleAlpha { get; private set; }
    public float BubbleBobTimer { get; private set; }
    public float AudioPromptCooldownTimer => _audioPromptCooldownTimer;

    public void Register(IInteractable interactable)
    {
        ArgumentNullException.ThrowIfNull(interactable);
        if (!_interactables.Contains(interactable))
        {
            _interactables.Add(interactable);
        }
    }

    public void Unregister(IInteractable interactable)
    {
        _interactables.Remove(interactable);
        if (ReferenceEquals(CurrentTarget, interactable))
        {
            CurrentTarget = null;
        }
    }

    public void Clear()
    {
        _interactables.Clear();
        CurrentTarget = null;
        BubbleAlpha = 0f;
        _audioPromptCooldownTimer = 0f;
    }

    /// <summary>
    /// Finds the nearest valid interactable entity within its base detection radius (default 48px).
    /// </summary>
    public IInteractable? FindNearestInteractable(Vector2 playerPosition, InteractionContext context)
        => FindNearestInteractable(playerPosition, context, null);

    /// <summary>
    /// Finds the nearest valid interactable entity within detection radius.
    /// Uses enter threshold (default 48px) for candidate entities and exit threshold (default 54px) for retaining the current target.
    /// </summary>
    public IInteractable? FindNearestInteractable(Vector2 playerPosition, InteractionContext context, IInteractable? currentTarget)
    {
        IInteractable? nearest = null;
        float minDistanceSq = float.MaxValue;

        for (int i = 0; i < _interactables.Count; i++)
        {
            var entity = _interactables[i];
            if (!entity.CanInteract(context))
                continue;

            float enterRadius = entity.InteractionRadius > 0f ? entity.InteractionRadius : DefaultEnterRadius;
            float maxRadius = ReferenceEquals(entity, currentTarget)
                ? enterRadius + HysteresisBuffer
                : enterRadius;
            float rSq = maxRadius * maxRadius;

            float dx = entity.InteractionCenter.X - playerPosition.X;
            float dy = entity.InteractionCenter.Y - playerPosition.Y;
            float distSq = dx * dx + dy * dy;

            if (distSq <= rSq)
            {
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    nearest = entity;
                }
                else if (MathF.Abs(distSq - minDistanceSq) < 0.001f && nearest != null)
                {
                    // Deterministic tie-break by ID
                    if (string.CompareOrdinal(entity.Id, nearest.Id) < 0)
                    {
                        nearest = entity;
                    }
                }
            }
        }

        return nearest;
    }

    public void Update(float deltaTime, Vector2 playerPosition, InteractionContext context)
    {
        BubbleBobTimer += deltaTime;
        if (_audioPromptCooldownTimer > 0f)
        {
            _audioPromptCooldownTimer = MathF.Max(0f, _audioPromptCooldownTimer - deltaTime);
        }

        var prevTarget = CurrentTarget;
        CurrentTarget = FindNearestInteractable(playerPosition, context, CurrentTarget);

        if (CurrentTarget != null)
        {
            BubbleAlpha = Math.Clamp(BubbleAlpha + deltaTime * 8.0f, 0.0f, 1.0f);
            if (prevTarget != CurrentTarget && _audioPromptCooldownTimer <= 0f)
            {
                context.AudioService.PlaySfx(SfxType.InteractPrompt);
                _audioPromptCooldownTimer = AudioPromptCooldown;
            }
        }
        else
        {
            BubbleAlpha = Math.Clamp(BubbleAlpha - deltaTime * 8.0f, 0.0f, 1.0f);
        }
    }

    public InteractionResult? TryInteract(InteractionContext context)
    {
        if (CurrentTarget == null || !CurrentTarget.CanInteract(context))
            return null;

        return CurrentTarget.Interact(context);
    }
}
