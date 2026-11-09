using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameProject.Core.Events;
using GameProject.Core.Models;

namespace GameProject.Core.Dialogue;

public sealed class DialogueBranch
{
    [JsonPropertyName("choiceId")]
    public string? ChoiceId { get; set; }

    [JsonPropertyName("choiceText")]
    public string ChoiceText { get; set; } = "";

    [JsonPropertyName("nextNodeId")]
    public string NextNodeId { get; set; } = "";

    [JsonPropertyName("requiredQuestFlag")]
    public string? RequiredQuestFlag { get; set; }
}

public sealed class DialogueNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("speaker")]
    public string Speaker { get; set; } = "";

    [JsonPropertyName("speakerAvatar")]
    public string SpeakerAvatar { get; set; } = "";

    [JsonPropertyName("avatarPosition")]
    public string AvatarPosition { get; set; } = "Left";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("sfxVoiceBlip")]
    public string? SfxVoiceBlip { get; set; }

    [JsonPropertyName("branches")]
    public List<DialogueBranch> Branches { get; set; } = new();

    [JsonPropertyName("nextNodeId")]
    public string? NextNodeId { get; set; }

    [JsonPropertyName("actionTrigger")]
    public string? ActionTrigger { get; set; }
}

public sealed class DialogueGraph
{
    [JsonPropertyName("dialogueId")]
    public string DialogueId { get; set; } = "";

    [JsonPropertyName("startNodeId")]
    public string StartNodeId { get; set; } = "";

    [JsonPropertyName("nodes")]
    public List<DialogueNode> Nodes { get; set; } = new();
}

public sealed class DialogueCollection
{
    [JsonPropertyName("dialogues")]
    public List<DialogueGraph> Dialogues { get; set; } = new();
}

public interface IDialogueService
{
    void LoadDialogueJson(string json);
    void LoadDialogueFile(string path);
    DialogueNode? StartDialogue(string dialogueId);
    DialogueNode? AdvanceToNext();
    DialogueNode? SelectChoice(int choiceIndex);
    DialogueNode? SelectChoice(string choiceId);
    DialogueNode? SelectChoice(DialogueBranch branch);
    void EndDialogue();
    IReadOnlyList<DialogueBranch> GetAvailableBranches(DialogueNode? node, QuestFlags? flags = null);
    DialogueNode? CurrentNode { get; }
    DialogueGraph? ActiveGraph { get; }
    bool IsDialogueActive { get; }
    IReadOnlyDictionary<string, DialogueGraph> LoadedGraphs { get; }
    event Action<string>? OnActionTriggered;
}

public sealed class DialogueService : IDialogueService
{
    private readonly Dictionary<string, DialogueGraph> _dialogues = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEventBus? _eventBus;
    private DialogueGraph? _activeGraph;
    private DialogueNode? _currentNode;

    public DialogueNode? CurrentNode => _currentNode;
    public DialogueGraph? ActiveGraph => _activeGraph;
    public bool IsDialogueActive => _currentNode != null;
    public IReadOnlyDictionary<string, DialogueGraph> LoadedGraphs => _dialogues;

    public event Action<string>? OnActionTriggered;

    public DialogueService(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    public void LoadDialogueFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var candidates = new List<string>
        {
            path,
            Path.Combine(AppContext.BaseDirectory, path),
            Path.Combine(AppContext.BaseDirectory, "Data", Path.GetFileName(path)),
            Path.Combine(AppContext.BaseDirectory, "Source", "Data", Path.GetFileName(path)),
            Path.Combine(Directory.GetCurrentDirectory(), path),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", Path.GetFileName(path)),
            Path.Combine(Directory.GetCurrentDirectory(), "Source", "Data", Path.GetFileName(path))
        };

        // Also probe parent directories up to 4 levels
        string? currentDir = AppContext.BaseDirectory;
        for (int i = 0; i < 4 && currentDir != null; i++)
        {
            candidates.Add(Path.Combine(currentDir, path));
            candidates.Add(Path.Combine(currentDir, "Data", Path.GetFileName(path)));
            candidates.Add(Path.Combine(currentDir, "Source", "Data", Path.GetFileName(path)));
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                try
                {
                    string json = File.ReadAllText(candidate);
                    LoadDialogueJson(json);
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DialogueService] Failed reading dialogue file '{candidate}': {ex.Message}");
                }
            }
        }

        Console.WriteLine($"[DialogueService] Warning: Could not locate dialogue file '{path}' in probing locations.");
    }

    public void LoadDialogueJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<DialogueGraph>>(json, options);
                if (list != null)
                {
                    foreach (var g in list.Where(g => !string.IsNullOrEmpty(g.DialogueId)))
                    {
                        _dialogues[g.DialogueId] = g;
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("dialogues", out var dialoguesProp) && dialoguesProp.ValueKind == JsonValueKind.Array)
                {
                    var envelope = JsonSerializer.Deserialize<DialogueCollection>(json, options);
                    if (envelope?.Dialogues != null)
                    {
                        foreach (var g in envelope.Dialogues.Where(g => !string.IsNullOrEmpty(g.DialogueId)))
                        {
                            _dialogues[g.DialogueId] = g;
                        }
                    }
                }
                else
                {
                    var single = JsonSerializer.Deserialize<DialogueGraph>(json, options);
                    if (single != null && !string.IsNullOrEmpty(single.DialogueId))
                    {
                        _dialogues[single.DialogueId] = single;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DialogueService] Malformed JSON: {ex.Message}");
        }
    }

    public DialogueNode? StartDialogue(string dialogueId)
    {
        if (!_dialogues.TryGetValue(dialogueId, out var graph))
        {
            _activeGraph = null;
            _currentNode = null;
            return null;
        }

        _activeGraph = graph;
        _currentNode = graph.Nodes.FirstOrDefault(n => n.Id == graph.StartNodeId);

        if (_currentNode != null)
        {
            _eventBus?.Publish(new DialogueStartedEvent(_activeGraph.DialogueId, _currentNode.Speaker));
            _eventBus?.Publish(new DialogueLineAdvancedEvent(_activeGraph.DialogueId, _currentNode.Id, _currentNode.Speaker, _currentNode.Text));
        }

        TriggerActionIfAny(_currentNode);
        return _currentNode;
    }

    public DialogueNode? AdvanceToNext()
    {
        if (_activeGraph == null || _currentNode == null) return null;

        // If current node has branching choices, cannot automatically advance
        if (_currentNode.Branches != null && _currentNode.Branches.Count > 0)
        {
            return _currentNode;
        }

        if (string.IsNullOrEmpty(_currentNode.NextNodeId))
        {
            EndDialogue();
            return null;
        }

        var next = _activeGraph.Nodes.FirstOrDefault(n => n.Id == _currentNode.NextNodeId);
        _currentNode = next;

        if (_currentNode != null)
        {
            _eventBus?.Publish(new DialogueLineAdvancedEvent(_activeGraph.DialogueId, _currentNode.Id, _currentNode.Speaker, _currentNode.Text));
            TriggerActionIfAny(_currentNode);
        }
        else
        {
            EndDialogue();
        }

        return _currentNode;
    }

    public DialogueNode? SelectChoice(int choiceIndex)
    {
        if (_activeGraph == null || _currentNode == null) return null;
        if (_currentNode.Branches == null || choiceIndex < 0 || choiceIndex >= _currentNode.Branches.Count)
            return null;

        var choice = _currentNode.Branches[choiceIndex];
        _eventBus?.Publish(new DialogueChoiceSelectedEvent(_activeGraph.DialogueId, choice.ChoiceId ?? choice.NextNodeId, choice.NextNodeId));

        _currentNode = _activeGraph.Nodes.FirstOrDefault(n => n.Id == choice.NextNodeId);
        if (_currentNode != null)
        {
            _eventBus?.Publish(new DialogueLineAdvancedEvent(_activeGraph.DialogueId, _currentNode.Id, _currentNode.Speaker, _currentNode.Text));
            TriggerActionIfAny(_currentNode);
        }
        else
        {
            EndDialogue();
        }

        return _currentNode;
    }

    public DialogueNode? SelectChoice(string choiceId)
    {
        if (_activeGraph == null || _currentNode == null || _currentNode.Branches == null) return null;

        for (int i = 0; i < _currentNode.Branches.Count; i++)
        {
            var b = _currentNode.Branches[i];
            if (string.Equals(b.ChoiceId, choiceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.NextNodeId, choiceId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(b.ChoiceText, choiceId, StringComparison.OrdinalIgnoreCase))
            {
                return SelectChoice(i);
            }
        }

        if (int.TryParse(choiceId, out int idx))
        {
            return SelectChoice(idx);
        }

        return null;
    }

    public DialogueNode? SelectChoice(DialogueBranch branch)
    {
        if (_activeGraph == null || _currentNode == null || _currentNode.Branches == null || branch == null) return null;

        int rawIndex = _currentNode.Branches.IndexOf(branch);
        if (rawIndex >= 0)
        {
            return SelectChoice(rawIndex);
        }

        for (int i = 0; i < _currentNode.Branches.Count; i++)
        {
            var b = _currentNode.Branches[i];
            if (!string.IsNullOrEmpty(branch.ChoiceId) && string.Equals(b.ChoiceId, branch.ChoiceId, StringComparison.OrdinalIgnoreCase))
            {
                return SelectChoice(i);
            }
            if (!string.IsNullOrEmpty(branch.NextNodeId) && string.Equals(b.NextNodeId, branch.NextNodeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(b.ChoiceText, branch.ChoiceText, StringComparison.OrdinalIgnoreCase))
            {
                return SelectChoice(i);
            }
        }

        if (!string.IsNullOrEmpty(branch.ChoiceId))
        {
            return SelectChoice(branch.ChoiceId);
        }

        return null;
    }

    public void EndDialogue()
    {
        if (_activeGraph != null)
        {
            _eventBus?.Publish(new DialogueEndedEvent(_activeGraph.DialogueId));
        }
        _currentNode = null;
        _activeGraph = null;
    }

    public IReadOnlyList<DialogueBranch> GetAvailableBranches(DialogueNode? node, QuestFlags? flags = null)
    {
        if (node?.Branches == null) return Array.Empty<DialogueBranch>();
        if (flags == null) return node.Branches;

        return node.Branches.Where(b =>
            string.IsNullOrEmpty(b.RequiredQuestFlag) || flags.GetFlag(b.RequiredQuestFlag)
        ).ToList();
    }

    private void TriggerActionIfAny(DialogueNode? node)
    {
        if (node != null && !string.IsNullOrEmpty(node.ActionTrigger))
        {
            OnActionTriggered?.Invoke(node.ActionTrigger);
        }
    }
}
