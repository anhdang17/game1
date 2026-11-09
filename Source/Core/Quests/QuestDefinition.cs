using System;
using System.Collections.Generic;
using System.Linq;

namespace GameProject.Core.Quests;

/// <summary>
/// Represents a quest's static definition, prerequisites, and dynamic progress state.
/// </summary>
public sealed class QuestDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RegionId { get; init; } = "Hanoi";
    public IReadOnlyList<string> PrerequisiteQuestIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<QuestObjective> Objectives { get; init; } = Array.Empty<QuestObjective>();

    public QuestState State { get; internal set; } = QuestState.NotStarted;

    public bool AreAllMandatoryObjectivesComplete =>
        Objectives.Where(o => !o.IsOptional).All(o => o.IsCompleted);

    public QuestObjective? GetObjective(string objectiveId)
    {
        string normalized = objectiveId switch
        {
            "Objective_AssemblePho" => "assemble_pho",
            "Objective_ClearStallPath" => "clear_long_bien_path",
            _ => objectiveId
        };

        return Objectives.FirstOrDefault(o =>
            o.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            o.Id.Equals(objectiveId, StringComparison.OrdinalIgnoreCase));
    }
}
