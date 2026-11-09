using System.Collections.Generic;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.UI;
using Xunit;

namespace GameProject.Tests;

public class DialogueTests
{
    private const string SampleDialogueJson = """
    {
      "dialogueId": "Dialogue_BaAn_Intro",
      "startNodeId": "Node_01",
      "nodes": [
        {
          "id": "Node_01",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Sad",
          "avatarPosition": "Right",
          "text": "Chào cháu, Bé Mây... Phố cổ mình giờ ảm đạm quá, bà muốn nấu nồi phở truyền thống mà thiếu nguyên liệu.",
          "nextNodeId": "Node_02",
          "branches": []
        },
        {
          "id": "Node_02",
          "speaker": "Bé Mây",
          "speakerAvatar": "Avatar_May_Determined",
          "avatarPosition": "Left",
          "text": "Bà đừng lo ạ! Cháu và Bút Chì sẽ đi gom đủ nguyên liệu về cho bà ngay!",
          "branches": [
            {
              "choiceId": "Choice_Accept",
              "choiceText": "Cháu sẵn sàng rồi, thưa bà!",
              "nextNodeId": "Node_Accept"
            },
            {
              "choiceId": "Choice_AskRecipe",
              "choiceText": "Bà cần những nguyên liệu gì ạ?",
              "nextNodeId": "Node_AskRecipe"
            }
          ]
        },
        {
          "id": "Node_Accept",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Cảm ơn cháu nhiều lắm! Hãy ghé các quầy hàng quanh Hàng Buồm và Lãn Ông nhé!",
          "nextNodeId": null,
          "actionTrigger": "Start_Quest_1"
        },
        {
          "id": "Node_AskRecipe",
          "speaker": "Bà Cụ Bán Phở",
          "speakerAvatar": "Avatar_BaAn_Smile",
          "avatarPosition": "Right",
          "text": "Bà cần bánh phở tươi, thịt bò, nước dùng hầm xương, hành lá và hoa hồi quế chi.",
          "nextNodeId": null,
          "actionTrigger": "Start_Quest_1"
        }
      ]
    }
    """;

    [Fact]
    public void LoadDialogueJson_ParsesGraphAndStartNodeCorrectly()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        var startNode = service.StartDialogue("Dialogue_BaAn_Intro");

        Assert.NotNull(startNode);
        Assert.Equal("Node_01", startNode.Id);
        Assert.Equal("Bà Cụ Bán Phở", startNode.Speaker);
        Assert.Contains("Phố cổ mình giờ ảm đạm quá", startNode.Text);
        Assert.True(service.IsDialogueActive);
    }

    [Fact]
    public void NodeTraversal_AdvancesLinearDialogueToNextNode()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        service.StartDialogue("Dialogue_BaAn_Intro");
        var nextNode = service.AdvanceToNext();

        Assert.NotNull(nextNode);
        Assert.Equal("Node_02", nextNode.Id);
        Assert.Equal("Bé Mây", nextNode.Speaker);
        Assert.Equal(2, nextNode.Branches.Count);
    }

    [Fact]
    public void BranchChoices_SelectsBranchAndTransitionsCorrectly()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        service.StartDialogue("Dialogue_BaAn_Intro");
        service.AdvanceToNext(); // at Node_02 with choices

        // Select Choice 0: "Cháu sẵn sàng rồi, thưa bà!" -> Node_Accept
        var chosenNode = service.SelectChoice(0);

        Assert.NotNull(chosenNode);
        Assert.Equal("Node_Accept", chosenNode.Id);
        Assert.Equal("Start_Quest_1", chosenNode.ActionTrigger);
    }

    [Fact]
    public void ActionTrigger_FiresEventWhenNodeIsReached()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        string? triggeredAction = null;
        service.OnActionTriggered += action => triggeredAction = action;

        service.StartDialogue("Dialogue_BaAn_Intro");
        service.AdvanceToNext();
        service.SelectChoice(1); // Node_AskRecipe -> ActionTrigger "Start_Quest_1"

        Assert.Equal("Start_Quest_1", triggeredAction);
    }

    [Fact]
    public void BrokenLink_GracefullyEndsDialogueWithoutCrash()
    {
        const string brokenJson = """
        {
          "dialogueId": "Dialogue_Broken",
          "startNodeId": "N1",
          "nodes": [
            {
              "id": "N1",
              "speaker": "Bé Mây",
              "speakerAvatar": "Avatar_May",
              "text": "Hello",
              "nextNodeId": "NonExistentNode"
            }
          ]
        }
        """;

        var service = new DialogueService();
        service.LoadDialogueJson(brokenJson);

        service.StartDialogue("Dialogue_Broken");
        var next = service.AdvanceToNext();

        // Target does not exist, should return null and end dialogue gracefully
        Assert.Null(next);
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void MalformedJson_HandlesGracefullyWithoutThrowing()
    {
        var service = new DialogueService();
        service.LoadDialogueJson("{ INVALID JSON [[[");

        var node = service.StartDialogue("Dialogue_BaAn_Intro");
        Assert.Null(node);
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void LoadDialogueJson_MultiGraphArray_LoadsAllGraphs()
    {
        const string multiJson = """
        [
          { "dialogueId": "G1", "startNodeId": "N1", "nodes": [{ "id": "N1", "speaker": "A", "text": "Hi" }] },
          { "dialogueId": "G2", "startNodeId": "N2", "nodes": [{ "id": "N2", "speaker": "B", "text": "Bye" }] }
        ]
        """;
        var service = new DialogueService();
        service.LoadDialogueJson(multiJson);

        Assert.NotNull(service.StartDialogue("G1"));
        Assert.NotNull(service.StartDialogue("G2"));
    }

    [Fact]
    public void LoadDialogueJson_EnvelopeWithDialoguesArray_LoadsAllGraphs()
    {
        const string envelopeJson = """
        {
          "dialogues": [
            { "dialogueId": "Env1", "startNodeId": "N1", "nodes": [{ "id": "N1", "speaker": "A", "text": "Hi" }] },
            { "dialogueId": "Env2", "startNodeId": "N2", "nodes": [{ "id": "N2", "speaker": "B", "text": "Bye" }] }
          ]
        }
        """;
        var service = new DialogueService();
        service.LoadDialogueJson(envelopeJson);

        Assert.NotNull(service.StartDialogue("Env1"));
        Assert.NotNull(service.StartDialogue("Env2"));
    }

    [Fact]
    public void DialogueService_PublishesEventsToEventBus()
    {
        var bus = new EventBus();
        var service = new DialogueService(bus);
        service.LoadDialogueJson(SampleDialogueJson);

        bool started = false;
        bool advanced = false;
        bool choiceSelected = false;
        bool ended = false;

        bus.Subscribe<DialogueStartedEvent>(_ => started = true);
        bus.Subscribe<DialogueLineAdvancedEvent>(_ => advanced = true);
        bus.Subscribe<DialogueChoiceSelectedEvent>(_ => choiceSelected = true);
        bus.Subscribe<DialogueEndedEvent>(_ => ended = true);

        service.StartDialogue("Dialogue_BaAn_Intro");
        Assert.True(started);
        Assert.True(advanced);

        service.AdvanceToNext(); // at Node_02 with choices
        service.SelectChoice("Choice_Accept");
        Assert.True(choiceSelected);

        service.AdvanceToNext(); // reaches end
        Assert.True(ended);
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void DialogueBranch_FiltersByRequiredQuestFlag()
    {
        var flags = new QuestFlags { Gate_LongBien_Unlocked = false };
        var node = new DialogueNode
        {
            Id = "TestNode",
            Branches = new List<DialogueBranch>
            {
                new() { ChoiceText = "Always open", NextNodeId = "N1" },
                new() { ChoiceText = "Only when gate open", NextNodeId = "N2", RequiredQuestFlag = "Gate_LongBien_Unlocked" }
            }
        };
        var service = new DialogueService();
        var available = service.GetAvailableBranches(node, flags);
        Assert.Single(available);
        Assert.Equal("Always open", available[0].ChoiceText);

        flags.Gate_LongBien_Unlocked = true;
        var unlocked = service.GetAvailableBranches(node, flags);
        Assert.Equal(2, unlocked.Count);
    }

    [Fact]
    public void SelectChoice_ByChoiceIdString_TransitionsCorrectly()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);

        service.StartDialogue("Dialogue_BaAn_Intro");
        service.AdvanceToNext();

        var node = service.SelectChoice("Choice_AskRecipe");
        Assert.NotNull(node);
        Assert.Equal("Node_AskRecipe", node.Id);
    }

    [Fact]
    public void DialogueBox_TypewriterFastForward_AndHeadlessExecution()
    {
        var box = new DialogueBox();
        box.Show("Bé Mây", "Chào phố cổ Hà Nội!");

        Assert.True(box.IsActive);
        Assert.False(box.IsTypingComplete);

        box.FastForward();
        Assert.True(box.IsTypingComplete);
        Assert.Equal("Chào phố cổ Hà Nội!", box.VisibleText);
    }

    [Fact]
    public void DialogueBox_ChoiceSelectionAndNavigation_Headless()
    {
        var service = new DialogueService();
        service.LoadDialogueJson(SampleDialogueJson);
        var box = new DialogueBox();

        var startNode = service.StartDialogue("Dialogue_BaAn_Intro");
        box.Show(startNode!, service);
        box.FastForward();
        Assert.True(box.IsTypingComplete);

        box.AdvanceToNext();
        Assert.Equal("Node_02", box.CurrentSpeaker == "Bé Mây" ? "Node_02" : "");
        Assert.Equal(2, box.CurrentBranches.Count);

        box.FastForward();
        Assert.Equal(0, box.SelectedChoiceIndex);

        box.SelectNextChoice();
        Assert.Equal(1, box.SelectedChoiceIndex);

        box.SelectPreviousChoice();
        Assert.Equal(0, box.SelectedChoiceIndex);

        box.ChooseOption(0);
        Assert.Equal("Node_Accept", service.CurrentNode?.Id);
    }

    [Fact]
    public void DialogueService_LoadDialogueFile_LoadsHanoiDialogues()
    {
        var service = new DialogueService();
        service.LoadDialogueFile("Data/dialogues_hanoi.json");

        Assert.True(service.LoadedGraphs.Count >= 5);
        Assert.NotNull(service.StartDialogue("Dialogue_BaAn_Intro"));
        Assert.NotNull(service.StartDialogue("Dialogue_Stall_BanhPho"));
        Assert.NotNull(service.StartDialogue("Dialogue_Gate_LongBien"));
    }
}
