using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Events;
using GameProject.Core.Models;

namespace GameProject.Core.Quests;

/// <summary>
/// Pure C# Domain Finite State Machine managing quest lifecycles, prerequisites, and objective progression.
/// </summary>
public sealed class QuestManager
{
    private readonly Dictionary<string, QuestDefinition> _quests = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEventBus? _eventBus;

    public QuestManager(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    public void RegisterQuest(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        _quests[quest.Id] = quest;
    }

    public QuestDefinition? GetQuest(string questId)
    {
        _quests.TryGetValue(questId, out var quest);
        return quest;
    }

    public IReadOnlyList<QuestDefinition> GetAllQuests() => _quests.Values.ToList();

    public IReadOnlyList<QuestDefinition> GetQuestsByState(QuestState state) =>
        _quests.Values.Where(q => q.State == state).ToList();

    public bool IsQuestCompleted(string questId) =>
        _quests.TryGetValue(questId, out var quest) && quest.State == QuestState.Completed;

    public bool IsQuestInProgress(string questId) =>
        _quests.TryGetValue(questId, out var quest) && quest.State == QuestState.InProgress;

    public QuestState GetQuestState(string questId) =>
        _quests.TryGetValue(questId, out var quest) ? quest.State : QuestState.NotStarted;

    /// <summary>
    /// Checks whether all prerequisite quests for this quest are completed.
    /// </summary>
    public bool CanStartQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest))
            return false;

        if (quest.State != QuestState.NotStarted)
            return false;

        foreach (var prereqId in quest.PrerequisiteQuestIds)
        {
            if (!IsQuestCompleted(prereqId))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Transitions quest from NotStarted to InProgress if prerequisites are satisfied.
    /// </summary>
    public bool StartQuest(string questId)
    {
        if (!CanStartQuest(questId))
            return false;

        var quest = _quests[questId];
        quest.State = QuestState.InProgress;

        _eventBus?.Publish(new QuestStartedEvent(questId, quest.Title, quest.Description));
        return true;
    }

    /// <summary>
    /// Records objective progress for a specific quest.
    /// If all mandatory objectives complete, state advances to RequirementsMet.
    /// </summary>
    public bool RecordObjectiveProgress(string questId, string objectiveId, int amount = 1)
    {
        if (!_quests.TryGetValue(questId, out var quest))
            return false;

        if (quest.State != QuestState.InProgress)
            return false;

        var objective = quest.GetObjective(objectiveId);
        if (objective == null)
            return false;

        bool changed = objective.IncrementProgress(amount);
        if (changed)
        {
            _eventBus?.Publish(new QuestProgressUpdatedEvent(questId, objectiveId, objective.CurrentCount, objective.TargetCount, objective.Description));

            if (quest.AreAllMandatoryObjectivesComplete)
            {
                quest.State = QuestState.RequirementsMet;
                _eventBus?.Publish(new QuestRequirementsMetEvent(questId));
            }
        }

        return changed;
    }

    /// <summary>
    /// Overload that finds the first active InProgress quest containing the given objective ID and advances it.
    /// Supports the contract defined in PROJECT.md: RecordObjectiveProgress("Item_BanhPho").
    /// </summary>
    public bool RecordObjectiveProgress(string objectiveId, int amount = 1)
    {
        foreach (var quest in _quests.Values)
        {
            if (quest.State == QuestState.InProgress && quest.GetObjective(objectiveId) != null)
            {
                return RecordObjectiveProgress(quest.Id, objectiveId, amount);
            }
        }
        return false;
    }

    /// <summary>
    /// Officially completes the quest and transitions to Completed.
    /// </summary>
    public bool CompleteQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest))
            return false;

        if (quest.State == QuestState.Completed)
            return true; // Idempotent

        if (quest.State != QuestState.RequirementsMet && !quest.AreAllMandatoryObjectivesComplete)
            return false;

        quest.State = QuestState.Completed;
        _eventBus?.Publish(new QuestCompletedEvent(questId, quest.Title));
        return true;
    }

    /// <summary>
    /// Synchronizes current quest states and objectives with persistent QuestFlags.
    /// </summary>
    public void ExportToFlags(QuestFlags flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        if (_quests.TryGetValue(HanoiQuests.Quest1Id, out var q1))
        {
            flags.Q1_Started = q1.State >= QuestState.InProgress;
            flags.Q1_BanhPho_Collected = q1.GetObjective(ItemIds.BanhPho)?.IsCompleted ?? false;
            flags.Q1_ThitBo_Collected = q1.GetObjective(ItemIds.ThitBo)?.IsCompleted ?? false;
            flags.Q1_NuocDung_Collected = q1.GetObjective(ItemIds.NuocDung)?.IsCompleted ?? false;
            flags.Q1_HanhLa_Collected = q1.GetObjective(ItemIds.HanhLa)?.IsCompleted ?? false;
            flags.Q1_ThaoMoc_Collected = (q1.GetObjective(ItemIds.ThaoMoc)?.IsCompleted ?? false) ||
                                         (q1.GetObjective(ItemIds.GiaVi)?.IsCompleted ?? false);
            flags.Q1_Completed = q1.State == QuestState.Completed;
        }

        if (_quests.TryGetValue(HanoiQuests.Quest2Id, out var q2))
        {
            flags.Q2_Started = q2.State >= QuestState.InProgress;
            flags.Q2_PhoAssembled = q2.GetObjective("assemble_pho")?.IsCompleted ?? false;
            flags.Q2_Completed = q2.State == QuestState.Completed;
        }

        if (_quests.TryGetValue(HanoiQuests.Quest3Id, out var q3))
        {
            flags.Q3_Started = q3.State >= QuestState.InProgress;
            flags.Q3_GridSolved = q3.GetObjective("clear_long_bien_path")?.IsCompleted ?? false;
            flags.Q3_Completed = q3.State == QuestState.Completed;
        }
    }

    /// <summary>
    /// Rehydrates quest states and objective counts from persistent QuestFlags.
    /// </summary>
    public void HydrateFromFlags(QuestFlags flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        if (_quests.TryGetValue(HanoiQuests.Quest1Id, out var q1))
        {
            if (flags.Q1_BanhPho_Collected) q1.GetObjective(ItemIds.BanhPho)?.SetProgress(1);
            if (flags.Q1_ThitBo_Collected) q1.GetObjective(ItemIds.ThitBo)?.SetProgress(1);
            if (flags.Q1_NuocDung_Collected) q1.GetObjective(ItemIds.NuocDung)?.SetProgress(1);
            if (flags.Q1_HanhLa_Collected) q1.GetObjective(ItemIds.HanhLa)?.SetProgress(1);
            if (flags.Q1_ThaoMoc_Collected)
            {
                q1.GetObjective(ItemIds.ThaoMoc)?.SetProgress(1);
                q1.GetObjective(ItemIds.GiaVi)?.SetProgress(1);
            }

            if (flags.Q1_Completed)
                q1.State = QuestState.Completed;
            else if (q1.AreAllMandatoryObjectivesComplete)
                q1.State = QuestState.RequirementsMet;
            else if (flags.Q1_Started)
                q1.State = QuestState.InProgress;
        }

        if (_quests.TryGetValue(HanoiQuests.Quest2Id, out var q2))
        {
            if (flags.Q2_PhoAssembled) q2.GetObjective("assemble_pho")?.SetProgress(1);

            if (flags.Q2_Completed)
                q2.State = QuestState.Completed;
            else if (q2.AreAllMandatoryObjectivesComplete)
                q2.State = QuestState.RequirementsMet;
            else if (flags.Q2_Started)
                q2.State = QuestState.InProgress;
        }

        if (_quests.TryGetValue(HanoiQuests.Quest3Id, out var q3))
        {
            if (flags.Q3_GridSolved) q3.GetObjective("clear_long_bien_path")?.SetProgress(1);

            if (flags.Q3_Completed)
                q3.State = QuestState.Completed;
            else if (q3.AreAllMandatoryObjectivesComplete)
                q3.State = QuestState.RequirementsMet;
            else if (flags.Q3_Started)
                q3.State = QuestState.InProgress;
        }
    }

    public void ResetAllQuests()
    {
        foreach (var quest in _quests.Values)
        {
            quest.State = QuestState.NotStarted;
            foreach (var objective in quest.Objectives)
            {
                objective.Reset();
            }
        }
    }
}
