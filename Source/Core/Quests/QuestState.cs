namespace GameProject.Core.Quests;

/// <summary>
/// Represents the finite states of a quest lifecycle.
/// </summary>
public enum QuestState
{
    /// <summary>Quest is registered but not yet accepted or triggered.</summary>
    NotStarted = 0,

    /// <summary>Quest is active; player is pursuing objectives.</summary>
    InProgress = 1,

    /// <summary>All mandatory objectives are met; ready for turn-in/climax.</summary>
    RequirementsMet = 2,

    /// <summary>Quest is officially resolved; rewards granted and next quests unlocked.</summary>
    Completed = 3
}
