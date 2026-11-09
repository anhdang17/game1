using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameProject.Core.Dialogue;

public sealed class DialogueBranch
{
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

public interface IDialogueService
{
    void LoadDialogueJson(string json);
    DialogueNode? StartDialogue(string dialogueId);
    DialogueNode? AdvanceToNext();
    DialogueNode? SelectChoice(int choiceIndex);
    DialogueNode? CurrentNode { get; }
    bool IsDialogueActive { get; }
    event Action<string>? OnActionTriggered;
}

public sealed class DialogueService : IDialogueService
{
    private readonly Dictionary<string, DialogueGraph> _dialogues = new();
    private DialogueGraph? _activeGraph;
    private DialogueNode? _currentNode;

    public DialogueNode? CurrentNode => _currentNode;
    public bool IsDialogueActive => _currentNode != null;

    public event Action<string>? OnActionTriggered;

    public void LoadDialogueJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var graph = JsonSerializer.Deserialize<DialogueGraph>(json, options);
            if (graph != null && !string.IsNullOrEmpty(graph.DialogueId))
            {
                _dialogues[graph.DialogueId] = graph;
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
            // End of dialogue
            _currentNode = null;
            _activeGraph = null;
            return null;
        }

        _currentNode = _activeGraph.Nodes.FirstOrDefault(n => n.Id == _currentNode.NextNodeId);
        TriggerActionIfAny(_currentNode);
        return _currentNode;
    }

    public DialogueNode? SelectChoice(int choiceIndex)
    {
        if (_activeGraph == null || _currentNode == null) return null;
        if (_currentNode.Branches == null || choiceIndex < 0 || choiceIndex >= _currentNode.Branches.Count)
            return null;

        var choice = _currentNode.Branches[choiceIndex];
        _currentNode = _activeGraph.Nodes.FirstOrDefault(n => n.Id == choice.NextNodeId);
        TriggerActionIfAny(_currentNode);
        return _currentNode;
    }

    private void TriggerActionIfAny(DialogueNode? node)
    {
        if (node != null && !string.IsNullOrEmpty(node.ActionTrigger))
        {
            OnActionTriggered?.Invoke(node.ActionTrigger);
        }
    }
}
